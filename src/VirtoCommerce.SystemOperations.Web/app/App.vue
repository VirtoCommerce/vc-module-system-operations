<script setup lang="ts">
import { provide, ref, computed, onMounted } from 'vue';
import { useDialog, DialogKey } from './composables/useDialog';
import { useI18n, I18nKey } from './composables/useI18n';
import { useOperations } from './composables/useOperations';
import { useSystemInfo } from './composables/useSystemInfo';
import { ApiError } from './composables/useApi';
import VcDialog from './components/VcDialog.vue';
import SectionGroup from './components/SectionGroup.vue';
import OperationCard from './components/OperationCard.vue';
import ModuleLoadSequence from './components/ModuleLoadSequence.vue';
import SampleDataPackages from './components/SampleDataPackages.vue';
import PlatformBackup from './components/PlatformBackup.vue';
import PlatformRestore from './components/PlatformRestore.vue';
import PlatformInfo from './components/PlatformInfo.vue';
import DevToolsNav from './components/DevToolsNav.vue';

const i18n = useI18n();
const { t } = i18n;
provide(I18nKey, i18n);

const dialog = useDialog();
provide(DialogKey, dialog);

const { resetCache, restartPlatform, isResetting, isRestarting, resetError, restartError } =
  useOperations(dialog, t);

const { downloadPackage } = useSystemInfo();

// Theme: 'system' | 'light' | 'dark'
const THEME_KEY = 'vc-system-operations-theme';
type ThemeMode = 'system' | 'light' | 'dark';
const themeMode = ref<ThemeMode>('system');

function getSystemDark(): boolean {
  return window.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
}

function applyTheme(mode: ThemeMode) {
  themeMode.value = mode;
  const dark = mode === 'dark' || (mode === 'system' && getSystemDark());
  document.documentElement.setAttribute('data-theme', dark ? 'dark' : 'light');
  try { localStorage.setItem(THEME_KEY, mode); } catch { /* ignore */ }
}

function cycleTheme() {
  const order: ThemeMode[] = ['system', 'light', 'dark'];
  const next = order[(order.indexOf(themeMode.value) + 1) % order.length];
  applyTheme(next);
}

const themeIcon = computed(() => {
  if (themeMode.value === 'system') return 'fas fa-desktop';
  if (themeMode.value === 'dark') return 'fas fa-moon';
  return 'fas fa-sun';
});

const themeTooltip = computed(() => {
  if (themeMode.value === 'system') return t('theme.system');
  if (themeMode.value === 'dark') return t('theme.dark');
  return t('theme.light');
});

onMounted(() => {
  try {
    const stored = localStorage.getItem(THEME_KEY) as ThemeMode | null;
    if (stored === 'light' || stored === 'dark' || stored === 'system') {
      applyTheme(stored);
    } else {
      applyTheme('system');
    }
  } catch { applyTheme('system'); }

  // Listen for OS theme changes when in system mode
  window.matchMedia?.('(prefers-color-scheme: dark)').addEventListener('change', () => {
    if (themeMode.value === 'system') applyTheme('system');
  });
});

const isDownloadingPackage = ref(false);
const packageError = ref('');
const commandCopied = ref(false);

const vcBuildCommand = 'vc-build Install -PackageManifestPath vc-package.json -ProbingPath platform/app_data/modules -DiscoveryPath platform/modules';

async function copyCommand() {
  try {
    await navigator.clipboard.writeText(vcBuildCommand);
  } catch {
    const ta = document.createElement('textarea');
    ta.value = vcBuildCommand;
    ta.style.position = 'fixed';
    ta.style.opacity = '0';
    document.body.appendChild(ta);
    ta.select();
    document.execCommand('copy');
    document.body.removeChild(ta);
  }
  commandCopied.value = true;
  setTimeout(() => { commandCopied.value = false; }, 2000);
}

async function handleDownloadPackage() {
  packageError.value = '';
  isDownloadingPackage.value = true;
  try {
    await downloadPackage();
  } catch (err) {
    packageError.value = err instanceof ApiError ? err.message : t('downloadPackage.error');
  } finally {
    isDownloadingPackage.value = false;
  }
}
</script>

