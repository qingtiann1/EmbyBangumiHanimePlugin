using System.Text.Json.Serialization;

namespace EmbyBangumiHanimePlugin
{
    public class PluginConfiguration
    {
        // Bangumi OAuth2配置
        [JsonPropertyName("bangumiClientId")]
        public string BangumiClientId { get; set; } = string.Empty;
        
        [JsonPropertyName("bangumiClientSecret")]
        public string BangumiClientSecret { get; set; } = string.Empty;
        
        [JsonPropertyName("bangumiAccessToken")]
        public string BangumiAccessToken { get; set; } = string.Empty;
        
        [JsonPropertyName("bangumiRefreshToken")]
        public string BangumiRefreshToken { get; set; } = string.Empty;
        
        [JsonPropertyName("bangumiTokenExpiry")]
        public long BangumiTokenExpiry { get; set; }
        
        // Hanime配置
        [JsonPropertyName("hanimeUsername")]
        public string HanimeUsername { get; set; } = string.Empty;
        
        [JsonPropertyName("hanimeCookie")]
        public string HanimeCookie { get; set; } = string.Empty;
        
        [JsonPropertyName("hanimeSession")]
        public string HanimeSession { get; set; } = string.Empty;
        
        // 刮削设置
        [JsonPropertyName("enableBangumi")]
        public bool EnableBangumi { get; set; } = true;
        
        [JsonPropertyName("enableHanime")]
        public bool EnableHanime { get; set; } = true;
        
        [JsonPropertyName("autoScrape")]
        public bool AutoScrape { get; set; } = true;
        
        [JsonPropertyName("searchTimeout")]
        public int SearchTimeout { get; set; } = 30;
    }
}
