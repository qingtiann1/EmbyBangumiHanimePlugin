using System.Text.Json.Serialization;

namespace EmbyBangumiHanimePlugin
{
    // Bangumi令牌响应
    public class BangumiTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
        
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
        
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
        
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;
        
        [JsonPropertyName("scope")]
        public string Scope { get; set; } = string.Empty;
    }

    // Hanime登录响应
    public class HanimeLoginResponse
    {
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        
        [JsonPropertyName("session_token")]
        public string SessionToken { get; set; } = string.Empty;
    }

    // 元数据结果
    public class MetadataResult
    {
        [JsonPropertyName("bangumiData")]
        public string BangumiData { get; set; } = string.Empty;
        
        [JsonPropertyName("hanimeData")]
        public string HanimeData { get; set; } = string.Empty;
        
        [JsonPropertyName("success")]
        public bool Success { get; set; }
        
        [JsonPropertyName("error")]
        public string Error { get; set; } = string.Empty;
        
        public MetadataResult()
        {
            Success = true;
        }
    }
}
