using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net.Http.Headers;

namespace EmbyBangumiHanimePlugin
{
    // 定义该 Provider 支持 Series (番剧)
    public class BangumiHanimeProvider : IRemoteMetadataProvider<Series, SeriesInfo>
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<BangumiHanimeProvider> _logger;

        public BangumiHanimeProvider(IHttpClientFactory httpClientFactory, ILogger<BangumiHanimeProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public string Name => "Bangumi & Hanime";

        // 核心方法：获取元数据（详情）
        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>
            {
                Item = new Series()
            };

            var config = Plugin.Instance.Configuration;
            
            // 优先检查 Bangumi ID
            if (info.ProviderIds.TryGetValue("Bangumi", out var bgmId))
            {
                await FetchBangumiMetadata(bgmId, result.Item, config, cancellationToken);
                result.HasMetadata = true;
            }
            // 如果没有 ID，尝试 Hanime 搜索刮削
            else if (!string.IsNullOrEmpty(info.Name)) 
            {
                await FetchHanimeMetadata(info.Name, result.Item, config, cancellationToken);
                result.HasMetadata = true;
            }

            return result;
        }

        // 核心方法：搜索
        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            var list = new List<RemoteSearchResult>();
            var config = Plugin.Instance.Configuration;

            if (string.IsNullOrWhiteSpace(searchInfo.Name)) return list;

            // 1. 搜索 Bangumi
            try
            {
                var query = System.Web.HttpUtility.UrlEncode(searchInfo.Name);
                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "EmbyBangumiPlugin/1.0");
                
                // 使用 Bangumi 搜索 API
                var response = await client.GetAsync($"https://api.bgm.tv/search/subject/{query}?type=2&responseGroup=small", cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("list", out var items))
                    {
                        foreach (var item in items.EnumerateArray())
                        {
                            var id = item.GetProperty("id").ToString();
                            var name = item.GetProperty("name").GetString();
                            var nameCn = item.TryGetProperty("name_cn", out var cn) ? cn.GetString() : "";

                            list.Add(new RemoteSearchResult
                            {
                                Name = string.IsNullOrEmpty(nameCn) ? name : nameCn,
                                ProductionYear = null, // 解析起来比较麻烦，暂时略过
                                ProviderIds = { { "Bangumi", id } },
                                SearchProviderName = Name,
                                ImageUrl = item.TryGetProperty("images", out var imgs) && imgs.TryGetProperty("common", out var url) ? url.GetString() : null
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Bangumi Search Failed");
            }

            return list;
        }

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            return client.GetAsync(url, cancellationToken);
        }

        // Bangumi 获取详情逻辑
        private async Task FetchBangumiMetadata(string subjectId, Series series, PluginConfiguration config, CancellationToken ct)
        {
            try
            {
                // 检查 Token 是否需要刷新
                await CheckAndRefreshToken(config);

                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "EmbyBangumiPlugin/1.0");
                if (!string.IsNullOrEmpty(config.BangumiAccessToken))
                {
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", config.BangumiAccessToken);
                }

                var url = $"https://api.bgm.tv/v0/subjects/{subjectId}";
                var json = await client.GetStringAsync(url, ct);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                series.Name = root.GetProperty("name").GetString();
                if(root.TryGetProperty("name_cn", out var cnName) && !string.IsNullOrEmpty(cnName.GetString()))
                {
                    series.Name = cnName.GetString();
                }
                
                series.Overview = root.GetProperty("summary").GetString();
                
                if (root.TryGetProperty("date", out var dateStr))
                    if (DateTime.TryParse(dateStr.GetString(), out var date))
                        series.PremiereDate = date;

                series.SetProviderId("Bangumi", subjectId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching Bangumi metadata");
            }
        }

        // Hanime 获取逻辑 (基于 HTML 简单正则)
        private async Task FetchHanimeMetadata(string query, Series series, PluginConfiguration config, CancellationToken ct)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                
                if (!string.IsNullOrEmpty(config.HanimeCookie))
                {
                    client.DefaultRequestHeaders.Add("Cookie", config.HanimeCookie);
                }

                // 搜索页面
                var searchUrl = $"https://hanime1.me/search?query={System.Web.HttpUtility.UrlEncode(query)}";
                var html = await client.GetStringAsync(searchUrl, ct);

                // 简单的正则匹配第一个结果链接
                // 需注意：Hanime1 的 DOM 结构可能会变，这里匹配 <a href="/watch?v=xxxxx">
                var match = Regex.Match(html, "href=\"(https://hanime1\\.me/watch\\?v=[^\"]+)\"");
                
                if (match.Success)
                {
                    var videoUrl = match.Groups[1].Value;
                    var videoHtml = await client.GetStringAsync(videoUrl, ct);

                    // 提取标题 (Title)
                    var titleMatch = Regex.Match(videoHtml, "<meta property=\"og:title\" content=\"([^\"]+)\"");
                    if (titleMatch.Success) series.Name = titleMatch.Groups[1].Value;

                    // 提取简介 (Description)
                    var descMatch = Regex.Match(videoHtml, "<meta property=\"og:description\" content=\"([^\"]+)\"");
                    if (descMatch.Success) series.Overview = descMatch.Groups[1].Value;

                    // 提取封面
                    var imgMatch = Regex.Match(videoHtml, "<meta property=\"og:image\" content=\"([^\"]+)\"");
                    if (imgMatch.Success) series.PrimaryImagePath = imgMatch.Groups[1].Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping Hanime");
            }
        }

        // 自动刷新 Token 逻辑
        private async Task CheckAndRefreshToken(PluginConfiguration config)
        {
            if (string.IsNullOrEmpty(config.BangumiRefreshToken)) return;
            if (DateTime.Now < config.BangumiTokenExpiry.AddDays(-3)) return; // 还有3天以上不过期就不刷

            try
            {
                using var client = _httpClientFactory.CreateClient();
                // 构造刷新请求 (Bangumi OAuth Refresh Flow)
                // 注意：由于没有 App Secret (这是纯客户端插件)，通常需要 App Secret 才能刷新。
                // 如果是个人使用，请在 bgm.tv 申请应用时选择 "Desktop/Mobile" 类应用，可能不需要 Secret。
                // 这里写一个通用的 POST 结构：
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", config.BangumiRefreshToken),
                    new KeyValuePair<string, string>("client_id", "YOUR_APP_ID_HERE"), // 用户需要自己去申请并填入代码，或者做到配置里
                    new KeyValuePair<string, string>("client_secret", "YOUR_APP_SECRET_HERE") 
                });

                var response = await client.PostAsync("https://bgm.tv/oauth/access_token", content);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    
                    if (doc.RootElement.TryGetProperty("access_token", out var at))
                    {
                        config.BangumiAccessToken = at.GetString();
                        config.BangumiRefreshToken = doc.RootElement.GetProperty("refresh_token").GetString();
                        config.BangumiTokenExpiry = DateTime.Now.AddSeconds(doc.RootElement.GetProperty("expires_in").GetInt32());
                        Plugin.Instance.SaveConfiguration();
                    }
                }
            }
            catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh Bangumi token");
            }
        }
    }
}
