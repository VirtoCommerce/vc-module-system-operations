<script setup lang="ts">
import { ref, inject } from 'vue';
import { useApi, ApiError } from '../composables/useApi';
import { I18nKey } from '../composables/useI18n';

const { t } = inject(I18nKey)!;
const { downloadFile } = useApi();

const isExporting = ref(false);
const error = ref('');

async function exportMigrations(): Promise<void> {
  error.value = '';
  isExporting.value = true;
  try {
    await downloadFile('/api/system-operations/migrations/export?mode=idempotent', 'migration-scripts.zip');
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : t('exportMigrations.error');
  } finally {
    isExporting.value = false;
  }
}
</script>

<template>
  <div class="op-card__actions">
    <button class="btn btn--primary" :disabled="isExporting" @click="exportMigrations">
      <i :class="isExporting ? 'fas fa-spinner fa-spin' : 'fas fa-file-export'"></i>
      {{ isExporting ? t('exportMigrations.loading') : t('exportMigrations.action') }}
    </button>
  </div>
  <div v-if="error" class="op-card__error">{{ error }}</div>
</template>
