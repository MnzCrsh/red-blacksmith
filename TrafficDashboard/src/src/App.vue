<script setup lang="ts">
import { computed, onBeforeUnmount, ref } from 'vue';
import {
  Activity,
  ArrowDownToLine,
  ArrowUpRight,
  BarChart3,
  Clock3,
  Database,
  Gauge,
  Layers3,
  Radio,
  RefreshCw,
  Search,
  Server,
  ShieldCheck,
  Wifi,
  Zap,
} from 'lucide-vue-next';

import TrafficChart from '@/components/TrafficChart.vue';
import GrafanaPanel from '@/components/GrafanaPanel.vue';
import { getSourceTrafficMinute } from '@/api/trafficApi';
import type { SourceTrafficMinute } from '@/types/traffic';

const grafanaUrl = import.meta.env.VITE_GRAFANA_DASHBOARD_URL ?? '';

const now = new Date();

function toLocalDateTime(value: Date): string {
  const localDate = new Date(
      value.getTime() - value.getTimezoneOffset() * 60_000,
  );

  return localDate.toISOString().slice(0, 16);
}

const sourceId = ref('');
const fromUtc = ref(toLocalDateTime(new Date(now.getTime() - 60 * 60 * 1000)));
const toUtc = ref(toLocalDateTime(now));

const records = ref<SourceTrafficMinute[]>([]);
const loading = ref(false);
const errorMessage = ref('');
const loadedAt = ref<Date | null>(null);

let activeController: AbortController | null = null;

const hasData = computed(() => records.value.length > 0);

const totalPackets = computed(() =>
    records.value.reduce((total, item) => total + item.packetCount, 0),
);

const totalBytes = computed(() =>
    records.value.reduce((total, item) => total + item.totalBytes, 0),
);

const averageDestinations = computed(() => {
  if (!hasData.value) {
    return 0;
  }

  return Math.round(
      records.value.reduce(
          (total, item) => total + item.uniqueDestinationCount,
          0,
      ) / records.value.length,
  );
});

const recentRecords = computed(() =>
    [...records.value].reverse().slice(0, 10),
);

const numberFormatter = new Intl.NumberFormat('ru-RU');

function formatNumber(value: number): string {
  return numberFormatter.format(value);
}

function formatCompact(value: number): string {
  return new Intl.NumberFormat('ru-RU', {
    notation: 'compact',
    maximumFractionDigits: 1,
  }).format(value);
}

function formatBytes(value: number): string {
  if (value === 0) return '0 B';

  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  const exponent = Math.min(
      Math.floor(Math.log(value) / Math.log(1024)),
      units.length - 1,
  );

  return `${(value / Math.pow(1024, exponent)).toFixed(2)} ${units[exponent]}`;
}

function formatTimestamp(value: string): string {
  return new Date(value).toLocaleString('ru-RU', {
    dateStyle: 'short',
    timeStyle: 'short',
  });
}

/**
 * Загружает минутную статистику из Traffic API.
 */
async function loadTraffic(): Promise<void> {
  errorMessage.value = '';

  if (!sourceId.value.trim()) {
    errorMessage.value = 'Укажи UUID источника трафика.';
    return;
  }

  if (!/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(sourceId.value.trim())) {
    errorMessage.value = 'Некорректный UUID источника.';
    return;
  }

  const from = new Date(fromUtc.value);
  const to = new Date(toUtc.value);

  if (
      Number.isNaN(from.getTime()) ||
      Number.isNaN(to.getTime()) ||
      from >= to
  ) {
    errorMessage.value = 'Проверь временной диапазон.';
    return;
  }

  activeController?.abort();

  const controller = new AbortController();
  activeController = controller;

  loading.value = true;

  try {
    records.value = await getSourceTrafficMinute(
        {
          sourceId: sourceId.value.trim(),
          fromUtc: from,
          toUtc: to,
        },
        controller.signal,
    );

    loadedAt.value = new Date();
  } catch (error) {
    if (error instanceof Error && error.name === 'AbortError') {
      return;
    }

    errorMessage.value =
        error instanceof Error
            ? error.message
            : 'Не удалось загрузить статистику.';
  } finally {
    if (activeController === controller) {
      activeController = null;
      loading.value = false;
    }
  }
}

