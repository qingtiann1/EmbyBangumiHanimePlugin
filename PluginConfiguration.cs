using MediaBrowser.Model.Plugins;
using System;

namespace EmbyBangumiHanimePlugin
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string BangumiAccessToken { get; set; }
        public string BangumiRefreshToken { get; set; }
        public DateTime BangumiTokenExpiry { get; set; }
        public string HanimeCookie { get; set; }

        public PluginConfiguration()
        {
            BangumiAccessToken = string.Empty;
            BangumiRefreshToken = string.Empty;
            HanimeCookie = string.Empty;
            BangumiTokenExpiry = DateTime.MinValue;
        }
    }
}
