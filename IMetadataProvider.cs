using System.Threading;
using System.Threading.Tasks;

namespace EmbyBangumiHanimePlugin
{
    public interface IMetadataProvider
    {
        Task<MetadataResult> GetMetadataAsync(string title, CancellationToken cancellationToken);
    }
}