onBeforeUnmount(() => {
  activeController?.abort();
});
</script>

<template>
  <main class="dashboard-shell">
    <div class="ambient ambient-one" />
    <div class="ambient ambient-two" />
    <div class="ambient ambient-three" />

    <div class="dashboard-layout">
      <!-- Sidebar -->
      <aside class="sidebar glass-panel">
        <div class="brand">
          <div class="brand-icon">
            <Activity :size="23" />
          </div>

          <div>
            <div class="brand-name">REDBLACKSMITH</div>
            <div class="brand-caption">TRAFFIC INTELLIGENCE</div>
          </div>
        </div>

        <div class="sidebar-section">
          <div class="sidebar-label">MONITORING</div>

          <div class="nav-item nav-active">
            <Gauge :size="18" />
            <span>Overview</span>
          </div>

          <div class="nav-item nav-muted">
            <Radio :size="18" />
            <span>Traffic analytics</span>
          </div>

          <div class="nav-item nav-muted">
            <BarChart3 :size="18" />
            <span>Observability</span>
          </div>
        </div>

        <div class="sidebar-section">
          <div class="sidebar-label">INFRASTRUCTURE</div>

          <div class="nav-item nav-muted">
            <Server :size="18" />
            <span>Microservices</span>
          </div>

          <div class="nav-item nav-muted">
            <Database :size="18" />
            <span>Data storage</span>
          </div>
        </div>

        <div class="sidebar-bottom">
          <div class="sidebar-status">
            <span class="status-pulse" />
            <span>Monitoring enabled</span>
          </div>

          <div class="version-label">REDBLACKSMITH · 1.0</div>
        </div>
      </aside>

      <!-- Main content -->
      <section class="main-content">
        <header class="topbar glass-panel">
          <div>
            <div class="breadcrumb">Monitoring / Overview</div>
            <div class="topbar-title">Traffic overview</div>
          </div>

          <div class="topbar-actions">
            <div class="environment-badge">
              <span class="status-pulse" />
              LOCAL ENVIRONMENT
            </div>

            <div class="icon-button">
              <ShieldCheck :size="19" />
            </div>
          </div>
        </header>

        <div class="page-heading">
          <div>
            <div class="eyebrow">NETWORK TELEMETRY</div>
            <h1>Traffic intelligence</h1>
            <p>
              Аналитика сетевого трафика и состояние инфраструктуры.
            </p>
          </div>

          <div class="live-indicator">
            <span class="status-pulse" />
            DATA PLATFORM
          </div>
        </div>

        <!-- Query filters -->
        <section class="glass-panel filter-panel">
          <div class="section-heading">
            <div class="heading-icon">
              <Search :size="17" />
            </div>

            <div>
              <h2>Traffic query</h2>
              <p>Выбери источник и временной диапазон</p>
            </div>
          </div>

          <form class="filter-grid" @submit.prevent="loadTraffic">
            <label class="filter-field source-field">
              <span>Source ID</span>
              <input
                  v-model="sourceId"
                  type="text"
                  placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx"
                  spellcheck="false"
              />
            </label>

            <label class="filter-field">
              <span>From</span>
              <input
                  v-model="fromUtc"
                  type="datetime-local"
              />
            </label>

            <label class="filter-field">
              <span>To</span>
              <input
                  v-model="toUtc"
                  type="datetime-local"
              />
            </label>

            <button
                type="submit"
                class="primary-button"
                :disabled="loading"
            >
              <RefreshCw
                  :size="16"
                  :class="{ spinning: loading }"
              />
              {{ loading ? 'Loading...' : 'Load data' }}
            </button>
          </form>

          <div v-if="errorMessage" class="error-message">
            {{ errorMessage }}
          </div>
        </section>

        <!-- Metrics -->
        <section class="metrics-grid">
          <article class="metric-card glass-panel">
            <div class="metric-header">
              <span>PACKETS PROCESSED</span>
              <div class="metric-icon cyan">
                <Layers3 :size="19" />
              </div>
            </div>

            <div class="metric-value">
              {{ hasData ? formatCompact(totalPackets) : '—' }}
            </div>

            <div class="metric-footer">
              <span class="metric-accent">Total packets</span>
              <span>За выбранный период</span>
            </div>
          </article>

          <article class="metric-card glass-panel">
            <div class="metric-header">
              <span>TRAFFIC VOLUME</span>
              <div class="metric-icon violet">
                <ArrowDownToLine :size="19" />
              </div>
            </div>

            <div class="metric-value">
              {{ hasData ? formatBytes(totalBytes) : '—' }}
            </div>

            <div class="metric-footer">
              <span class="metric-accent">Total bytes</span>
              <span>За выбранный период</span>
            </div>
          </article>

          <article class="metric-card glass-panel">
            <div class="metric-header">
              <span>DESTINATIONS / MIN</span>
              <div class="metric-icon emerald">
                <Wifi :size="19" />
              </div>
            </div>

            <div class="metric-value">
              {{ hasData ? formatNumber(averageDestinations) : '—' }}
            </div>

            <div class="metric-footer">
              <span class="metric-accent">Average</span>
              <span>Уникальные IP назначения</span>
            </div>
          </article>

          <article class="metric-card glass-panel">
            <div class="metric-header">
              <span>TIME BUCKETS</span>
              <div class="metric-icon amber">
                <Clock3 :size="19" />
              </div>
            </div>

            <div class="metric-value">
              {{ hasData ? formatNumber(records.length) : '—' }}
            </div>

            <div class="metric-footer">
              <span class="metric-accent">One minute</span>
              <span>Точек агрегации</span>
            </div>
          </article>
        </section>

        <!-- Chart -->
        <section class="glass-panel content-panel">
          <div class="panel-heading">
            <div>
              <h2>Traffic activity</h2>
              <p>Количество пакетов и объём переданных данных</p>
            </div>

            <div class="panel-period">
              <Zap :size="14" />
              MINUTE AGGREGATION
            </div>
          </div>

          <div v-if="hasData">
            <TrafficChart :records="records" />
          </div>

          <div v-else class="empty-chart">
            <div class="empty-chart-icon">
              <BarChart3 :size="25" />
            </div>

            <div class="empty-chart-title">
              Нет данных для отображения
            </div>

            <p>
              Укажи Source ID и нажми Load data, чтобы загрузить статистику.
            </p>
          </div>

          <div v-if="loadedAt" class="chart-footer">
            <span>
              <span class="status-pulse" />
              Последнее обновление: {{ loadedAt.toLocaleTimeString('ru-RU') }}
            </span>

            <span>{{ records.length }} records</span>
          </div>
        </section>

        <!-- Recent records -->
        <section class="glass-panel content-panel">
          <div class="panel-heading">
            <div>
              <h2>Recent traffic records</h2>
              <p>Последние минутные агрегаты</p>
            </div>

            <div class="records-count">
              {{ records.length }} RECORDS
            </div>
          </div>

          <div class="table-container">
            <table>
              <thead>
              <tr>
                <th>TIME (UTC)</th>
                <th>PACKETS</th>
                <th>DATA VOLUME</th>
                <th>DESTINATIONS</th>
              </tr>
              </thead>

              <tbody>
              <tr v-for="record in recentRecords" :key="record.bucketStartUtc">
                <td class="table-time">
                  {{ formatTimestamp(record.bucketStartUtc) }}
                </td>

                <td>{{ formatNumber(record.packetCount) }}</td>

                <td>{{ formatBytes(record.totalBytes) }}</td>

                <td>
                    <span class="destination-badge">
                      {{ record.uniqueDestinationCount }}
                    </span>
                </td>
              </tr>

              <tr v-if="!hasData">
                <td colspan="4" class="table-empty">
                  Данные пока не загружены
                </td>
              </tr>
              </tbody>
            </table>
          </div>
        </section>

        <!-- Grafana -->
        <section class="glass-panel content-panel">
          <div class="panel-heading">
            <div>
              <h2>Infrastructure observability</h2>
              <p>Метрики и мониторинг платформы</p>
            </div>

            <div class="grafana-badge">
              GRAFANA
              <ArrowUpRight :size="13" />
            </div>
          </div>

          <GrafanaPanel :url="grafanaUrl" />
        </section>

        <footer class="dashboard-footer">
          <span>Traffic research</span>
          <span>Built for network observability</span>
        </footer>
      </section>
    </div>
  </main>
</template>