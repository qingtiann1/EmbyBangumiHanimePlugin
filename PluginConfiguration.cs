using MediaBrowser.Model.Plugins;

namespace EmbyBangumiHanimePlugin
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        // Bangumi OAuth2配置
        public string BangumiClientId { get; set; } = string.Empty;
        public string BangumiClientSecret { get; set; } = string.Empty;
        public string BangumiAccessToken { get; set; } = string.Empty;
        public string BangumiRefreshToken { get; set; } = string.Empty;
        public bool IsBangumiAuthenticated { get; set; } = false;
        
        // Hanime配置
        public string HanimeUsername { get; set; } = string.Empty;
        public string HanimePassword { get; set; } = string.Empty;
        public string HanimeCookies { get; set; } = string.Empty;
        
        // 刮削设置
        public bool EnableBangumi { get; set; } = true;
        public bool EnableHanime { get; set; } = true;
        public bool AutoScrape { get; set; } = true;
        public int SearchTimeout { get; set; } = 30;
        
        // 令牌过期时间（用于检查）
        public long BangumiTokenExpiryTicks { get; set; }
    }
}
