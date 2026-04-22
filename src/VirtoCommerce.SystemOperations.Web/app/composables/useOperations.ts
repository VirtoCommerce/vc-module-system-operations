import { ref } from 'vue';
import { useApi, ApiError } from './useApi';
import type { useDialog } from './useDialog';

type TranslateFn = (key: string, params?: Record<string, string>) => string;

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

export function useOperations(dialog: ReturnType<typeof useDialog>, t: TranslateFn) {
  const { post, get } = useApi();
  const isResetting = ref(false);
  const isRestarting = ref(false);
  const resetError = ref('');
  const restartError = ref('');

  async function resetCache(): Promise<void> {
    resetError.value = '';
    const confirmed = await dialog.warning(
      t('resetCache.confirmTitle'),
      t('resetCache.confirmMessage'),
    );
    if (!confirmed) return;

    isResetting.value = true;
    try {
      await post('/api/platform-cache/reset');
      await dialog.success(t('resetCache.successTitle'), t('resetCache.successMessage'));
    } catch (err) {
      resetError.value = err instanceof ApiError ? err.message : t('errors.unexpected');
    } finally {
      isResetting.value = false;
    }
  }

  async function restartPlatform(): Promise<void> {
    restartError.value = '';
    const confirmed = await dialog.warning(
      t('restart.confirmTitle'),
      t('restart.confirmMessage'),
    );
    if (!confirmed) return;

    isRestarting.value = true;
    try {
      try {
        await post('/api/platform/modules/restart');
      } catch {
        // Restart may fail transiently as server shuts down
      }

      await delay(3000);

      const maxAttempts = 60;
      for (let i = 0; i < maxAttempts; i++) {
        try {
          await get('/api/platform/diagnostics/systeminfo');
          await dialog.success(t('restart.successTitle'), t('restart.successMessage'));
          return;
        } catch {
          await delay(2000);
        }
      }

      restartError.value = t('restart.timeoutError');
    } finally {
      isRestarting.value = false;
    }
  }

  return { resetCache, restartPlatform, isResetting, isRestarting, resetError, restartError };
}