<template>
  <div class="page-header">
    <div class="page-header__row">
      <h1>{{ t('page.title') }}</h1>
      <button class="theme-toggle" :title="themeTooltip" @click="cycleTheme">
        <i :class="themeIcon"></i>
      </button>
    </div>
    <p>{{ t('page.subtitle') }}</p>
  </div>

  <!-- Dev Tools -->
  <DevToolsNav />

  <!-- Maintenance -->
  <SectionGroup :title="t('sections.maintenance')">
    <OperationCard
      icon="fas fa-info-circle"
      icon-color="blue"
      :title="t('platformInfo.title')"
    >
      <PlatformInfo />
    </OperationCard>

    <OperationCard
      icon="fas fa-eraser"
      icon-color="orange"
      :title="t('resetCache.title')"
      permission="cache:reset"
      :description="t('resetCache.description')"
      :scenario="t('resetCache.scenario')"
      variant="warning"
    >
      <div class="op-card__actions">
        <button class="btn btn--warning" :disabled="isResetting" @click="resetCache">
          <i :class="isResetting ? 'fas fa-spinner fa-spin' : 'fas fa-eraser'"></i>
          {{ isResetting ? t('resetCache.loading') : t('resetCache.action') }}
        </button>
      </div>
      <div v-if="resetError" class="op-card__error">{{ resetError }}</div>
    </OperationCard>

    <OperationCard
      icon="fas fa-bolt"
      icon-color="red"
      :title="t('restart.title')"
      permission="platform:module:manage"
      :description="t('restart.description')"
      :scenario="t('restart.scenario')"
      variant="danger"
    >
      <div class="op-card__actions">
        <button class="btn btn--danger" :disabled="isRestarting" @click="restartPlatform">
          <i :class="isRestarting ? 'fas fa-spinner fa-spin' : 'fas fa-bolt'"></i>
          {{ isRestarting ? t('restart.loading') : t('restart.action') }}
        </button>
      </div>
      <div v-if="restartError" class="op-card__error">{{ restartError }}</div>
    </OperationCard>
  </SectionGroup>

  <!-- Data -->
  <SectionGroup :title="t('sections.data')">
    <OperationCard
      icon="fas fa-database"
      icon-color="green"
      :title="t('sampleData.title')"
      permission="platform:import"
      :description="t('sampleData.description')"
      :scenario="t('sampleData.scenario')"
    >
      <SampleDataPackages />
    </OperationCard>

    <OperationCard
      icon="fas fa-upload"
      icon-color="blue"
      :title="t('backup.title')"
      permission="platform:export"
      :description="t('backup.description')"
      :scenario="t('backup.scenario')"
    >
      <PlatformBackup />
    </OperationCard>

    <OperationCard
      icon="fas fa-download"
      icon-color="purple"
      :title="t('restore.title')"
      permission="platform:import"
      :description="t('restore.description')"
      :scenario="t('restore.scenario')"
    >
      <PlatformRestore />
    </OperationCard>
  </SectionGroup>

  <!-- Diagnostics & Export -->
  <SectionGroup :title="t('sections.diagnostics')">
    <OperationCard
      icon="fas fa-file-code"
      icon-color="blue"
      :title="t('downloadPackage.title')"
      :description="t('downloadPackage.description')"
      :scenario="t('downloadPackage.scenario')"
    >
      <div class="op-card__actions">
        <button class="btn btn--primary" :disabled="isDownloadingPackage" @click="handleDownloadPackage">
          <i :class="isDownloadingPackage ? 'fas fa-spinner fa-spin' : 'fas fa-file-code'"></i>
          {{ t('downloadPackage.action') }}
        </button>
      </div>
      <div class="code-hint">
        <div class="code-hint__label">{{ t('downloadPackage.restoreHint') }}</div>
        <div class="code-hint__block">
          <code class="code-hint__code">vc-build Install -PackageManifestPath vc-package.json -ProbingPath platform/app_data/modules -DiscoveryPath platform/modules</code>
          <button class="code-hint__copy" :title="t('platformInfo.copy')" @click="copyCommand">
            <i :class="commandCopied ? 'fas fa-check' : 'fas fa-copy'"></i>
          </button>
        </div>
      </div>
      <div v-if="packageError" class="op-card__error">{{ packageError }}</div>
    </OperationCard>

    <OperationCard
      icon="fas fa-sort-numeric-down"
      icon-color="purple"
      :title="t('moduleSequence.title')"
      permission="platform:module:manage"
      :description="t('moduleSequence.description')"
      :scenario="t('moduleSequence.scenario')"
    >
      <ModuleLoadSequence />
    </OperationCard>
  </SectionGroup>

  <VcDialog />
</template>
