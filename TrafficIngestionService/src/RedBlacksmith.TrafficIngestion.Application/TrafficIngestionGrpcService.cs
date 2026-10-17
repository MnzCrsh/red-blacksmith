using System.Threading.Channels;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using RedBlacksmith.TrafficIngestion.Models;
using Proto = RedBlacksmith.TrafficIngestion.Contracts;

namespace RedBlacksmith.TrafficIngestion.Application;

/// <summary>
/// Принимает, проверяет и буферизует сетевые события.
/// </summary>
/// <param name="eventValidator"><see cref="PacketEventValidator"/></param>
/// <param name="channelWriter"><see cref="ChannelWriter{PacketEvent}"/></param>
internal sealed class TrafficIngestionGrpcService(
    ChannelWriter<PacketEvent> channelWriter,
    ILogger<TrafficIngestionGrpcService> logger) : Proto.TrafficIngestion.TrafficIngestionBase
{
    /// <inheritdoc/>
    public override async Task<Empty> Ingest(IAsyncStreamReader<Proto.PacketEvent> requestStream, ServerCallContext context)
    {
        var enqueuedCount = 0;
        var rejectedCount = 0;
        string? lastValidationError = null;
        
        while (await requestStream.MoveNext(context.CancellationToken))
        {
            var request = requestStream.Current;

            if (!PacketEventValidator.TryValidate(request, out var packetEvent, out var errorMessage ))
            {
                rejectedCount++;
                lastValidationError = errorMessage;
                continue;
            }

            try
            {
                await channelWriter.WriteAsync(packetEvent, context.CancellationToken);
                enqueuedCount++;
            }
            catch (ChannelClosedException)
            {
                throw new RpcException(new Status(StatusCode.Unavailable, "Приём событий временно недоступен."));
            }
        }

        if (rejectedCount > 0) 
            logger.LogWarning("Rejected {RejectedCount} invalid events. Last validation error: {Error}", rejectedCount, lastValidationError);
        
        logger.LogDebug("gRPC ingestion stream completed. Events enqueued: {EnqueuedCount}, rejected: {RejectedCount}.",
                        enqueuedCount, rejectedCount);
        
        return new Empty();
    }
}