<script setup lang="ts">
import { ref, inject, computed } from 'vue';
import { useApi, ApiError } from '../composables/useApi';
import { I18nKey } from '../composables/useI18n';
import type { ExportManifest, ExportImportNotification, PushNotificationSearchResult } from '../types';

const { t } = inject(I18nKey)!;
const { get, post } = useApi();

const fileUrl = ref('');
const manifest = ref<ExportManifest | null>(null);
const isUploading = ref(false);
const isImporting = ref(false);
const notification = ref<ExportImportNotification | null>(null);
const error = ref('');
const uploadError = ref('');
const isDragOver = ref(false);

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

function formatDate(dateStr: string): string {
  try {
    return new Date(dateStr).toLocaleString();
  } catch {
    return dateStr;
  }
}

async function handleFileSelect(event: Event) {
  const input = event.target as HTMLInputElement;
  if (input.files?.length) {
    await uploadFile(input.files[0]);
  }
}

function handleDrop(event: DragEvent) {
  isDragOver.value = false;
  const files = event.dataTransfer?.files;
  if (files?.length) {
    uploadFile(files[0]);
  }
}

async function uploadFile(file: File) {
  uploadError.value = '';
  error.value = '';

  if (!file.name.toLowerCase().endsWith('.zip')) {
    uploadError.value = t('restore.uploadError');
    return;
  }

  isUploading.value = true;
  try {
    const formData = new FormData();
    formData.append('file', file, file.name);

    const res = await fetch('/api/assets/localstorage', {
      method: 'POST',
      credentials: 'same-origin',
      body: formData,
    });

    if (!res.ok) throw new Error(`Upload failed: ${res.status}`);

    const assets = await res.json();
    const asset = Array.isArray(assets) ? assets[0] : assets;
    fileUrl.value = asset.url;

    // Load manifest from uploaded file
    manifest.value = await get<ExportManifest>(
      `/api/platform/export/manifest/load?fileUrl=${encodeURIComponent(fileUrl.value)}`,
    );

    // Sync checkboxes with manifest
    handleSecurity.value = manifest.value.handleSecurity;
    handleBinaryData.value = manifest.value.handleBinaryData;
    handleSettings.value = manifest.value.handleSettings;
    handleDynamicProperties.value = manifest.value.handleDynamicProperties;
    manifest.value.modules.forEach((m) => (m.isChecked = true));
  } catch (err) {
    uploadError.value = err instanceof ApiError ? err.message : t('restore.uploadError');
  } finally {
    isUploading.value = false;
  }
}

function toggleAll(checked: boolean) {
  if (!manifest.value) return;
  // Only toggle entries the manifest actually contains
  if (manifest.value.handleSecurity) handleSecurity.value = checked;
  if (manifest.value.handleBinaryData) handleBinaryData.value = checked;
  if (manifest.value.handleSettings) handleSettings.value = checked;
  if (manifest.value.handleDynamicProperties) handleDynamicProperties.value = checked;
  manifest.value.modules.forEach((m) => (m.isChecked = checked));
}

async function startImport() {
  if (!manifest.value) return;
  error.value = '';
  isImporting.value = true;

  const selectedModules = manifest.value.modules
    .filter((m) => m.isChecked)
    .map((m) => m.id);

  try {
    notification.value = await post<ExportImportNotification>('/api/platform/import', {
      exportManifest: manifest.value,
      fileUrl: fileUrl.value,
      handleSecurity: handleSecurity.value,
      handleBinaryData: handleBinaryData.value,
      handleSettings: handleSettings.value,
      handleDynamicProperties: handleDynamicProperties.value,
      modules: selectedModules,
    });

    if (notification.value?.jobId) {
      await pollProgress();
    }
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : t('restore.error');
    isImporting.value = false;
  }
}

async function pollProgress() {
  const notificationId = notification.value?.id;
  if (!notificationId) return;

  const maxAttempts = 600;
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
        isImporting.value = false;
        if ((notification.value.errorCount ?? 0) > 0) {
          error.value = (notification.value.errors ?? []).join('\n');
        }
        return;
      }
    } catch {
      // Server may be temporarily unavailable
    }
  }
  isImporting.value = false;
  error.value = t('restore.error');
}

async function cancelImport() {
  if (notification.value?.jobId) {
    try {
      await post(`/api/platform/exortimport/tasks/${notification.value.jobId}/cancel`);
    } catch {
      // Ignore cancel errors
    }
  }
  isImporting.value = false;
}

