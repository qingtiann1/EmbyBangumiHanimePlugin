using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace EmbyBangumiHanimePlugin
{
    public class Plugin
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        private DateTime _bangumiTokenExpiry;
        private bool _isBangumiAuthenticated;
        
        public PluginConfiguration Configuration { get; private set; }
        
        public Plugin()
        {
            Instance = this;
            Configuration = new PluginConfiguration();
            
            // 设置默认User-Agent
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            
            // 尝试加载配置
            LoadConfiguration();
        }

        public static Plugin Instance { get; private set; }

        public string Name => "EmbyBangumiHanimePlugin";
        
        public string Description => "Anime metadata scraper for Emby with Bangumi and Hanime1 support";
        
        // 加载配置
        private void LoadConfiguration()
        {
            try
            {
                var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bangumi-hanime-config.json");
                if (File.Exists(configFile))
                {
                    var json = File.ReadAllText(configFile);
                    Configuration = JsonSerializer.Deserialize<PluginConfiguration>(json) ?? new PluginConfiguration();
                    
                    // 检查令牌是否即将过期（小于30天）
                    if (Configuration.BangumiTokenExpiryTicks > 0)
                    {
                        var expiry = new DateTime(Configuration.BangumiTokenExpiryTicks);
                        if (DateTime.UtcNow >= expiry.AddDays(-30))
                        {
                            // 令牌即将过期，需要重新授权
                            _isBangumiAuthenticated = false;
                        }
                        else
                        {
                            _isBangumiAuthenticated = !string.IsNullOrEmpty(Configuration.BangumiAccessToken);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载配置失败: {ex.Message}");
                Configuration = new PluginConfiguration();
            }
        }

        // 保存配置
        private void SaveConfiguration()
        {
            try
            {
                var configFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bangumi-hanime-config.json");
                var json = JsonSerializer.Serialize(Configuration, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(configFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存配置失败: {ex.Message}");
            }
        }

        // Bangumi OAuth2认证
        public async Task<bool> AuthenticateBangumiAsync(string authCode, CancellationToken cancellationToken = default)
        {
            try
            {
                var clientId = Configuration.BangumiClientId;
                var clientSecret = Configuration.BangumiClientSecret;
                var redirectUri = "urn:ietf:wg:oauth:2.0:oob"; // 使用 OOB 方式

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
                        Configuration.BangumiTokenExpiryTicks = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn).Ticks;
                        
                        SaveConfiguration();
                        
                        // 验证令牌是否有效
                        return await ValidateBangumiTokenAsync(cancellationToken);
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
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
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {Configuration.BangumiAccessToken}");
                request.Headers.Add("User-Agent", "EmbyBangumiHanimePlugin/1.0.0");
                
                var response = await _httpClient.SendAsync(request, cancellationToken);
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
                        Configuration.BangumiTokenExpiryTicks = DateTime.UtcNow.AddSeconds(tokenData.ExpiresIn).Ticks;
                        
                        SaveConfiguration();
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
                // 注意：hanime1.me 网站可能需要特殊处理，这里提供基本框架
                var loginUrl = "https://members.hanime.tv/rapi/v7/login";
                
                // 构建登录数据
                var loginData = new
                {
                    email = username,
                    password = password,
                    session_token = Guid.NewGuid().ToString()
                };
                
                var json = JsonSerializer.Serialize(loginData);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                // 添加必要的请求头
                var request = new HttpRequestMessage(HttpMethod.Post, loginUrl)
                {
                    Content = content
                };
                
                request.Headers.Add("X-Signature-Version", "web2");
                request.Headers.Add("X-Signature", "d7c8b8c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0");
                
                // 发送登录请求
                var response = await _httpClient.SendAsync(request, cancellationToken);
                
                // 检查是否登录成功
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonSerializer.Deserialize<HanimeLoginResponse>(responseContent);
                    
                    if (loginResponse != null && loginResponse.Success)
                    {
                        // 保存会话信息
                        Configuration.HanimeSession = responseContent;
                        Configuration.HanimeUsername = username;
                        
                        SaveConfiguration();
                        return true;
                    }
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
                Configuration.HanimeCookie = cookie;
                SaveConfiguration();
                
                // 验证Cookie是否有效
                var testUrl = "https://hanime.tv/api/v8/channel";
                var request = new HttpRequestMessage(HttpMethod.Get, testUrl);
                request.Headers.Add("Cookie", cookie);
                request.Headers.Add("X-Signature-Version", "web2");
                request.Headers.Add("X-Signature", "d7c8b8c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0c0");
                
                var response = await _httpClient.SendAsync(request, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hanime1 Cookie认证失败: {ex.Message}");
                return false;
            }
        }

        // 获取Bangumi元数据
        public async Task<string> GetBangumiMetadataAsync(string query, CancellationToken cancellationToken = default)
        {
            // 检查令牌是否即将过期
            if (Configuration.BangumiTokenExpiryTicks > 0 && 
                new DateTime(Configuration.BangumiTokenExpiryTicks) <= DateTime.UtcNow.AddDays(1))
            {
                await RefreshBangumiTokenAsync(cancellationToken);
            }

            if (!_isBangumiAuthenticated)
                return "{\"error\": \"未认证，无法获取Bangumi数据\"}";

            try
            {
                var url = $"https://api.bgm.tv/search/subject/{Uri.EscapeDataString(query)}?type=2"; // type=2为动画
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Authorization", $"Bearer {Configuration.BangumiAccessToken}");
                request.Headers.Add("User-Agent", "EmbyBangumiHanimePlugin/1.0.0");
                
                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                
                return "{\"error\": \"获取Bangumi数据失败\"}";
            }
            catch (Exception ex)
            {
                return $"{{\"error\": \"获取Bangumi数据时出错: {ex.Message.Replace("\"", "\\\"")}\"}}";
            }
        }

        // 获取Hanime元数据
        public async Task<string> GetHanimeMetadataAsync(string query, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(Configuration.HanimeCookie))
                return "{\"error\": \"未认证，无法获取Hanime数据\"}";

            try
            {
                // 搜索Hanime内容
                var searchUrl = $"https://search.htv-services.com/";
                
                var searchData = new
                {
                    search_text = query,
                    tags = new string[0],
                    tags_mode = "AND",
                    brands = new string[0],
                    blacklist = new string[0],
                    order_by = "created_at_unix",
                    ordering = "desc",
                    page = 0
                };
                
                var json = JsonSerializer.Serialize(searchData);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
                
                var request = new HttpRequestMessage(HttpMethod.Post, searchUrl)
                {
                    Content = content
                };
                
                // 添加必要的请求头
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                request.Headers.Add("Referer", "https://hanime.tv/");
                
                var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                
                return "{\"error\": \"获取Hanime数据失败\"}";
            }
            catch (Exception ex)
            {
                return $"{{\"error\": \"获取Hanime数据时出错: {ex.Message.Replace("\"", "\\\"")}\"}}";
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
            
            if (useHanime && !string.IsNullOrEmpty(Configuration.HanimeCookie))
            {
                result.HanimeData = await GetHanimeMetadataAsync(title, cancellationToken);
            }
            
            return result;
        }

        // 获取授权URL
        public string GetBangumiAuthUrl()
        {
            if (string.IsNullOrEmpty(Configuration.BangumiClientId))
                return string.Empty;
                
            var redirectUri = "urn:ietf:wg:oauth:2.0:oob";
            return $"https://bgm.tv/oauth/authorize?client_id={Configuration.BangumiClientId}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}";
        }
    }

    // 配置类
    public class PluginConfiguration
    {
        // Bangumi OAuth2配置
        public string BangumiClientId { get; set; } = string.Empty;
        public string BangumiClientSecret { get; set; } = string.Empty;
        public string BangumiAccessToken { get; set; } = string.Empty;
        public string BangumiRefreshToken { get; set; } = string.Empty;
        public long BangumiTokenExpiryTicks { get; set; }
        
        // Hanime配置
        public string HanimeUsername { get; set; } = string.Empty;
        public string HanimeCookie { get; set; } = string.Empty;
        public string HanimeSession { get; set; } = string.Empty;
        
        // 刮削设置
        public bool EnableBangumi { get; set; } = true;
        public bool EnableHanime { get; set; } = true;
        public bool AutoScrape { get; set; } = true;
        public int SearchTimeout { get; set; } = 30;
    }

    // 辅助类
    public class BangumiTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }
        
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }
        
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
        
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }
        
        [JsonPropertyName("scope")]
        public string Scope { get; set; }
    }

    public class HanimeLoginResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        
        [JsonPropertyName("session_token")]
        public string SessionToken { get; set; }
    }

    public class MetadataResult
    {
        public string BangumiData { get; set; }
        public string HanimeData { get; set; }
    }
}
