using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace EmbyBangumiHanimePlugin.Services
{
    public class MetadataService
    {
        private readonly Plugin _plugin;
        
        public MetadataService(Plugin plugin)
        {
            _plugin = plugin;
        }
        
        // 自动刮削元数据
        public async Task<Dictionary<string, object>> ScrapeMetadata(string mediaTitle, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<string, object>();
            
            try
            {
                // 从Bangumi获取数据
                var bangumiData = await _plugin.GetBangumiMetadataAsync(mediaTitle, cancellationToken);
                if (!string.IsNullOrEmpty(bangumiData) && !bangumiData.Contains("未认证"))
                {
                    var bangumiInfo = ParseBangumiResponse(bangumiData);
                    if (bangumiInfo != null)
                    {
                        result["Bangumi"] = bangumiInfo;
                    }
                }
                
                // 从Hanime获取数据
                var hanimeData = await _plugin.GetHanimeMetadataAsync(mediaTitle, cancellationToken);
                if (!string.IsNullOrEmpty(hanimeData) && !hanimeData.Contains("未认证"))
                {
                    var hanimeInfo = ParseHanimeResponse(hanimeData);
                    if (hanimeInfo != null)
                    {
                        result["Hanime"] = hanimeInfo;
                    }
                }
            }
            catch (Exception ex)
            {
                result["Error"] = ex.Message;
            }
            
            return result;
        }
        
        private Dictionary<string, object> ParseBangumiResponse(string jsonResponse)
        {
            try
            {
                using var doc = JsonDocument.Parse(jsonResponse);
                var root = doc.RootElement;
                
                // 简单解析Bangumi返回的数据
                var result = new Dictionary<string, object>();
                
                if (root.TryGetProperty("list", out var list) && list.GetArrayLength() > 0)
                {
                    var firstItem = list[0];
                    
                    if (firstItem.TryGetProperty("name", out var name))
                        result["Title"] = name.GetString();
                    
                    if (firstItem.TryGetProperty("name_cn", out var nameCn))
                        result["ChineseTitle"] = nameCn.GetString();
                    
                    if (firstItem.TryGetProperty("summary", out var summary))
                        result["Summary"] = summary.GetString();
                    
                    if (firstItem.TryGetProperty("images", out var images))
                    {
                        if (images.TryGetProperty("large", out var largeImage))
                            result["Image"] = largeImage.GetString();
                    }
                    
                    if (firstItem.TryGetProperty("rating", out var rating))
                    {
                        if (rating.TryGetProperty("score", out var score))
                            result["Rating"] = score.GetDouble();
                    }
                }
                
                return result;
            }
            catch
            {
                return null;
            }
        }
        
        private Dictionary<string, object> ParseHanimeResponse(string htmlResponse)
        {
            // 简单的HTML解析，实际使用时可能需要更复杂的解析逻辑
            var result = new Dictionary<string, object>();
            
            // 这里添加HTML解析逻辑
            // 由于Hanime没有公开API，需要解析HTML页面
            
            return result;
        }
    }
}
