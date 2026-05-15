<script setup lang="ts">
import { provide, ref, computed, onMounted, inject } from 'vue';
import { useDialog, DialogKey } from './composables/useDialog';
import { useI18n, I18nKey } from './composables/useI18n';
import { useOperations } from './composables/useOperations';
import { useSystemInfo } from './composables/useSystemInfo';
import { ApiError } from './composables/useApi';
import { useModuleSettings } from './composables/useModuleSettings';
import VcDialog from './components/VcDialog.vue';
import SectionGroup from './components/SectionGroup.vue';
import OperationCard from './components/OperationCard.vue';
import ModuleLoadSequence from './components/ModuleLoadSequence.vue';
import SampleDataPackages from './components/SampleDataPackages.vue';
import PlatformBackup from './components/PlatformBackup.vue';
import PlatformRestore from './components/PlatformRestore.vue';
import PlatformInfo from './components/PlatformInfo.vue';
import DevToolsNav from './components/DevToolsNav.vue';
import { PluginRegistryKey } from './plugins/registry';
import type { SystemOperationsSection } from './plugins/types';

// Plugin registry (provided by main.ts). Lookup helper returns the cards
// contributed for a section in the order plugins should render.
const pluginRegistry = inject(PluginRegistryKey)!;
function pluginCards(section: SystemOperationsSection) {
  return pluginRegistry.bySection.value.get(section) ?? [];
}

const i18n = useI18n();
const { t } = i18n;
provide(I18nKey, i18n);

const dialog = useDialog();
provide(DialogKey, dialog);

// Module settings declared in module.manifest <settings>. Two scopes:
//   • global settings (RestartTimeoutSeconds, AllowDestructiveOperations)
//     come from /api/platform/settings/v2/global/* — admin-tuned, shared.
//   • UserProfile settings (DefaultTheme) come from /api/platform/settings/v2/me/*
//     — per-user, persisted to the caller's profile across browsers/devices.
// The composable picks the right endpoint family based on `scope`.
const MODULE_ID = 'VirtoCommerce.SystemOperations';
const SETTING_DEFAULT_THEME = `${MODULE_ID}.DefaultTheme`;
const SETTING_ALLOW_DESTRUCTIVE = `${MODULE_ID}.AllowDestructiveOperations`;
const SETTING_RESTART_TIMEOUT = `${MODULE_ID}.RestartTimeoutSeconds`;
const globalSettings = useModuleSettings(MODULE_ID);
const userSettings = useModuleSettings(MODULE_ID, { scope: 'UserProfile' });

// `useOperations` consults the timeout getter on each restart click, so an
// admin updating the value mid-session takes effect on the next click
// without remounting.
const { resetCache, restartPlatform, isResetting, isRestarting, resetError, restartError } =
  useOperations(dialog, t, {
    restartTimeoutSeconds: () => globalSettings.get<number>(SETTING_RESTART_TIMEOUT, 120),
  });

// Production safety: when false, hide Reset Cache + Restart Platform cards.
// Defaults to true (i.e. allow) so existing installs keep current behaviour.
const allowDestructiveOperations = computed(() =>
  globalSettings.get<boolean>(SETTING_ALLOW_DESTRUCTIVE, true),
);

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
  // localStorage is a per-browser cache used to avoid FOUC on next mount;
  // UserProfile is the cross-device source of truth (persisted via save).
  try { localStorage.setItem(THEME_KEY, mode); } catch { /* ignore */ }
}

async function cycleTheme() {
  const order: ThemeMode[] = ['system', 'light', 'dark'];
  const next = order[(order.indexOf(themeMode.value) + 1) % order.length];
  applyTheme(next);
  // Persist to the caller's UserProfile so the choice follows them across
  // browsers/devices. Best-effort: a failed save (offline / 401) keeps the
  // local change applied — localStorage will still serve the next mount on
  // this browser.
  try {
    await userSettings.save({ [SETTING_DEFAULT_THEME]: next });
  } catch { /* keep the local change even if the server didn't accept it */ }
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

onMounted(async () => {
  // Apply a synchronous theme immediately (localStorage if present, else
  // 'system') so the user never sees an unstyled flash. Once UserProfile
  // settings load, the persisted server-side preference (the source of
  // truth across browsers/devices) takes over if it differs.
  let storedTheme: ThemeMode | null = null;
  try {
    const v = localStorage.getItem(THEME_KEY);
    if (v === 'light' || v === 'dark' || v === 'system') {
      storedTheme = v;
    }
  } catch { /* ignore */ }
  applyTheme(storedTheme ?? 'system');

  // Listen for OS theme changes when in system mode
  window.matchMedia?.('(prefers-color-scheme: dark)').addEventListener('change', () => {
    if (themeMode.value === 'system') applyTheme('system');
  });

  // Load global settings (RestartTimeout, AllowDestructive). Best-effort.
  try {
    await globalSettings.load();
  } catch {
    // /global/* unreachable / 401 — fall through to defaults.
  }

  // Load the caller's UserProfile theme preference. Cross-device source of
  // truth — if it differs from the localStorage cache we just rendered,
  // switch to it (the user changed their theme on another browser/device).
  // Falls back to the schema default ('system') when no value is persisted.
  try {
    await userSettings.load();
    const persisted = userSettings.get<ThemeMode>(SETTING_DEFAULT_THEME, 'system');
    if (
      (persisted === 'light' || persisted === 'dark' || persisted === 'system') &&
      persisted !== themeMode.value
    ) {
      applyTheme(persisted);
    }
  } catch {
    // /me/* unreachable / 401 — keep whatever theme is applied.
  }
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
      v-if="allowDestructiveOperations"
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
      v-if="allowDestructiveOperations"
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

    <!-- Plugin contributions (section: maintenance) -->
    <OperationCard
      v-for="card in pluginCards('maintenance')"
      :key="`maintenance-${card.pluginId}`"
      v-bind="card.props"
    >
      <component :is="card.component" />
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

    <!-- Plugin contributions (section: data) -->
    <OperationCard
      v-for="card in pluginCards('data')"
      :key="`data-${card.pluginId}`"
      v-bind="card.props"
    >
      <component :is="card.component" />
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

    <!-- Plugin contributions (section: diagnostics) -->
    <OperationCard
      v-for="card in pluginCards('diagnostics')"
      :key="`diagnostics-${card.pluginId}`"
      v-bind="card.props"
    >
      <component :is="card.component" />
    </OperationCard>
  </SectionGroup>

  <!-- Plugins — only renders when at least one plugin contributes a "plugins"-section card. -->
  <SectionGroup v-if="pluginRegistry.sectionHasContent('plugins')" :title="t('sections.plugins')">
    <OperationCard
      v-for="card in pluginCards('plugins')"
      :key="`plugins-${card.pluginId}`"
      v-bind="card.props"
    >
      <component :is="card.component" />
    </OperationCard>
  </SectionGroup>

  <VcDialog />
</template>
