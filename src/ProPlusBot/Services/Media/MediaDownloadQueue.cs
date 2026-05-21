using System.Threading.Channels;

namespace ProPlusBot.Services.Media;

public class MediaDownloadQueue
{
    private readonly Channel<MediaDownloadJob> _channel = Channel.CreateUnbounded<MediaDownloadJob>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ValueTask EnqueueAsync(MediaDownloadJob job, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(job, ct);

    public IAsyncEnumerable<MediaDownloadJob> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
