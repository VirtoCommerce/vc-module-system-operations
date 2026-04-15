<script setup lang="ts">
import { ref, inject } from 'vue';
import { useApi, ApiError } from '../composables/useApi';
import { I18nKey } from '../composables/useI18n';

const { t } = inject(I18nKey)!;
const { get } = useApi();

const modules = ref<string[]>([]);
const isVisible = ref(false);
const isLoading = ref(false);
const isLoaded = ref(false);
const error = ref('');

async function toggle() {
  error.value = '';

  if (isLoaded.value) {
    isVisible.value = !isVisible.value;
    return;
  }

  isLoading.value = true;
  try {
    modules.value = await get<string[]>('/api/platform/modules/loading-order');
    isLoaded.value = true;
    isVisible.value = true;
  } catch (err) {
    error.value = err instanceof ApiError ? err.message : t('moduleSequence.error');
  } finally {
    isLoading.value = false;
  }
}
</script>

<template>
  <div class="op-card__actions">
    <button class="btn btn--outline" :disabled="isLoading" @click="toggle">
      <i :class="isLoading ? 'fas fa-spinner fa-spin' : 'fas fa-eye'"></i>
      {{ isVisible ? t('moduleSequence.hideAction') : t('moduleSequence.showAction') }}
    </button>
  </div>

  <div v-if="isVisible" class="expand-section">
    <ol class="module-list">
      <li v-for="(name, index) in modules" :key="name">
        <span class="module-list__num">{{ index + 1 }}</span>
        <span class="module-list__name">{{ name }}</span>
      </li>
    </ol>
  </div>

  <div v-if="error" class="op-card__error">{{ error }}</div>
</template>
