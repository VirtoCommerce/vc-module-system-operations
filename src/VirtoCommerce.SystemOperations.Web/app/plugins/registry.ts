// Reactive registry for plugin-contributed cards.
//
// The registry is provided to App.vue via inject; App.vue subscribes to
// `cards` and `sections` (computed) to re-render whenever a plugin
// registers something new.

import { computed, readonly, ref, type InjectionKey } from 'vue';
import type {
  RegisterCardOptions,
  SystemOperationsHost,
  SystemOperationsSection,
} from './types';

interface RegisteredCard extends RegisterCardOptions {
  /** Which plugin id contributed this card (for de-dup, error reporting). */
  pluginId: string;
}

export interface PluginRegistry {
  /** Reactive read-only list of every registered card (any section). */
  cards: Readonly<{ value: ReadonlyArray<RegisteredCard> }>;
  /** Cards grouped by section (computed; sorted by order then pluginId). */
  bySection: Readonly<{ value: ReadonlyMap<SystemOperationsSection, ReadonlyArray<RegisteredCard>> }>;
  /** Returns true if the named section has at least one plugin card. */
  sectionHasContent(section: SystemOperationsSection): boolean;
  /** Builds a host facade scoped to a specific plugin id. */
  forPlugin(pluginId: string): SystemOperationsHost;
}

export const PluginRegistryKey: InjectionKey<PluginRegistry> = Symbol('SystemOperationsPluginRegistry');

const DEFAULT_SECTION: SystemOperationsSection = 'plugins';

export function createPluginRegistry(): PluginRegistry {
  const cards = ref<RegisteredCard[]>([]);

  const bySection = computed(() => {
    const map = new Map<SystemOperationsSection, RegisteredCard[]>();
    for (const card of cards.value) {
      const section = card.section ?? DEFAULT_SECTION;
      let bucket = map.get(section);
      if (!bucket) {
        bucket = [];
        map.set(section, bucket);
      }
      bucket.push(card);
    }
    for (const list of map.values()) {
      list.sort((a, b) => {
        const oa = a.order ?? 0;
        const ob = b.order ?? 0;
        if (oa !== ob) return oa - ob;
        return a.pluginId.localeCompare(b.pluginId);
      });
    }
    return map as ReadonlyMap<SystemOperationsSection, ReadonlyArray<RegisteredCard>>;
  });

  return {
    cards: readonly(cards) as PluginRegistry['cards'],
    bySection: bySection as PluginRegistry['bySection'],
    sectionHasContent(section) {
      return (bySection.value.get(section)?.length ?? 0) > 0;
    },
    forPlugin(pluginId): SystemOperationsHost {
      return {
        registerCard(card) {
          cards.value.push({ ...card, pluginId });
        },
      };
    },
  };
}
