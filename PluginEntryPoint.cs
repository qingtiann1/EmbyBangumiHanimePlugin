using System;
using System.Reflection;

namespace EmbyBangumiHanimePlugin
{
    public static class PluginEntryPoint
    {
        public static IMetadataProvider CreatePlugin()
        {
            return new Plugin();
        }
        
        public static string GetConfigurationPage()
        {
            // 读取嵌入的HTML配置页面
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "EmbyBangumiHanimePlugin.Configuration.config.html";
            
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream != null)
                {
                    using (var reader = new System.IO.StreamReader(stream))
                    {
                        return reader.ReadToEnd();
                    }
                }
            }
            
            return "<html><body><h1>Configuration page not found</h1></body></html>";
        }
    }
}
