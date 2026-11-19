using Dapper;
using FastEndpoints;
using Npgsql;
using TrafficApi.Models;

namespace TrafficApi;

public class GetSourceTrafficMinuteEndpoint(NpgsqlDataSource dataSource) 
    : Endpoint<GetSourceTrafficMinuteRequest, IReadOnlyList<SourceTrafficMinuteResponse>>
{
    public override void Configure()
    {
        Get("/api/traffic/sources/{SourceId}/minute");
        Description(x => x.WithTags("Traffic"));
    }
    
    public override async Task HandleAsync(
        GetSourceTrafficMinuteRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT
                               bucket_start_utc AS "BucketStartUtc",
                               source_id AS "SourceId",
                               packet_count AS "PacketCount",
                               total_bytes AS "TotalBytes",
                               unique_destination_count AS "UniqueDestinationCount"
                           FROM traffic_read.source_traffic_minute
                           WHERE source_id = @SourceId
                             AND bucket_start_utc >= @FromUtc
                             AND bucket_start_utc < @ToUtc
                           ORDER BY bucket_start_utc;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var result = await connection.QueryAsync<SourceTrafficMinuteResponse>(new CommandDefinition(
                                                                                   sql,
                                                                                   new
                                                                                   {
                                                                                       request.SourceId,
                                                                                       FromUtc = request.FromUtc.ToUniversalTime(),
                                                                                       ToUtc = request.ToUtc.ToUniversalTime()
                                                                                   },
                                                                                   cancellationToken: cancellationToken));
        
        await Send.OkAsync(result.AsList(), cancellationToken);
    }
}
