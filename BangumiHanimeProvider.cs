using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Net; // 必须引用这个以使用 HttpResponseInfo
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EmbyBangumiHanimePlugin
{
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

        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>
            {
                Item = new Series()
            };

            var config = Plugin.Instance.Configuration;
            
            if (info.ProviderIds.TryGetValue("Bangumi", out var bgmId))
            {
                await FetchBangumiMetadata(bgmId, result.Item, config, cancellationToken);
                result.HasMetadata = true;
            }
            else if (!string.IsNullOrEmpty(info.Name)) 
            {
                await FetchHanimeMetadata(info.Name, result.Item, config, cancellationToken);
                result.HasMetadata = true;
            }

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            var list = new List<RemoteSearchResult>();
            if (string.IsNullOrWhiteSpace(searchInfo.Name)) return list;

            try
            {
                var query = System.Web.HttpUtility.UrlEncode(searchInfo.Name);
                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "EmbyBangumiPlugin/1.0");
                
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
                                ProductionYear = null,
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

        // 修复：返回类型必须是 Task<HttpResponseInfo> 而不是 HttpResponseMessage
        public async Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var client = _httpClientFactory.CreateClient();
            // 这里可以添加 Hanime 的 Cookie，如果下载图片需要的话
            var response = await client.GetAsync(url, cancellationToken);
            
            return new HttpResponseInfo
            {
                Content = await response.Content.ReadAsStreamAsync(cancellationToken),
                ContentType = response.Content.Headers.ContentType?.ToString(),
                StatusCode = response.StatusCode
            };
        }

        private async Task FetchBangumiMetadata(string subjectId, Series series, PluginConfiguration config, CancellationToken ct)
        {
            try
            {
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
                
                series.Overview = root.TryGetProperty("summary", out var sum) ? sum.GetString() : "";
                
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

        private async Task FetchHanimeMetadata(string query, Series series, PluginConfiguration config, CancellationToken ct)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                
                if (!string.IsNullOrEmpty(config.HanimeCookie))
                {
                    client.DefaultRequestHeaders.Add("Cookie", config.HanimeCookie);
                }

                var searchUrl = $"https://hanime1.me/search?query={System.Web.HttpUtility.UrlEncode(query)}";
                var html = await client.GetStringAsync(searchUrl, ct);

                var match = Regex.Match(html, "href=\"(https://hanime1\\.me/watch\\?v=[^\"]+)\"");
                
                if (match.Success)
                {
                    var videoUrl = match.Groups[1].Value;
                    var videoHtml = await client.GetStringAsync(videoUrl, ct);

                    var titleMatch = Regex.Match(videoHtml, "<meta property=\"og:title\" content=\"([^\"]+)\"");
                    if (titleMatch.Success) series.Name = titleMatch.Groups[1].Value;

                    var descMatch = Regex.Match(videoHtml, "<meta property=\"og:description\" content=\"([^\"]+)\"");
                    if (descMatch.Success) series.Overview = descMatch.Groups[1].Value;

                    var imgMatch = Regex.Match(videoHtml, "<meta property=\"og:image\" content=\"([^\"]+)\"");
                    if (imgMatch.Success) series.PrimaryImagePath = imgMatch.Groups[1].Value;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error scraping Hanime");
            }
        }

        private async Task CheckAndRefreshToken(PluginConfiguration config)
        {
            if (string.IsNullOrEmpty(config.BangumiRefreshToken)) return;
            // 简单实现，如果有效期不存在，假设未过期或不处理
            if (config.BangumiTokenExpiry != DateTime.MinValue && DateTime.Now < config.BangumiTokenExpiry.AddDays(-3)) return; 

            try
            {
                using var client = _httpClientFactory.CreateClient();
                // 占位符，需填入实际 APP ID
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", config.BangumiRefreshToken),
                    new KeyValuePair<string, string>("client_id", "YOUR_APP_ID"), 
                    new KeyValuePair<string, string>("client_secret", "YOUR_APP_SECRET") 
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
