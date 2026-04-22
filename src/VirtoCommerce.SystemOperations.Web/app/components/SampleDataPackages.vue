<script setup lang="ts">
import { ref, inject, computed } from 'vue';
import { useApi, ApiError } from '../composables/useApi';
import { DialogKey } from '../composables/useDialog';
import { I18nKey } from '../composables/useI18n';
import type {
  SampleDataPackage,
  SampleDataState,
  ExportImportNotification,
  ObjectSettingEntry,
  PushNotificationSearchResult,
} from '../types';

const SAMPLE_DATA_STATE_SETTING = 'VirtoCommerce.SampleDataState';
const POLL_INTERVAL_MS = 2000;
const POLL_MAX_ATTEMPTS = 900; // 30 minutes

const { get, post } = useApi();
const dialog = inject(DialogKey)!;
const { t } = inject(I18nKey)!;

const packages = ref<SampleDataPackage[]>([]);
const isVisible = ref(false);
const isLoading = ref(false);
const isLoaded = ref(false);
const installingName = ref('');
const notification = ref<ExportImportNotification | null>(null);
const error = ref('');

const progressPercent = computed(() => {
  const n = notification.value;
  if (!n) return 0;
  if (n.finished) return 100;
  if (!n.totalCount) return 0;
  return Math.round((n.processedCount / n.totalCount) * 100);
});

const isFinished = computed(() => notification.value?.finished != null);
const hasErrors = computed(() => (notification.value?.errorCount ?? 0) > 0);

function formatSize(size: string | number | null): string {
  if (!size) return '';
  if (typeof size === 'string') return size;
  return `${Math.round(size / 1024 / 1024)} MB`;
}

function toErrorMessage(err: unknown, fallback: string): string {
  return err instanceof ApiError ? err.message : fallback;
}

async function discover() {
  error.value = '';

  if (isLoaded.value) {
    isVisible.value = !isVisible.value;
    return;
  }

  isLoading.value = true;
  try {
    packages.value = await get<SampleDataPackage[]>('/api/platform/sampledata/discover');
    isLoaded.value = true;
    isVisible.value = true;
  } catch (err) {
    error.value = toErrorMessage(err, t('sampleData.discoverError'));
  } finally {
    isLoading.value = false;
  }
}

async function resetSampleDataState() {
  const setting = await get<ObjectSettingEntry>(
    `/api/platform/settings/${encodeURIComponent(SAMPLE_DATA_STATE_SETTING)}`,
  );
  setting.value = 'Undefined';
  await post('/api/platform/settings', [setting]);
}

async function install(pkg: SampleDataPackage) {
  if (!pkg.url) return; // "Empty" template or invalid entry — nothing to install
  if (installingName.value) return;

  error.value = '';
  notification.value = null;

  // Step 1: check current sample data state — the platform endpoint only enqueues
  // the import job when the state is "Undefined". Otherwise the POST returns 200
  // with an empty body and silently does nothing.
  let state: SampleDataState;
  try {
    state = await get<SampleDataState>('/api/platform/sampledata/state');
  } catch (err) {
    error.value = toErrorMessage(err, t('sampleData.stateError'));
    return;
  }

  if (state === 'Processing') {
    await dialog.warning(
      t('sampleData.processingTitle'),
      t('sampleData.processingMessage'),
    );
    return;
  }

  // Step 2: confirm (with a different message when we are about to overwrite).
  const confirmed = await dialog.warning(
    t('sampleData.confirmTitle'),
    state === 'Completed'
      ? t('sampleData.reinstallMessage', { name: pkg.name || 'Sample Data' })
      : t('sampleData.confirmMessage', { name: pkg.name || 'Sample Data' }),
  );
  if (!confirmed) return;

  // Step 3: reset the state gate so the platform will actually enqueue the job.
  if (state === 'Completed') {
    try {
      await resetSampleDataState();
    } catch (err) {
      error.value = toErrorMessage(err, t('sampleData.resetStateError'));
      return;
    }
  }

  // Step 4: kick off import. Use `name` (legacy-compatible) — the backend's
  // background job looks up the URL from discovery by name.
  installingName.value = pkg.name;
  try {
    const started = await post<ExportImportNotification | null>(
      `/api/platform/sampledata/import?name=${encodeURIComponent(pkg.name)}`,
    );
    if (!started || !started.id) {
      error.value = t('sampleData.notStartedError');
      installingName.value = '';
      return;
    }
    notification.value = started;
    await pollProgress();
  } catch (err) {
    error.value = toErrorMessage(err, t('sampleData.installError'));
    installingName.value = '';
  }
}

