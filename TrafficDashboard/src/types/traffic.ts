/**
 * Минутная статистика сетевого трафика источника.
 */
export interface SourceTrafficMinute {
    bucketStartUtc: string;
    sourceId: string;
    packetCount: number;
    totalBytes: number;
    uniqueDestinationCount: number;
}

/**
 * Параметры запроса статистики источника.
 */
export interface SourceTrafficMinuteQuery {
    sourceId: string;
    fromUtc: Date;
    toUtc: Date;
}