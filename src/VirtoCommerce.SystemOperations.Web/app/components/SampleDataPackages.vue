<script setup lang="ts">
import { ref, inject } from 'vue';
import { useApi, ApiError } from '../composables/useApi';
import { DialogKey } from '../composables/useDialog';
import { I18nKey } from '../composables/useI18n';
import type { SampleDataPackage } from '../types';

const { get, post } = useApi();
const dialog = inject(DialogKey)!;
const { t } = inject(I18nKey)!;

const packages = ref<SampleDataPackage[]>([]);
const isVisible = ref(false);
const isLoading = ref(false);
const isLoaded = ref(false);
const installingUrl = ref('');
const error = ref('');

function formatSize(size: string | number | null): string {
  if (!size) return '';
  if (typeof size === 'string') return size;
  return `${Math.round(size / 1024 / 1024)} MB`;
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
    error.value = err instanceof ApiError ? err.message : t('sampleData.discoverError');
  } finally {
    isLoading.value = false;
  }
}

async function install(pkg: SampleDataPackage) {
  error.value = '';
  const confirmed = await dialog.warning(
    t('sampleData.confirmTitle'),
    t('sampleData.confirmMessage', { name: pkg.name }),
  );
  if (!confirmed) return;

  installingUrl.value = pkg.url ?? '';
  try {
    await post(`/api/platform/sampledata/import?url=${encodeURIComponent(pkg.url ?? '')}`);
    await dialog.success(
      t('sampleData.successTitle'),
      t('sampleData.successMessage'),
    );
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : t('sampleData.installError');
  } finally {
    installingUrl.value = '';
  }
}
</script>

<template>
  <div class="op-card__actions">
    <button class="btn btn--primary" :disabled="isLoading" @click="discover">
      <i :class="isLoading ? 'fas fa-spinner fa-spin' : 'fas fa-search'"></i>
      {{ t('sampleData.discoverAction') }}
    </button>
  </div>

  <div v-if="isVisible" class="expand-section">
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
          :disabled="installingUrl === pkg.url"
          @click="install(pkg)"
        >
          <i :class="installingUrl === pkg.url ? 'fas fa-spinner fa-spin' : 'fas fa-download'"></i>
          {{ t('sampleData.installAction') }}
        </button>
      </li>
    </ul>
  </div>

  <div v-if="error" class="op-card__error">{{ error }}</div>
</template>
