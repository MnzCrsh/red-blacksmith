/**
 * Общие поля минутной статистики трафика.
 */
export interface TrafficMinutePoint {
    bucketStartUtc: string;
    packetCount: number;
    totalBytes: number;
}

/**
 * Минутная статистика сетевого трафика источника.
 */
export interface SourceTrafficMinute extends TrafficMinutePoint {
    sourceId: string;
    uniqueDestinationCount: number;
}

/**
 * Общая минутная статистика по всем источникам.
 */
export interface TrafficMinuteAnalytics extends TrafficMinutePoint {
    destinationCount: number;
}

/**
 * Параметры запроса статистики источника.
 */
export interface SourceTrafficMinuteQuery {
    sourceId: string;
    fromUtc: Date;
    toUtc: Date;
}

/**
 * Параметры запроса общей минутной статистики.
 */
export interface TrafficMinuteAnalyticsQuery {
    fromUtc: Date;
    toUtc: Date;
}