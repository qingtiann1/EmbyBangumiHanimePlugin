using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace EmbyBangumiHanimePlugin
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private DateTime _bangumiTokenExpiry;
        private bool _isBangumiAuthenticated;

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            
            // 设置默认User-Agent
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public static Plugin Instance { get; private set; }

        public override string Name => "EmbyBangumiHanimePlugin";
        
        public override string Description => "Anime metadata scraper for Emby with Bangumi and Hanime1 support";
        
        public override Guid Id => Guid.Parse("YOUR-PLUGIN-GUID-HERE"); // 替换为唯一GUID

        // 获取网页配置信息
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "EmbyBangumiHanimePlugin",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.config.html"
                }
            };
        }

        // Bangumi OAuth2认证
        public async Task<bool> AuthenticateBangumiAsync(string authCode, CancellationToken cancellationToken = default)
        {
            try
            {
                var clientId = Configuration.BangumiClientId;
                var clientSecret = Configuration.BangumiClientSecret;
                var redirectUri = "https://bgm.tv/oauth/authorize"; // 或您的回调地址

                // 使用授权码获取访问令牌
                var tokenUrl = "https://bgm.tv/oauth/access_token";
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", authCode),
                    new KeyValuePair<string, string>("client_id", clientId),
                    new KeyValuePair<string, string>("client_secret", clientSecret),
                    new KeyValuePair<string, string>("redirect_uri", redirectUri)
                });

                var response = await _httpClient.PostAsync(tokenUrl, content, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var tokenData = JsonSerializer.Deserialize<BangumiTokenResponse>(responseContent);
                    
                    if (tokenData != null && !string.IsNullOrEmpty(tokenData.AccessToken))
                    {
                        Configuration.BangumiAccessToken = tokenData.AccessToken;
                        Configuration.BangumiRefreshToken = tokenData.RefreshToken;
                        _bangumiTokenExpiry = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
                        
                        // 验证令牌是否有效
                        return await ValidateBangumiTokenAsync(cancellationToken);
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                // 记录日志
                Console.WriteLine($"Bangumi认证失败: {ex.Message}");
                return false;
            }
        }

        // 验证Bangumi令牌
        private async Task<bool> ValidateBangumiTokenAsync(CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(Configuration.BangumiAccessToken))
                return false;

            try
            {
                var url = "https://api.bgm.tv/v0/me";
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Configuration.BangumiAccessToken);
                
                var response = await _httpClient.GetAsync(url, cancellationToken);
                _isBangumiAuthenticated = response.IsSuccessStatusCode;
                return _isBangumiAuthenticated;
            }
            catch
            {
                _isBangumiAuthenticated = false;
                return false;
            }
        }

        // 刷新Bangumi令牌
        public async Task<bool> RefreshBangumiTokenAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(Configuration.BangumiRefreshToken) || 
                string.IsNullOrEmpty(Configuration.BangumiClientId) || 
                string.IsNullOrEmpty(Configuration.BangumiClientSecret))
                return false;

            try
            {
                var tokenUrl = "https://bgm.tv/oauth/access_token";
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token", Configuration.BangumiRefreshToken),
                    new KeyValuePair<string, string>("client_id", Configuration.BangumiClientId),
                    new KeyValuePair<string, string>("client_secret", Configuration.BangumiClientSecret)
                });

                var response = await _httpClient.PostAsync(tokenUrl, content, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var tokenData = JsonSerializer.Deserialize<BangumiTokenResponse>(responseContent);
                    
                    if (tokenData != null && !string.IsNullOrEmpty(tokenData.AccessToken))
                    {
                        Configuration.BangumiAccessToken = tokenData.AccessToken;
                        Configuration.BangumiRefreshToken = tokenData.RefreshToken;
                        _bangumiTokenExpiry = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn);
                        return true;
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"刷新Bangumi令牌失败: {ex.Message}");
                return false;
            }
        }

        // Hanime1认证
        public async Task<bool> AuthenticateHanimeAsync(string username, string password, CancellationToken cancellationToken = default)
        {
            try
            {
                // Hanime1登录URL
                var loginUrl = "https://hanime1.me/login";
                
                // 首先获取登录页面以获取CSRF令牌
                var loginPageResponse = await _httpClient.GetAsync(loginUrl, cancellationToken);
                var loginPageContent = await loginPageResponse.Content.ReadAsStringAsync();
                
                // 提取CSRF令牌（根据实际HTML结构调整）
                var csrfToken = ExtractCsrfToken(loginPageContent);
                
                // 构建登录数据
                var loginData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", username),
                    new KeyValuePair<string, string>("password", password),
                    new KeyValuePair<string, string>("_token", csrfToken)
                });

                // 发送登录请求
                var response = await _httpClient.PostAsync(loginUrl, loginData, cancellationToken);
                
                // 检查是否登录成功
                if (response.IsSuccessStatusCode)
                {
                    // 保存Cookie
                    var cookies = response.Headers.GetValues("Set-Cookie");
                    Configuration.HanimeCookies = string.Join("; ", cookies);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hanime1认证失败: {ex.Message}");
                return false;
            }
        }

        // 使用Cookie认证Hanime1
        public async Task<bool> AuthenticateHanimeWithCookieAsync(string cookie, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(cookie))
                return false;

            try
            {
                Configuration.HanimeCookies = cookie;
                
                // 验证Cookie是否有效
                var testUrl = "https://hanime1.me/account";
                var request = new HttpRequestMessage(HttpMethod.Get, testUrl);
                request.Headers.Add("Cookie", cookie);
                
                var response = await _httpClient.SendAsync(request, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hanime1 Cookie认证失败: {ex.Message}");
                return false;
            }
        }

        // 提取CSRF令牌
        private string ExtractCsrfToken(string htmlContent)
        {
            var pattern = @"name=""_token"" value=""([^""]+)""";
            var match = Regex.Match(htmlContent, pattern);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        // 获取Bangumi元数据
        public async Task<string> GetBangumiMetadataAsync(string query, CancellationToken cancellationToken = default)
        {
            if (!_isBangumiAuthenticated && DateTime.UtcNow >= _bangumiTokenExpiry.AddDays(-1))
            {
                await RefreshBangumiTokenAsync(cancellationToken);
            }

            if (!_isBangumiAuthenticated)
                return "未认证，无法获取Bangumi数据";

            try
            {
                var url = $"https://api.bgm.tv/search/subject/{Uri.EscapeDataString(query)}?type=2"; // type=2为动画
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Configuration.BangumiAccessToken);
                
                var response = await _httpClient.GetAsync(url, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                
                return "获取Bangumi数据失败";
            }
            catch (Exception ex)
            {
                return $"获取Bangumi数据时出错: {ex.Message}";
            }
        }

        // 获取Hanime元数据
        public async Task<string> GetHanimeMetadataAsync(string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(Configuration.HanimeCookies))
                return "未认证，无法获取Hanime数据";

            try
            {
                var url = $"https://hanime1.me/search?query={Uri.EscapeDataString(query)}";
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Cookie", Configuration.HanimeCookies);
                
                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                
                return "获取Hanime数据失败";
            }
            catch (Exception ex)
            {
                return $"获取Hanime数据时出错: {ex.Message}";
            }
        }

        // 自动刮削元数据
        public async Task<MetadataResult> ScrapeMetadataAsync(string title, bool useBangumi = true, bool useHanime = true, CancellationToken cancellationToken = default)
        {
            var result = new MetadataResult();
            
            if (useBangumi && _isBangumiAuthenticated)
            {
                result.BangumiData = await GetBangumiMetadataAsync(title, cancellationToken);
            }
            
            if (useHanime && !string.IsNullOrEmpty(Configuration.HanimeCookies))
            {
                result.HanimeData = await GetHanimeMetadataAsync(title, cancellationToken);
            }
            
            return result;
        }
    }

    // 辅助类
    public class BangumiTokenResponse
    {
        public string AccessToken { get; set; }
        public string TokenType { get; set; }
        public int ExpiresIn { get; set; }
        public string RefreshToken { get; set; }
        public string Scope { get; set; }
    }

    public class MetadataResult
    {
        public string BangumiData { get; set; }
        public string HanimeData { get; set; }
    }
}
