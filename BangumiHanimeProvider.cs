using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
// 重要：核心引用
using MediaBrowser.Model.Net; 

namespace EmbyBangumiHanimePlugin
{
    public class BangumiHanimeProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<BangumiHanimeProvider> _logger;

        public BangumiHanimeProvider(IHttpClientFactory httpClientFactory, ILogger<BangumiHanimeProvider> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        public string Name => "Bangumi & Hanime";
        public int Order => 0;

        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series> { Item = new Series() };
            var config = Plugin.Instance.Configuration;
            
            if (info.ProviderIds.TryGetValue("Bangumi", out var bgmId))
            {
                await FetchBangumiMetadata(bgmId, result.Item, config, cancellationToken);
                result.HasMetadata = true;
            }
            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            var list = new List<RemoteSearchResult>();
            // 逻辑实现...
            return list;
        }

        // 核心修复点：明确指定返回类型所属的命名空间
        public async Task<MediaBrowser.Model.Net.HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            using var client = _httpClientFactory.CreateClient();
            var response = await client.GetAsync(url, cancellationToken).ConfigureAwait(false);
            
            return new MediaBrowser.Model.Net.HttpResponseInfo
            {
                Content = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                ContentType = response.Content.Headers.ContentType?.ToString(),
                StatusCode = response.StatusCode
            };
        }

        private async Task FetchBangumiMetadata(string id, Series series, PluginConfiguration config, CancellationToken ct) { /* 实现 */ }
    }
}
