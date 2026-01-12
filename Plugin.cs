using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace EmbyBangumiHanimePlugin
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public override string Name => "Bangumi Hanime Scraper";
        public override Guid Id => Guid.Parse("E1F2A3B4-C5D6-7890-ABCD-EF1234567890");

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
        }

        public IEnumerable<PluginPageInfo> GetWebPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = "bangumi-hanime-config",
                    DisplayName = "Bangumi Hanime Config",
                    ResourcePath = GetType().Namespace + ".Configuration.config.html",
                    IsMainConfigPage = true
                }
            };
        }
    }

    public class PluginConfiguration : BasePluginConfiguration
    {
        public string BangumiApiKey { get; set; }
        public string HanimeUsername { get; set; }
        public string HanimePassword { get; set; }
        public string HanimeCookie { get; set; }
    }

    public class BangumiProvider : IRemoteMetadataProvider<Movie, MovieInfo>
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly PluginConfiguration _config;

        public string Name => "Bangumi";

        public BangumiProvider(IHttpClientFactory httpClientFactory, IPluginManager pluginManager)
        {
            _httpClientFactory = httpClientFactory;
            var plugin = pluginManager.GetPlugins().FirstOrDefault(p => p is Plugin) as Plugin;
            _config = plugin?.Configuration;
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Movie>();
            
            if (string.IsNullOrEmpty(_config?.BangumiApiKey))
                return result;

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Emby-Bangumi-Hanime-Plugin");

            try
            {
                // Search for anime on Bangumi
                var searchUrl = $"https://api.bgm.tv/search/subject/{Uri.EscapeDataString(info.Name)}?responseGroup=large&type=2";
                var response = await httpClient.GetStringAsync(searchUrl, cancellationToken);
                
                using var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("list", out var list) && list.GetArrayLength() > 0)
                {
                    var firstItem = list[0];
                    var subjectId = firstItem.GetProperty("id").GetInt32();

                    // Get detailed info
                    var detailUrl = $"https://api.bgm.tv/v0/subjects/{subjectId}";
                    var detailResponse = await httpClient.GetStringAsync(detailUrl, cancellationToken);
                    
                    using var detailDoc = JsonDocument.Parse(detailResponse);
                    var detail = detailDoc.RootElement;
                    
                    result.Item = new Movie
                    {
                        Name = detail.GetProperty("name").GetString(),
                        OriginalTitle = detail.GetProperty("name_cn").GetString(),
                        Overview = detail.GetProperty("summary").GetString(),
                        ProductionYear = detail.GetProperty("date").GetString()?.Split('-')[0],
                    };

                    // Add genres
                    if (detail.TryGetProperty("tags", out var tags))
                    {
                        result.Item.Genres = tags.EnumerateArray()
                            .Take(5)
                            .Select(t => t.GetProperty("name").GetString())
                            .ToArray();
                    }

                    // Add rating
                    if (detail.TryGetProperty("rating", out var rating))
                    {
                        if (rating.TryGetProperty("score", out var score))
                        {
                            result.Item.CommunityRating = score.GetDouble();
                        }
                    }

                    result.HasMetadata = true;
                }
            }
            catch (Exception ex)
            {
                // Log exception
            }

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();

            if (string.IsNullOrEmpty(_config?.BangumiApiKey))
                return results;

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Emby-Bangumi-Hanime-Plugin");

            try
            {
                var searchUrl = $"https://api.bgm.tv/search/subject/{Uri.EscapeDataString(searchInfo.Name)}?responseGroup=large&type=2";
                var response = await httpClient.GetStringAsync(searchUrl, cancellationToken);
                
                using var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("list", out var list))
                {
                    foreach (var item in list.EnumerateArray())
                    {
                        var result = new RemoteSearchResult
                        {
                            Name = item.GetProperty("name").GetString(),
                            SearchProviderName = Name,
                            PremiereDate = !item.GetProperty("date").GetString().Equals("0000-00-00") 
                                ? DateTime.TryParse(item.GetProperty("date").GetString(), out var date) ? date : null
                                : null,
                        };

                        if (item.TryGetProperty("images", out var images))
                        {
                            if (images.TryGetProperty("large", out var largeImage))
                            {
                                result.ImageUrl = largeImage.GetString();
                            }
                        }

                        result.SetProviderId("Bangumi", item.GetProperty("id").GetInt32().ToString());
                        results.Add(result);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception
            }

            return results;
        }
    }

    public class HanimeProvider : IRemoteMetadataProvider<Movie, MovieInfo>
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly PluginConfiguration _config;

        public string Name => "Hanime";

        public HanimeProvider(IHttpClientFactory httpClientFactory, IPluginManager pluginManager)
        {
            _httpClientFactory = httpClientFactory;
            var plugin = pluginManager.GetPlugins().FirstOrDefault(p => p is Plugin) as Plugin;
            _config = plugin?.Configuration;
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Movie>();
            
            if (string.IsNullOrEmpty(_config?.HanimeCookie) && (string.IsNullOrEmpty(_config?.HanimeUsername) || string.IsNullOrEmpty(_config?.HanimePassword)))
                return result;

            using var httpClient = _httpClientFactory.CreateClient();
            if (!string.IsNullOrEmpty(_config.HanimeCookie))
            {
                httpClient.DefaultRequestHeaders.Add("Cookie", _config.HanimeCookie);
            }

            try
            {
                var searchUrl = $"https://hanime1.me/search?query={Uri.EscapeDataString(info.Name)}";
                var response = await httpClient.GetStringAsync(searchUrl, cancellationToken);

                // This would require more complex parsing depending on Hanime's actual HTML structure
                // For demonstration purposes only
                result.HasMetadata = false; // Placeholder
            }
            catch (Exception ex)
            {
                // Log exception
            }

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();

            if (string.IsNullOrEmpty(_config?.HanimeCookie) && (string.IsNullOrEmpty(_config?.HanimeUsername) || string.IsNullOrEmpty(_config?.HanimePassword)))
                return results;

            using var httpClient = _httpClientFactory.CreateClient();
            if (!string.IsNullOrEmpty(_config.HanimeCookie))
            {
                httpClient.DefaultRequestHeaders.Add("Cookie", _config.HanimeCookie);
            }

            try
            {
                var searchUrl = $"https://hanime1.me/search?query={Uri.EscapeDataString(searchInfo.Name)}";
                var response = await httpClient.GetStringAsync(searchUrl, cancellationToken);

                // Parse search results - implementation depends on Hanime's HTML structure
                // This is a placeholder implementation
            }
            catch (Exception ex)
            {
                // Log exception
            }

            return results;
        }
    }
}