async function pollProgress() {
  const notificationId = notification.value?.id;
  if (!notificationId) return;

  for (let i = 0; i < POLL_MAX_ATTEMPTS; i++) {
    await new Promise((r) => setTimeout(r, POLL_INTERVAL_MS));
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
        installingName.value = '';
        if (hasErrors.value) {
          error.value = (notification.value.errors ?? []).join('\n') || t('sampleData.installError');
        }
        return;
      }
    } catch {
      // Transient error — keep polling.
    }
  }

  // Timeout — stop waiting but do not clear the notification so the user sees last state.
  installingName.value = '';
  error.value = t('sampleData.pollTimeoutError');
}

async function cancelInstall() {
  const jobId = notification.value?.jobId;
  if (!jobId) return;
  try {
    await post(`/api/platform/exortimport/tasks/${jobId}/cancel`);
  } catch {
    // ignore cancel errors — user will see next poll result
  }
}

function resetProgress() {
  notification.value = null;
  installingName.value = '';
  error.value = '';
}
</script>

<template>
  <div class="op-card__actions">
    <button class="btn btn--primary" :disabled="isLoading" @click="discover">
      <i :class="isLoading ? 'fas fa-spinner fa-spin' : 'fas fa-search'"></i>
      {{ t('sampleData.discoverAction') }}
    </button>
  </div>

  <div v-if="isVisible && !notification" class="expand-section">
    <ul class="package-list">
      <li v-if="packages.length === 0" class="package-item">
        <div class="package-item__info">
          <div class="package-item__name">{{ t('sampleData.noPackages') }}</div>
        </div>
      </li>
      <li v-for="pkg in packages" :key="pkg.url ?? pkg.name" class="package-item">
        <div class="package-item__info">
          <div class="package-item__name">{{ pkg.name || 'Sample Data' }}</div>
          <div class="package-item__desc">{{ pkg.description }}</div>
        </div>
        <span v-if="pkg.size" class="package-item__size">{{ formatSize(pkg.size) }}</span>
        <button
          class="btn btn--outline"
          :disabled="!pkg.url || installingName === pkg.name"
          @click="install(pkg)"
        >
          <i :class="installingName === pkg.name ? 'fas fa-spinner fa-spin' : 'fas fa-download'"></i>
          {{ t('sampleData.installAction') }}
        </button>
      </li>
    </ul>
  </div>

  <div v-if="notification" class="expand-section visible">
    <div class="progress-section">
      <div class="progress-section__label">{{ t('sampleData.progressTitle') }}</div>

      <div class="progress-bar">
        <div class="progress-bar__fill" :style="{ width: progressPercent + '%' }"></div>
      </div>

      <div class="progress-section__text">
        {{ notification.description || (isFinished ? t('sampleData.completed') : t('sampleData.starting')) }}
      </div>

      <div v-if="isFinished" class="progress-section__download">
        <button class="btn btn--primary" @click="resetProgress">
          <i class="fas fa-check"></i>
          {{ t('sampleData.installAnother') }}
        </button>
      </div>

      <div v-if="!isFinished" class="op-card__actions">
        <button class="btn btn--outline" @click="cancelInstall">
          <i class="fas fa-times"></i>
          {{ t('sampleData.cancelAction') }}
        </button>
      </div>
    </div>
  </div>

  <div v-if="error" class="op-card__error">{{ error }}</div>
</template>
