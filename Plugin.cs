using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
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
        public string BangumiApiKey { get; set; } = "";
        public string HanimeUsername { get; set; } = "";
        public string HanimePassword { get; set; } = "";
        public string HanimeCookie { get; set; } = "";
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
            _config = plugin?.Configuration ?? new PluginConfiguration();
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Movie>();
            
            if (string.IsNullOrEmpty(_config.BangumiApiKey))
                return result;

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Emby-Bangumi-Hanime-Plugin");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            try
            {
                // Search for anime on Bangumi
                var searchUrl = $"https://api.bgm.tv/v0/search/subjects/{Uri.EscapeDataString(info.Name)}?type=2&limit=1";
                var response = await httpClient.GetStringAsync(searchUrl, cancellationToken);
                
                using var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("data", out var data) && data.GetArrayLength() > 0)
                {
                    var firstItem = data[0];
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
                    };

                    // Extract year from date
                    if (detail.TryGetProperty("date", out var dateElement))
                    {
                        var dateStr = dateElement.GetString();
                        if (!string.IsNullOrEmpty(dateStr) && dateStr.Length >= 4)
                        {
                            if (int.TryParse(dateStr.Substring(0, 4), out var year))
                            {
                                result.Item.ProductionYear = year;
                            }
                        }
                    }

                    // Add genres
                    if (detail.TryGetProperty("tags", out var tags))
                    {
                        result.Item.Genres = tags.EnumerateArray()
                            .Take(5)
                            .Select(t => t.GetProperty("name").GetString())
                            .Where(name => !string.IsNullOrEmpty(name))
                            .ToArray();
                    }

                    // Add rating
                    if (detail.TryGetProperty("rating", out var rating))
                    {
                        if (rating.TryGetProperty("score", out var score))
                        {
                            result.Item.CommunityRating = (float)score.GetDouble();
                        }
                    }

                    // Add poster
                    if (detail.TryGetProperty("images", out var images))
                    {
                        if (images.TryGetProperty("large", out var largeImage))
                        {
                            result.Item.SetImage(new ImageInfo
                            {
                                Type = ImageType.Primary,
                                Url = largeImage.GetString()
                            }, 0);
                            
                            result.Item.SetImage(new ImageInfo
                            {
                                Type = ImageType.Backdrop,
                                Url = largeImage.GetString()
                            }, 0);
                        }
                    }

                    result.HasMetadata = true;
                }
            }
            catch (Exception ex)
            {
                // Log exception in real implementation
                Console.WriteLine($"Bangumi provider error: {ex.Message}");
            }

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();

            if (string.IsNullOrEmpty(_config.BangumiApiKey))
                return results;

            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Emby-Bangumi-Hanime-Plugin");
            httpClient.DefaultRequestHeaders.Add("Accept", "application/json");

            try
            {
                var searchUrl = $"https://api.bgm.tv/v0/search/subjects/{Uri.EscapeDataString(searchInfo.Name)}?type=2&limit=10";
                var response = await httpClient.GetStringAsync(searchUrl, cancellationToken);
                
                using var doc = JsonDocument.Parse(response);
                if (doc.RootElement.TryGetProperty("data", out var data))
                {
                    foreach (var item in data.EnumerateArray())
                    {
                        var result = new RemoteSearchResult
                        {
                            Name = item.GetProperty("name").GetString() ?? item.GetProperty("name_cn").GetString() ?? "Unknown",
                            SearchProviderName = Name,
                        };

                        // Set year if available
                        if (item.TryGetProperty("date", out var dateElement))
                        {
                            var dateStr = dateElement.GetString();
                            if (!string.IsNullOrEmpty(dateStr) && dateStr.Length >= 4)
                            {
                                if (DateTime.TryParse(dateStr, out var date))
                                {
                                    result.ProductionYear = date.Year;
                                }
                            }
                        }

                        // Add image if available
                        if (item.TryGetProperty("images", out var images))
                        {
                            if (images.TryGetProperty("large", out var largeImage))
                            {
                                result.ImageUrl = largeImage.GetString();
                            }
                        }

                        // Set provider ID
                        if (item.TryGetProperty("id", out var idElement))
                        {
                            result.SetProviderId("Bangumi", idElement.GetInt32().ToString());
                        }

                        results.Add(result);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log exception in real implementation
                Console.WriteLine($"Bangumi search error: {ex.Message}");
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
            _config = plugin?.Configuration ?? new PluginConfiguration();
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Movie>();
            
            // Note: Hanime scraping requires handling of anti-bot measures
            // This is a simplified implementation
            if (string.IsNullOrEmpty(_config.HanimeCookie) && 
                (string.IsNullOrEmpty(_config.HanimeUsername) || string.IsNullOrEmpty(_config.HanimePassword)))
                return result;

            using var httpClient = _httpClientFactory.CreateClient();
            
            // Set headers to mimic browser
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            
            if (!string.IsNullOrEmpty(_config.HanimeCookie))
            {
                httpClient.DefaultRequestHeaders.Add("Cookie", _config.HanimeCookie);
            }

            try
            {
                // Note: Actual implementation would need to handle Hanime's specific HTML structure
                // This is a placeholder showing the approach
                var searchUrl = $"https://hanime1.me/search?query={Uri.EscapeDataString(info.Name)}";
                var response = await httpClient.GetStringAsync(searchUrl, cancellationToken);

                // In a real implementation, you would parse Hanime's HTML response
                // to extract anime information
                
                // Placeholder result
                result.HasMetadata = false;
            }
            catch (Exception ex)
            {
                // Log exception in real implementation
                Console.WriteLine($"Hanime provider error: {ex.Message}");
            }

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();

            // Note: Hanime scraping requires handling of anti-bot measures
            if (string.IsNullOrEmpty(_config.HanimeCookie) && 
                (string.IsNullOrEmpty(_config.HanimeUsername) || string.IsNullOrEmpty(_config.HanimePassword)))
                return results;

            using var httpClient = _httpClientFactory.CreateClient();
            
            // Set headers to mimic browser
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            
            if (!string.IsNullOrEmpty(_config.HanimeCookie))
            {
                httpClient.DefaultRequestHeaders.Add("Cookie", _config.HanimeCookie);
            }

            try
            {
                // Actual implementation would parse Hanime search results
                // Placeholder for now
            }
            catch (Exception ex)
            {
                // Log exception in real implementation
                Console.WriteLine($"Hanime search error: {ex.Message}");
            }

            return results;
        }
    }
}
