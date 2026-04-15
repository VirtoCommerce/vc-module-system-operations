import { ref, type InjectionKey } from 'vue';

type Messages = Record<string, string>;

export const I18nKey: InjectionKey<ReturnType<typeof useI18n>> = Symbol('i18n');

// Statically import all locale JSON files — Vite resolves at build time
const localeModules = import.meta.glob<{ default: Record<string, unknown> }>('../locales/*.json', { eager: true });

function flattenMessages(obj: Record<string, unknown>, prefix = ''): Messages {
  const result: Messages = {};
  for (const [key, value] of Object.entries(obj)) {
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === 'string') {
      result[path] = value;
    } else if (typeof value === 'object' && value !== null) {
      Object.assign(result, flattenMessages(value as Record<string, unknown>, path));
    }
  }
  return result;
}

function detectLocale(): string {
  // 1. angular-translate stores active language in localStorage (set via .useLocalStorage() in i18n.js)
  try {
    const stored = localStorage.getItem('NG_TRANSLATE_LANG_KEY');
    if (stored) {
      // Value may be quoted: "en" or just en
      const lang = stored.replace(/^"|"$/g, '').split('-')[0];
      if (lang) return lang;
    }
  } catch {
    // localStorage blocked — ignore
  }

  // 2. Check parent window's html lang attribute
  try {
    const parentDoc = window.parent?.document;
    if (parentDoc) {
      const htmlLang = parentDoc.documentElement.lang;
      if (htmlLang) return htmlLang.split('-')[0];
    }
  } catch {
    // Cross-origin — ignore
  }

  // 3. Browser language
  return navigator.language?.split('-')[0] || 'en';
}

function loadMessages(locale: string): Messages {
  // Find matching locale file from eager imports
  const key = Object.keys(localeModules).find((k) => k.includes(`/${locale}.json`));
  if (key) {
    return flattenMessages(localeModules[key].default);
  }

  // Fallback to English
  const enKey = Object.keys(localeModules).find((k) => k.includes('/en.json'));
  if (enKey) {
    return flattenMessages(localeModules[enKey].default);
  }

  return {};
}

export function useI18n() {
  const locale = ref(detectLocale());
  const messages = ref<Messages>(loadMessages(locale.value));

  // Fallback English messages for missing keys
  const fallback = locale.value !== 'en' ? loadMessages('en') : messages.value;

  function t(key: string, params?: Record<string, string>): string {
    let text = messages.value[key] ?? fallback[key] ?? key;

    if (params) {
      for (const [param, value] of Object.entries(params)) {
        text = text.replace(`{${param}}`, value);
      }
    }

    return text;
  }

  return { t, locale };
}
