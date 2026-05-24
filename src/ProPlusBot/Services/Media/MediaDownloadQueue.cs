using System.Threading.Channels;

namespace ProPlusBot.Services.Media;

public class MediaDownloadQueue(IServiceScopeFactory scopeFactory)
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public async ValueTask<Guid> EnqueueAsync(
        MediaDownloadJob job,
        long? incomingChatMessageId,
        CancellationToken ct = default)
    {
        Guid jobId;
        using (var scope = scopeFactory.CreateScope())
        {
            var jobService = scope.ServiceProvider.GetRequiredService<MediaDownloadJobService>();
            jobId = await jobService.CreatePendingAsync(job, incomingChatMessageId, ct);
        }

        await _channel.Writer.WriteAsync(jobId, ct);
        return jobId;
    }

    public ValueTask SignalAsync(Guid jobId, CancellationToken ct = default) =>
        _channel.Writer.WriteAsync(jobId, ct);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
