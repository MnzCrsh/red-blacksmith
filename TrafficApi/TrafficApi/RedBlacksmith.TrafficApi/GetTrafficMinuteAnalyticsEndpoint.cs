using Dapper;
using FastEndpoints;
using Npgsql;
using TrafficApi.Models;

namespace TrafficApi;

public class GetTrafficMinuteAnalyticsEndpoint(NpgsqlDataSource dataSource)
    : Endpoint<GetTrafficMinuteAnalyticsRequest, IReadOnlyList<TrafficMinuteAnalyticsResponse>>
{
    /// <inheritdoc />
    public override void Configure()
    {
        Get("/api/traffic/analytics/minute");
        AllowAnonymous(Http.GET);
        Description(x => x.WithTags("Traffic"));
    }

    /// <inheritdoc />
    public override async Task HandleAsync(
        GetTrafficMinuteAnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT
                               bucket_start_utc AS "BucketStartUtc",
                               SUM(packet_count)::BIGINT AS "PacketCount",
                               SUM(total_bytes)::BIGINT AS "TotalBytes",
                               SUM(unique_destination_count)::BIGINT AS "DestinationCount"
                           FROM traffic_read.source_traffic_minute
                           WHERE bucket_start_utc >= @FromUtc
                             AND bucket_start_utc < @ToUtc
                           GROUP BY bucket_start_utc
                           ORDER BY bucket_start_utc;
                           """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        var result = await connection.QueryAsync<TrafficMinuteAnalyticsResponse>(
         new CommandDefinition(sql,
                               new
                               {
                                   FromUtc = request.FromUtc.ToUniversalTime(),
                                   ToUtc = request.ToUtc.ToUniversalTime()
                               },
                               cancellationToken: cancellationToken));

        await Send.OkAsync(result.AsList(), cancellationToken);
    }
}