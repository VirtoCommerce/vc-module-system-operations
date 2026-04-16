<script setup lang="ts">
import { ref, inject, onMounted } from 'vue';
import { useApi, ApiError } from '../composables/useApi';
import { I18nKey } from '../composables/useI18n';
import type { SystemInfo } from '../types';

const { t } = inject(I18nKey)!;
const { get } = useApi();

const info = ref<SystemInfo | null>(null);
const isLoading = ref(false);
const error = ref('');
const copied = ref(false);

function formatDate(dateStr: string | null): string {
  if (!dateStr) return '—';
  try {
    return new Date(dateStr).toLocaleDateString();
  } catch {
    return dateStr;
  }
}

async function loadInfo() {
  error.value = '';
  isLoading.value = true;
  try {
    info.value = await get<SystemInfo>('/api/platform/diagnostics/systeminfo');
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : t('platformInfo.error');
  } finally {
    isLoading.value = false;
  }
}

function downloadJson() {
  if (!info.value) return;
  const blob = new Blob([JSON.stringify(info.value, null, '\t')], { type: 'application/json' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = 'vc-platform-info.json';
  a.click();
  URL.revokeObjectURL(a.href);
}

async function copyToClipboard() {
  if (!info.value) return;

  const lines: string[] = [];
  lines.push(`Platform Version: ${info.value.platformVersion}`);
  lines.push(`Environment: ${info.value.environmentName}`);
  lines.push(`Database Provider: ${info.value.databaseProvider}`);
  lines.push(`Runtime: ${info.value.runtimeIdentifier}`);
  lines.push(`CLR Version: ${info.value.version}`);
  lines.push(`64-bit OS: ${info.value.is64BitOperatingSystem}`);
  lines.push(`64-bit Process: ${info.value.is64BitProcess}`);

  if (info.value.license) {
    lines.push(`License: ${info.value.license.type}`);
    lines.push(`Customer: ${info.value.license.customerName}`);
    if (info.value.license.expirationDate) {
      lines.push(`Expires: ${formatDate(info.value.license.expirationDate)}`);
    }
  } else {
    lines.push('License: Community (no license)');
  }

  lines.push('');
  lines.push(`Modules (${info.value.installedModules.length}):`);
  for (const m of info.value.installedModules) {
    lines.push(`  ${m.id} ${m.version}`);
  }

  try {
    await navigator.clipboard.writeText(lines.join('\n'));
    copied.value = true;
    setTimeout(() => { copied.value = false; }, 2000);
  } catch {
    const ta = document.createElement('textarea');
    ta.value = lines.join('\n');
    ta.style.position = 'fixed';
    ta.style.opacity = '0';
    document.body.appendChild(ta);
    ta.select();
    document.execCommand('copy');
    document.body.removeChild(ta);
    copied.value = true;
    setTimeout(() => { copied.value = false; }, 2000);
  }
}

// Load automatically on mount
onMounted(() => loadInfo());
</script>

<template>
  <div v-if="isLoading" class="op-card__actions">
    <button class="btn btn--primary" disabled>
      <i class="fas fa-spinner fa-spin"></i>
      {{ t('platformInfo.loading') }}
    </button>
  </div>

  <div v-if="info" class="expand-section visible">
    <div class="manifest-info">
      <div class="manifest-info__row">
        <span class="manifest-info__label">{{ t('platformInfo.version') }}:</span>
        <span>{{ info.platformVersion }}</span>
      </div>
      <div class="manifest-info__row">
        <span class="manifest-info__label">{{ t('platformInfo.environment') }}:</span>
        <span>{{ info.environmentName }}</span>
      </div>
      <div class="manifest-info__row">
        <span class="manifest-info__label">{{ t('platformInfo.database') }}:</span>
        <span>{{ info.databaseProvider }}</span>
      </div>
      <div class="manifest-info__row">
        <span class="manifest-info__label">{{ t('platformInfo.runtime') }}:</span>
        <span>{{ info.runtimeIdentifier }}</span>
      </div>
      <div class="manifest-info__row">
        <span class="manifest-info__label">{{ t('platformInfo.license') }}:</span>
        <span v-if="info.license">
          {{ info.license.type }}
          <template v-if="info.license.customerName"> — {{ info.license.customerName }}</template>
          <template v-if="info.license.expirationDate"> ({{ t('platformInfo.expires') }} {{ formatDate(info.license.expirationDate) }})</template>
        </span>
        <span v-else>{{ t('platformInfo.noLicense') }}</span>
      </div>
      <div class="manifest-info__row">
        <span class="manifest-info__label">{{ t('platformInfo.modules') }}:</span>
        <span>{{ info.installedModules.length }}</span>
      </div>
    </div>

    <div class="op-card__actions">
      <button class="btn btn--primary" @click="downloadJson">
        <i class="fas fa-download"></i>
        {{ t('platformInfo.download') }}
      </button>
      <button class="btn btn--outline" @click="copyToClipboard">
        <i :class="copied ? 'fas fa-check' : 'fas fa-copy'"></i>
        {{ copied ? t('platformInfo.copied') : t('platformInfo.copy') }}
      </button>
    </div>
  </div>

  <div v-if="error" class="op-card__error">{{ error }}</div>
</template>
