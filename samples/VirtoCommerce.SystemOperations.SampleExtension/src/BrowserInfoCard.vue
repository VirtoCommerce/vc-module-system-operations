<script setup lang="ts">
import { computed, ref } from 'vue';

interface InfoRow {
  label: string;
  value: string;
}

const copied = ref(false);

const rows = computed<InfoRow[]>(() => {
  const ua = navigator.userAgent;
  const platform =
    (navigator as Navigator & { userAgentData?: { platform?: string } }).userAgentData?.platform ??
    navigator.platform ??
    'unknown';
  return [
    { label: 'User-Agent', value: ua },
    { label: 'Platform', value: platform },
    { label: 'Language', value: navigator.language },
    { label: 'Online', value: String(navigator.onLine) },
    { label: 'Cores', value: String(navigator.hardwareConcurrency ?? '?') },
    { label: 'Screen', value: `${screen.width}×${screen.height} @ ${window.devicePixelRatio}x` },
    { label: 'Viewport', value: `${innerWidth}×${innerHeight}` },
    { label: 'Time zone', value: Intl.DateTimeFormat().resolvedOptions().timeZone },
  ];
});

async function copyAll() {
  const text = rows.value.map((r) => `${r.label}: ${r.value}`).join('\n');
  try {
    await navigator.clipboard.writeText(text);
  } catch {
    /* clipboard may be blocked — fail silently */
  }
  copied.value = true;
  setTimeout(() => {
    copied.value = false;
  }, 2000);
}
</script>

<template>
  <div class="browser-info">
    <table class="browser-info__table">
      <tbody>
        <tr v-for="row in rows" :key="row.label">
          <th>{{ row.label }}</th>
          <td>{{ row.value }}</td>
        </tr>
      </tbody>
    </table>
    <div class="op-card__actions">
      <button class="btn btn--primary" type="button" @click="copyAll">
        <i :class="copied ? 'fas fa-check' : 'fas fa-copy'"></i>
        {{ copied ? 'Copied' : 'Copy all' }}
      </button>
    </div>
  </div>
</template>

<style scoped>
/* Scoped to avoid leaking into the host. The host already provides
   .op-card__actions, .btn, and .btn--primary via global CSS — we reuse
   them here so the card matches platform styling. */
.browser-info__table {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
  margin-bottom: 12px;
}
.browser-info__table th {
  text-align: left;
  font-weight: 600;
  padding: 4px 8px 4px 0;
  white-space: nowrap;
  vertical-align: top;
  width: 1%;
  opacity: 0.75;
}
.browser-info__table td {
  padding: 4px 0;
  word-break: break-all;
  font-family: var(--code-font, monospace);
}
</style>
