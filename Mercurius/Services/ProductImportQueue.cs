using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Mercurius.Services
{
    public class ProductImportJob
    {
        public string JobId { get; set; } = "";
        public byte[] FileContent { get; set; } = System.Array.Empty<byte>();
        public bool UpdateExisting { get; set; }
    }

    /// <summary>
    /// Hands CSV import work from the request thread to the background service that actually
    /// runs it, so the upload request can return immediately instead of blocking for however
    /// long a large file takes to process.
    /// </summary>
    public class ProductImportQueue
    {
        private readonly Channel<ProductImportJob> _channel = Channel.CreateUnbounded<ProductImportJob>();

        public ValueTask EnqueueAsync(ProductImportJob job, CancellationToken ct = default)
        {
            return _channel.Writer.WriteAsync(job, ct);
        }

        public IAsyncEnumerable<ProductImportJob> ReadAllAsync(CancellationToken ct)
        {
            return _channel.Reader.ReadAllAsync(ct);
        }
    }
}
