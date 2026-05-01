// Reactive accessor for platform settings declared in this module's
// `module.manifest <settings>` block. Two scopes are supported:
//
//   global       (default) — shared across all admins/tenants. Routed to
//                  /api/platform/settings/v2/global/values?moduleId=<id>.
//                  Requires `platform:setting:read` / `:update` permission.
//   UserProfile  — per-user. Routed to
//                  /api/platform/settings/v2/me/values?moduleId=<id>.
//                  Requires authentication only (no extra permission for
//                  own profile). Anonymous → 401.
//
// One round-trip: the platform's /values endpoint accepts a `moduleId`
// query filter and returns each registered setting with its persisted
// value or default already filled in (modifiedOnly=false). That means
// callers don't need a parallel /schema fetch just to know which keys
// belong to this module — the filtered values blob is the answer.
// If you need full schema metadata (displayName, allowedValues,
// valueType) — e.g. to render a settings editor — call
// `/v2/<scope>/schema?moduleId=…` directly; this composable is the
// read-and-use shortcut that doesn't.
//
// See docs/developer-guide/manifest-settings.md (in vc-platform) for the
// declarative-settings story this composable consumes, including §4a
// for the per-user scope.
//
// `load()` returns a freshly-fetched snapshot; the host can call it on
// app start (eager) or lazily on first access. The returned `values`
// ref updates in place after each `save()` so any component that binds
// to it re-renders automatically.

import { ref, type Ref } from 'vue';
import { useApi, ApiError } from './useApi';

export type SettingScope = 'global' | 'UserProfile';

export interface UseModuleSettingsOptions {
  /**
   * Which storage tier to read/write. Defaults to 'global'. Pick
   * 'UserProfile' for per-user preferences (theme, layout density, etc.)
   * — values are stored under the authenticated caller's profile and
   * surfaced via /api/platform/settings/v2/me/*.
   */
  scope?: SettingScope;
}

export interface UseModuleSettingsResult {
  /** Current values (effective: persisted overrides default), keyed by setting name. */
  values: Ref<Record<string, unknown>>;
  /** Whether load() has completed at least once. */
  loaded: Ref<boolean>;
  /** Last error encountered by load() / save(); null on success. */
  error: Ref<ApiError | null>;
  /** Refetch values. Idempotent. */
  load(): Promise<void>;
  /** Persist one or more setting values. Updates `values` on success. */
  save(updates: Record<string, unknown>): Promise<void>;
  /** Effective value for a single setting: persisted/default → caller fallback. */
  get<T = unknown>(name: string, fallback?: T): T;
}

export function useModuleSettings(
  moduleId: string,
  options: UseModuleSettingsOptions = {},
): UseModuleSettingsResult {
  const scope: SettingScope = options.scope ?? 'global';
  // /global/values for shared settings; /me/values for per-user settings.
  // Identical response shapes — only the URL prefix differs.
  const urlPrefix = scope === 'UserProfile'
    ? '/api/platform/settings/v2/me'
    : '/api/platform/settings/v2/global';

  const { get: apiGet, post: apiPost } = useApi();
  const values = ref<Record<string, unknown>>({});
  const loaded = ref(false);
  const error = ref<ApiError | null>(null);

  async function load() {
    error.value = null;
    try {
      // moduleId scopes the response server-side: only descriptors with
      // matching ModuleId come back. modifiedOnly=false means the dict
      // includes every setting in scope with its default value filled
      // in if nothing is persisted — single source of truth, no schema
      // round-trip needed.
      const valuesResp = await apiGet<Record<string, unknown>>(
        `${urlPrefix}/values?modifiedOnly=false&moduleId=${encodeURIComponent(moduleId)}`,
      );
      values.value = valuesResp ?? {};
      loaded.value = true;
    } catch (err) {
      error.value = err instanceof ApiError ? err : new ApiError(0, String(err));
      throw error.value;
    }
  }

  async function save(updates: Record<string, unknown>) {
    error.value = null;
    try {
      await apiPost(`${urlPrefix}/values`, updates);
      // Optimistically merge so consumers see the new values without a
      // second round-trip.
      values.value = { ...values.value, ...updates };
    } catch (err) {
      error.value = err instanceof ApiError ? err : new ApiError(0, String(err));
      throw error.value;
    }
  }

  function getEffective<T>(name: string, fallback?: T): T {
    // The server fills defaults into the response when modifiedOnly=false,
    // so a present key is the source of truth (persisted or default).
    // The caller-supplied fallback only matters before load() resolves
    // (or if load() failed).
    return name in values.value ? (values.value[name] as T) : (fallback as T);
  }

  return { values, loaded, error, load, save, get: getEffective };
}
