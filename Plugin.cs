using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Plugins;
using System;
using System.Collections.Generic;

namespace EmbyBangumiHanimePlugin
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public override string Name => "Bangumi & Hanime Scraper";
        public override Guid Id => new Guid("a1b2c3d4-e5f6-7890-a1b2-c3d4e5f67890"); // 随机生成的唯一GUID
        public override string Description => "从 Bangumi 和 Hanime1 获取番剧元数据";

        public static Plugin Instance { get; private set; }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "BangumiHanimeConfig",
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.config.html"
                }
            };
        }
    }
}
