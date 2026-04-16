<script setup lang="ts">
import { ref, inject, computed } from 'vue';
import { useApi, ApiError } from '../composables/useApi';
import { I18nKey } from '../composables/useI18n';
import type { ExportManifest, ExportImportNotification, PushNotificationSearchResult } from '../types';

const { t } = inject(I18nKey)!;
const { get, post } = useApi();

const manifest = ref<ExportManifest | null>(null);
const isLoadingManifest = ref(false);
const isExporting = ref(false);
const notification = ref<ExportImportNotification | null>(null);
const error = ref('');

const handleSecurity = ref(true);
const handleBinaryData = ref(true);
const handleSettings = ref(true);
const handleDynamicProperties = ref(true);

const progressPercent = computed(() => {
  if (!notification.value) return 0;
  if (notification.value.finished) return 100;
  if (!notification.value.totalCount) return 0;
  return Math.round((notification.value.processedCount / notification.value.totalCount) * 100);
});

const isCompleted = computed(() => notification.value?.finished != null);

async function loadManifest() {
  error.value = '';
  isLoadingManifest.value = true;
  try {
    manifest.value = await get<ExportManifest>('/api/platform/export/manifest/new');
    manifest.value.modules.forEach((m) => (m.isChecked = true));
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : t('backup.error');
  } finally {
    isLoadingManifest.value = false;
  }
}

function toggleAll(checked: boolean) {
  handleSecurity.value = checked;
  handleBinaryData.value = checked;
  handleSettings.value = checked;
  handleDynamicProperties.value = checked;
  manifest.value?.modules.forEach((m) => (m.isChecked = checked));
}

async function startExport() {
  if (!manifest.value) return;
  error.value = '';
  isExporting.value = true;

  const selectedModules = manifest.value.modules
    .filter((m) => m.isChecked)
    .map((m) => m.id);

  try {
    notification.value = await post<ExportImportNotification>('/api/platform/export', {
      exportManifest: manifest.value,
      handleSecurity: handleSecurity.value,
      handleBinaryData: handleBinaryData.value,
      handleSettings: handleSettings.value,
      handleDynamicProperties: handleDynamicProperties.value,
      modules: selectedModules,
    });

    // Poll for progress
    if (notification.value?.jobId) {
      await pollProgress();
    }
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : t('backup.error');
    isExporting.value = false;
  }
}

async function pollProgress() {
  const notificationId = notification.value?.id;
  if (!notificationId) return;

  const maxAttempts = 600; // 10 minutes max
  for (let i = 0; i < maxAttempts; i++) {
    await new Promise((r) => setTimeout(r, 2000));
    if (!notification.value || notification.value.id !== notificationId) return;

    try {
      const res = await post<PushNotificationSearchResult>(
        '/api/platform/pushnotifications',
        { ids: [notificationId] },
      );
      const evt = res?.notifyEvents?.[0] as Partial<ExportImportNotification> | undefined;
      if (evt) {
        notification.value = { ...notification.value, ...evt } as ExportImportNotification;
      }

      if (notification.value.finished) {
        isExporting.value = false;
        if ((notification.value.errorCount ?? 0) > 0) {
          error.value = (notification.value.errors ?? []).join('\n');
        }
        return;
      }
    } catch {
      // Server may be temporarily unavailable, keep polling
    }
  }
  isExporting.value = false;
  error.value = t('backup.error');
}

async function cancelExport() {
  if (notification.value?.jobId) {
    try {
      await post(`/api/platform/exortimport/tasks/${notification.value.jobId}/cancel`);
    } catch {
      // Ignore cancel errors
    }
  }
  isExporting.value = false;
}

function reset() {
  notification.value = null;
  error.value = '';
}
</script>

<template>
  <!-- Step 1: Load manifest -->
  <div v-if="!manifest && !isLoadingManifest" class="op-card__actions">
    <button class="btn btn--primary" @click="loadManifest">
      <i class="fas fa-cog"></i>
      {{ t('backup.action') }}
    </button>
  </div>

  <div v-if="isLoadingManifest" class="op-card__actions">
    <button class="btn btn--primary" disabled>
      <i class="fas fa-spinner fa-spin"></i>
      {{ t('backup.loading') }}
    </button>
  </div>

  <!-- Step 2: Configure export -->
  <div v-if="manifest && !notification" class="expand-section visible">
    <div class="export-config">
      <div class="export-config__toolbar">
        <button class="btn-link" @click="toggleAll(true)">{{ t('backup.selectAll') }}</button>
        <span class="btn-link-sep">|</span>
        <button class="btn-link" @click="toggleAll(false)">{{ t('backup.unselectAll') }}</button>
      </div>

      <div class="export-config__section">
        <div class="export-config__label">{{ t('backup.platformEntries') }}</div>
        <label class="export-config__checkbox">
          <input v-model="handleSecurity" type="checkbox"> {{ t('backup.security') }}
        </label>
        <label class="export-config__checkbox">
          <input v-model="handleBinaryData" type="checkbox"> {{ t('backup.binaryData') }}
        </label>
        <label class="export-config__checkbox">
          <input v-model="handleSettings" type="checkbox"> {{ t('backup.settings') }}
        </label>
        <label class="export-config__checkbox">
          <input v-model="handleDynamicProperties" type="checkbox"> {{ t('backup.dynamicProperties') }}
        </label>
      </div>

      <div class="export-config__section">
        <div class="export-config__label">{{ t('backup.modules') }}</div>
        <div v-if="manifest.modules.length === 0" class="export-config__empty">
          {{ t('backup.noModules') }}
        </div>
        <div v-else class="export-config__modules">
          <label v-for="mod in manifest.modules" :key="mod.id" class="export-config__checkbox">
            <input v-model="mod.isChecked" type="checkbox"> {{ mod.id }}
            <span class="export-config__version">{{ mod.version }}</span>
          </label>
        </div>
      </div>
    </div>

    <div class="op-card__actions">
      <button class="btn btn--primary" :disabled="isExporting" @click="startExport">
        <i class="fas fa-upload"></i>
        {{ t('backup.action') }}
      </button>
    </div>
  </div>

  <!-- Step 3: Progress -->
  <div v-if="notification" class="expand-section visible">
    <div class="progress-section">
      <div class="progress-section__label">{{ t('backup.progress') }}</div>
      <div class="progress-bar">
        <div class="progress-bar__fill" :style="{ width: progressPercent + '%' }"></div>
      </div>
      <div class="progress-section__text">
        {{ notification.description || (isCompleted ? t('backup.completed') : t('backup.loading')) }}
      </div>

      <div v-if="isCompleted" class="progress-section__download">
        <a v-if="notification.downloadUrl" :href="'/' + notification.downloadUrl" class="btn btn--primary" download>
          <i class="fas fa-download"></i>
          {{ t('backup.downloadFile') }}
        </a>
        <button class="btn btn--outline" @click="reset">
          <i class="fas fa-redo"></i>
        </button>
      </div>

      <div v-if="isExporting" class="op-card__actions">
        <button class="btn btn--outline" @click="cancelExport">
          <i class="fas fa-times"></i>
          {{ t('backup.cancel') }}
        </button>
      </div>
    </div>
  </div>

  <div v-if="error" class="op-card__error">{{ error }}</div>
</template>
