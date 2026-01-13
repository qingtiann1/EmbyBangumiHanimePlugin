using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace EmbyBangumiHanimePlugin
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        private readonly IHttpClientFactory _httpClientFactory;
        
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, IHttpClientFactory httpClientFactory) 
            : base(applicationPaths, xmlSerializer)
        {
            _httpClientFactory = httpClientFactory;
            Instance = this;
        }

        public override string Name => "EmbyBangumiHanimePlugin";
        
        public override string Description => "Anime metadata scraper for Emby";
        
        public static Plugin Instance { get; private set; }

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
    }

    public class PluginConfiguration : BasePluginConfiguration
    {
        public string BangumiClientId { get; set; }
        public string BangumiClientSecret { get; set; }
        public string BangumiRefreshToken { get; set; }
        public long? TokenExpiryTime { get; set; }
        public string HanimeLoginMethod { get; set; } = "credentials";
        public string HanimeUsername { get; set; }
        public string HanimePassword { get; set; }
        public string HanimeCookie { get; set; }
    }
}
