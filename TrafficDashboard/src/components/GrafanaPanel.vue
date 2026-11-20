<script setup lang="ts">
defineProps<{
  url: string;
}>();
</script>

<template>
  <div class="grafana-frame">
    <iframe
        v-if="url"
        :src="url"
        title="Grafana monitoring dashboard"
        loading="lazy"
        allow="fullscreen"
        referrerpolicy="strict-origin-when-cross-origin"
    />

    <div v-else class="grafana-placeholder">
      <div class="grafana-placeholder-icon">
        <span class="text-2xl">◈</span>
      </div>

      <h3>Grafana dashboard</h3>

      <p>
        Укажите VITE_GRAFANA_DASHBOARD_URL в конфигурации окружения.
      </p>
    </div>
  </div>
</template>

<style scoped>
.grafana-frame {
  min-height: 420px;
  overflow: hidden;
  border-radius: 18px;
  background: rgb(243 170 101);
}

.grafana-frame iframe {
  display: block;
  width: 100%;
  height: 520px;
  border: 0;
}

.grafana-placeholder {
  min-height: 420px;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 12px;
  padding: 24px;
  text-align: center;
  color: #94a3b8;
}

.grafana-placeholder h3 {
  color: #e2e8f0;
  font-size: 17px;
  font-weight: 600;
}

.grafana-placeholder p {
  max-width: 360px;
  font-size: 13px;
  line-height: 1.6;
}

.grafana-placeholder-icon {
  display: grid;
  width: 54px;
  height: 54px;
  place-items: center;
  border: 1px solid rgba(148, 163, 184, 0.15);
  border-radius: 16px;
  background: rgba(148, 163, 184, 0.08);
}
</style>