using System.Threading.Channels;
using Microsoft.Extensions.Options;
using OmniOps.Core.Entities;
using OmniOps.Core.Interfaces;
using OmniOps.Infrastructure.Configuration;

namespace OmniOps.Infrastructure.Services;

/// <summary>Bounded in-memory queue between telemetry producers and <c>TelemetryIngestionWorker</c>.</summary>
public sealed class ChannelTelemetryIngestionQueue : ITelemetryIngestionQueue
{
    private readonly Channel<VehicleTelemetry> _channel;

    public ChannelTelemetryIngestionQueue(IOptions<IngestionOptions> options)
    {
        _channel = Channel.CreateBounded<VehicleTelemetry>(new BoundedChannelOptions(
            Math.Max(1, options.Value.QueueCapacity))
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
        });
    }

    public ChannelReader<VehicleTelemetry> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(VehicleTelemetry telemetry, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(telemetry, cancellationToken);
}
