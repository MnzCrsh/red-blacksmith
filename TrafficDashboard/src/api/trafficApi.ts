import type {
    SourceTrafficMinute,
    SourceTrafficMinuteQuery, TrafficMinuteAnalytics, TrafficMinuteAnalyticsQuery,
} from '@/types/traffic';

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL?.replace(/\/$/, '') ?? '';

/**
 * Получает минутную статистику указанного источника.
 */
export async function getSourceTrafficMinute(
    query: SourceTrafficMinuteQuery,
    signal?: AbortSignal,
): Promise<SourceTrafficMinute[]> {
    const parameters = new URLSearchParams({
        fromUtc: query.fromUtc.toISOString(),
        toUtc: query.toUtc.toISOString(),
    });

    const response = await fetch(
        `${API_BASE_URL}/api/traffic/sources/${encodeURIComponent(query.sourceId)}/minute?${parameters}`,
        {
            method: 'GET',
            headers: {
                Accept: 'application/json',
            },
            signal,
        },
    );

    if (!response.ok) {
        const details = await response.text();

        throw new Error(
            details || `Traffic API вернул HTTP ${response.status}.`,
        );
    }

    return await response.json() as SourceTrafficMinute[];
}

/**
 * Получает общую минутную статистику по всем источникам.
 */
export async function getTrafficMinuteAnalytics(
    query: TrafficMinuteAnalyticsQuery,
    signal?: AbortSignal,
): Promise<TrafficMinuteAnalytics[]> {
    const parameters = new URLSearchParams({
        fromUtc: query.fromUtc.toISOString(),
        toUtc: query.toUtc.toISOString(),
    });

    const response = await fetch(
        `${API_BASE_URL}/api/traffic/analytics/minute?${parameters}`,
        {
            method: 'GET',
            headers: {
                Accept: 'application/json',
            },
            signal,
        },
    );

    if (!response.ok) {
        const details = await response.text();
        throw new Error(details || `Traffic API вернул HTTP ${response.status}.`);
    }

    return await response.json() as TrafficMinuteAnalytics[];
}