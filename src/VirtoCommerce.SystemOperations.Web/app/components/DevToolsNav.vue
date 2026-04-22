<script setup lang="ts">
import { ref, inject, onMounted } from 'vue';
import { useApi } from '../composables/useApi';
import { I18nKey } from '../composables/useI18n';

interface DevTool {
  name: string;
  url: string;
  isExternal: boolean;
  sortOrder: number;
  permission: string;
}

interface ToolStyle {
  icon: string;
  color: string;
}

const knownTools: { match: (t: DevTool) => boolean; style: ToolStyle }[] = [
  { match: (t) => /health/i.test(t.name) || /health/i.test(t.url),       style: { icon: 'fas fa-heartbeat',      color: '#e74c3c' } },
  { match: (t) => /hangfire/i.test(t.name) || /hangfire/i.test(t.url),   style: { icon: 'fas fa-clock',          color: '#8e44ad' } },
  { match: (t) => /swagger/i.test(t.name) || /swagger/i.test(t.url),     style: { icon: 'fas fa-file-alt',       color: '#85ea2d' } },
  { match: (t) => /graphql/i.test(t.name) || /graphql/i.test(t.url),     style: { icon: 'fas fa-project-diagram', color: '#e535ab' } },
  { match: (t) => /log/i.test(t.name),                                    style: { icon: 'fas fa-scroll',         color: '#f39c12' } },
  { match: (t) => /metric/i.test(t.name) || /monitor/i.test(t.name),     style: { icon: 'fas fa-chart-line',     color: '#3498db' } },
  { match: (t) => /cache/i.test(t.name) || /redis/i.test(t.name),        style: { icon: 'fas fa-memory',         color: '#d63031' } },
  { match: (t) => /search/i.test(t.name) || /elastic/i.test(t.name),     style: { icon: 'fas fa-search',         color: '#00bcd4' } },
  { match: (t) => /config/i.test(t.name) || /setting/i.test(t.name),     style: { icon: 'fas fa-sliders-h',      color: '#607d8b' } },
];

const defaultStyle: ToolStyle = { icon: 'fas fa-toolbox', color: 'var(--vc-success)' };

function getToolStyle(tool: DevTool): ToolStyle {
  return knownTools.find((k) => k.match(tool))?.style ?? defaultStyle;
}

function isCurrent(tool: DevTool): boolean {
  return tool.url.includes('system-operations');
}

function getToolHref(tool: DevTool): string {
  if (tool.isExternal) return tool.url;
  const url = tool.url.startsWith('/') ? tool.url : `/${tool.url}`;
  return url;
}

const { t } = inject(I18nKey)!;
const { get } = useApi();

const tools = ref<DevTool[]>([]);
const isLoaded = ref(false);

onMounted(async () => {
  try {
    const data = await get<DevTool[]>('/api/platform/developer-tools');
    tools.value = data
      .filter((tool) => !isCurrent(tool))
      .sort((a, b) => a.sortOrder - b.sortOrder);
  } catch {
    // Silently fail
  } finally {
    isLoaded.value = true;
  }
});
</script>

<template>
  <div v-if="isLoaded && tools.length > 0" class="section">
    <div class="section-title">{{ t('devTools.title') }}</div>
    <div class="devtools-grid">
      <a
        v-for="tool in tools"
        :key="tool.url"
        :href="getToolHref(tool)"
        :target="tool.isExternal ? '_blank' : '_self'"
        :rel="tool.isExternal ? 'noopener' : undefined"
        class="devtool-widget"
      >
        <i :class="getToolStyle(tool).icon" class="devtool-widget__icon" :style="{ color: getToolStyle(tool).color }"></i>
        <span class="devtool-widget__name">{{ tool.name }}</span>
        <span v-if="tool.permission" class="devtool-widget__permission">{{ tool.permission }}</span>
        <i :class="tool.isExternal ? 'fas fa-external-link-alt' : 'fas fa-arrow-right'" class="devtool-widget__arrow"></i>
      </a>
    </div>
  </div>
</template>