function reset() {
  manifest.value = null;
  notification.value = null;
  fileUrl.value = '';
  error.value = '';
  uploadError.value = '';
}
</script>

<template>
  <!-- Step 1: File upload -->
  <div v-if="!manifest && !isUploading && !notification">
    <div
      class="file-dropzone"
      :class="{ 'file-dropzone--active': isDragOver }"
      @dragover.prevent="isDragOver = true"
      @dragleave="isDragOver = false"
      @drop.prevent="handleDrop"
      @click="($refs.fileInput as HTMLInputElement)?.click()"
    >
      <i class="fas fa-cloud-upload-alt file-dropzone__icon"></i>
      <span class="file-dropzone__text">{{ t('restore.uploadPrompt') }}</span>
      <input
        ref="fileInput"
        type="file"
        accept=".zip"
        style="display: none"
        @change="handleFileSelect"
      >
    </div>
    <div v-if="uploadError" class="op-card__error">{{ uploadError }}</div>
  </div>

  <div v-if="isUploading" class="op-card__actions">
    <button class="btn btn--primary" disabled>
      <i class="fas fa-spinner fa-spin"></i>
      {{ t('restore.loading') }}
    </button>
  </div>

  <!-- Step 2: Configure import -->
  <div v-if="manifest && !notification" class="expand-section visible">
    <div class="manifest-info">
      <div class="manifest-info__row">
        <span class="manifest-info__label">{{ t('restore.author') }}:</span>
        <span>{{ manifest.author }}</span>
      </div>
      <div class="manifest-info__row">
        <span class="manifest-info__label">{{ t('restore.created') }}:</span>
        <span>{{ formatDate(manifest.created) }}</span>
      </div>
      <div class="manifest-info__row">
        <span class="manifest-info__label">{{ t('restore.platformVersion') }}:</span>
        <span>{{ manifest.platformVersion }}</span>
      </div>
    </div>

    <div class="export-config">
      <div class="export-config__toolbar">
        <button class="btn-link" @click="toggleAll(true)">{{ t('restore.selectAll') }}</button>
        <span class="btn-link-sep">|</span>
        <button class="btn-link" @click="toggleAll(false)">{{ t('restore.unselectAll') }}</button>
      </div>

      <div class="export-config__section">
        <div class="export-config__label">{{ t('restore.platformEntries') }}</div>
        <label class="export-config__checkbox">
          <input v-model="handleSecurity" type="checkbox" :disabled="!manifest.handleSecurity"> {{ t('restore.security') }}
        </label>
        <label class="export-config__checkbox">
          <input v-model="handleBinaryData" type="checkbox" :disabled="!manifest.handleBinaryData"> {{ t('restore.binaryData') }}
        </label>
        <label class="export-config__checkbox">
          <input v-model="handleSettings" type="checkbox" :disabled="!manifest.handleSettings"> {{ t('restore.settings') }}
        </label>
        <label class="export-config__checkbox">
          <input v-model="handleDynamicProperties" type="checkbox" :disabled="!manifest.handleDynamicProperties"> {{ t('restore.dynamicProperties') }}
        </label>
      </div>

      <div class="export-config__section">
        <div class="export-config__label">{{ t('restore.modules') }}</div>
        <div v-if="manifest.modules.length === 0" class="export-config__empty">
          {{ t('restore.noModules') }}
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
      <button class="btn btn--primary" :disabled="isImporting" @click="startImport">
        <i class="fas fa-download"></i>
        {{ t('restore.action') }}
      </button>
      <button class="btn btn--outline" @click="reset">
        <i class="fas fa-redo"></i>
      </button>
    </div>
  </div>

  <!-- Step 3: Progress -->
  <div v-if="notification" class="expand-section visible">
    <div class="progress-section">
      <div class="progress-section__label">{{ t('restore.progress') }}</div>
      <div class="progress-bar">
        <div class="progress-bar__fill" :style="{ width: progressPercent + '%' }"></div>
      </div>
      <div class="progress-section__text">
        {{ notification.description || (isCompleted ? t('restore.completed') : t('restore.loading')) }}
      </div>

      <div v-if="isCompleted" class="op-card__actions">
        <button class="btn btn--outline" @click="reset">
          <i class="fas fa-redo"></i>
        </button>
      </div>

      <div v-if="isImporting" class="op-card__actions">
        <button class="btn btn--outline" @click="cancelImport">
          <i class="fas fa-times"></i>
          {{ t('restore.cancel') }}
        </button>
      </div>
    </div>
  </div>

  <div v-if="error" class="op-card__error">{{ error }}</div>
</template>
