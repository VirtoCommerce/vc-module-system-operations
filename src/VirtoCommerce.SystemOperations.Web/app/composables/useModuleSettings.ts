// Reactive accessor for platform settings declared in this module's
// `module.manifest <settings>` block. Reads via the v2 settings API:
//
//   GET  /api/platform/settings/v2/global/schema?moduleId=…
//   GET  /api/platform/settings/v2/global/values
//   POST /api/platform/settings/v2/global/values
//
// See docs/developer-guide/manifest-settings.md (in vc-platform) for the
// declarative-settings story this composable consumes.
//
// `load()` returns a freshly-fetched snapshot; the host can call it on
// app start (eager) or lazily on first access. The returned `values`
// ref updates in place after each `save()` so any component that binds
// to it re-renders automatically.

import { ref, type Ref } from 'vue';
import { useApi, ApiError } from './useApi';

export type SettingValueType =
  | 'ShortText'
  | 'LongText'
  | 'Integer'
  | 'PositiveInteger'
  | 'Decimal'
  | 'DateTime'
  | 'Boolean'
  | 'SecureString'
  | 'Json';

export interface SettingSchema {
  name: string;
  groupName: string;
  displayName?: string;
  valueType: SettingValueType;
  defaultValue?: unknown;
  allowedValues?: unknown[];
  isRequired?: boolean;
  isHidden?: boolean;
  isPublic?: boolean;
  restartRequired?: boolean;
  moduleId?: string;
}

export interface UseModuleSettingsResult {
  /** Schema for every setting registered against {moduleId}. */
  schema: Ref<SettingSchema[]>;
  /** Current values (effective: persisted overrides default). */
  values: Ref<Record<string, unknown>>;
  /** Whether load() has completed at least once. */
  loaded: Ref<boolean>;
  /** Last error encountered by load() / save(); null on success. */
  error: Ref<ApiError | null>;
  /** Refetch schema + values. Idempotent. */
  load(): Promise<void>;
  /** Persist one or more setting values. Updates `values` on success. */
  save(updates: Record<string, unknown>): Promise<void>;
  /** Effective value for a single setting: persisted → default → fallback. */
  get<T = unknown>(name: string, fallback?: T): T;
}

export function useModuleSettings(moduleId: string): UseModuleSettingsResult {
  const { get: apiGet, post: apiPost } = useApi();
  const schema = ref<SettingSchema[]>([]);
  const values = ref<Record<string, unknown>>({});
  const loaded = ref(false);
  const error = ref<ApiError | null>(null);

  async function load() {
    error.value = null;
    try {
      const [schemaResp, valuesResp] = await Promise.all([
        apiGet<SettingSchema[]>(
          `/api/platform/settings/v2/global/schema?moduleId=${encodeURIComponent(moduleId)}`,
        ),
        apiGet<Record<string, unknown>>(
          `/api/platform/settings/v2/global/values?modifiedOnly=false`,
        ),
      ]);

      schema.value = schemaResp ?? [];

      // Filter the global values blob down to just settings owned by this
      // module so callers can't accidentally write into another module's
      // namespace.
      const owned = new Set(schema.value.map((s) => s.name));
      values.value = Object.fromEntries(
        Object.entries(valuesResp ?? {}).filter(([k]) => owned.has(k)),
      );

      loaded.value = true;
    } catch (err) {
      error.value = err instanceof ApiError ? err : new ApiError(0, String(err));
      throw error.value;
    }
  }

  async function save(updates: Record<string, unknown>) {
    error.value = null;
    try {
      await apiPost('/api/platform/settings/v2/global/values', updates);
      // Optimistically merge so consumers see the new values without a
      // second round-trip.
      values.value = { ...values.value, ...updates };
    } catch (err) {
      error.value = err instanceof ApiError ? err : new ApiError(0, String(err));
      throw error.value;
    }
  }

  function getEffective<T>(name: string, fallback?: T): T {
    if (name in values.value) {
      return values.value[name] as T;
    }
    const declared = schema.value.find((s) => s.name === name);
    if (declared && declared.defaultValue !== undefined && declared.defaultValue !== null) {
      return declared.defaultValue as T;
    }
    return fallback as T;
  }

  return { schema, values, loaded, error, load, save, get: getEffective };
}
