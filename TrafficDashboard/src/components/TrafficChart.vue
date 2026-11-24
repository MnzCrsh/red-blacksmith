<script setup lang="ts">
import { computed } from 'vue';
import { use } from 'echarts/core';
import { CanvasRenderer } from 'echarts/renderers';
import { LineChart } from 'echarts/charts';
import {
  GridComponent,
  LegendComponent,
  TooltipComponent,
} from 'echarts/components';
import type { EChartsOption } from 'echarts';
import VChart from 'vue-echarts';

import type { TrafficMinutePoint } from '@/types/traffic';

use([
  CanvasRenderer,
  LineChart,
  GridComponent,
  LegendComponent,
  TooltipComponent,
]);

const props = defineProps<{
  records: TrafficMinutePoint[];
}>();

const formatter = new Intl.DateTimeFormat('ru-RU', {
  hour: '2-digit',
  minute: '2-digit',
});

const option = computed<EChartsOption>(() => ({
  animationDuration: 500,

  backgroundColor: 'transparent',

  grid: {
    top: 45,
    left: 55,
    right: 65,
    bottom: 30,
    containLabel: true,
  },

  legend: {
    top: 0,
    right: 0,
    textStyle: {
      color: '#94a3b8',
    },
    itemWidth: 14,
    itemHeight: 6,
  },

  tooltip: {
    trigger: 'axis',
    backgroundColor: 'rgba(15, 23, 42, 0.94)',
    borderColor: 'rgba(148, 163, 184, 0.2)',
    textStyle: {
      color: '#e2e8f0',
    },
  },

  xAxis: {
    type: 'category',
    boundaryGap: false,
    data: props.records.map(record =>
        formatter.format(new Date(record.bucketStartUtc)),
    ),
    axisLabel: {
      color: '#64748b',
    },
    axisLine: {
      lineStyle: {
        color: 'rgba(148, 163, 184, 0.15)',
      },
    },
    axisTick: {
      show: false,
    },
  },

  yAxis: [
    {
      type: 'value',
      name: 'Пакеты',
      nameTextStyle: {
        color: '#64748b',
      },
      axisLabel: {
        color: '#64748b',
      },
      splitLine: {
        lineStyle: {
          color: 'rgba(148, 163, 184, 0.08)',
        },
      },
    },
    {
      type: 'value',
      name: 'Байты',
      nameTextStyle: {
        color: '#64748b',
      },
      axisLabel: {
        color: '#64748b',
      },
      splitLine: {
        show: false,
      },
    },
  ],

  series: [
    {
      name: 'Пакеты',
      type: 'line',
      smooth: true,
      showSymbol: false,
      yAxisIndex: 0,
      data: props.records.map(record => record.packetCount),
      lineStyle: {
        width: 3,
        color: '#38bdf8',
      },
      areaStyle: {
        opacity: 0.1,
        color: '#38bdf8',
      },
    },
    {
      name: 'Трафик',
      type: 'line',
      smooth: true,
      showSymbol: false,
      yAxisIndex: 1,
      data: props.records.map(record => record.totalBytes),
      lineStyle: {
        width: 2,
        color: '#a78bfa',
      },
    },
  ],
}));
</script>

<template>
  <VChart
      :option="option"
      autoresize
      class="traffic-chart"
  />
</template>

<style scoped>
.traffic-chart {
  width: 100%;
  height: 320px;
}
</style>