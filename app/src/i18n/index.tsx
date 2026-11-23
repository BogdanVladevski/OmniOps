// Driver app languages. Each phone uses its own language (from the phone, or chosen in the app), independent of the
// office. Missing keys fall back to English; every language must have every key (checked by the type system).
import AsyncStorage from '@react-native-async-storage/async-storage';
import { getLocales } from 'expo-localization';
import { createContext, use, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';
import { bg } from './bg';
import { de } from './de';
import { en, type StringKey } from './en';
import { mk } from './mk';
import { pl } from './pl';
import { ro } from './ro';
import { sq } from './sq';
import { sr } from './sr';
import { tr } from './tr';

export const LANGUAGES = [
  { code: 'en', name: 'English', strings: en },
  { code: 'mk', name: 'Македонски', strings: mk },
  { code: 'sq', name: 'Shqip', strings: sq },
  { code: 'sr', name: 'Srpski / Hrvatski / Bosanski', strings: sr },
  { code: 'bg', name: 'Български', strings: bg },
  { code: 'ro', name: 'Română', strings: ro },
  { code: 'de', name: 'Deutsch', strings: de },
  { code: 'tr', name: 'Türkçe', strings: tr },
  { code: 'pl', name: 'Polski', strings: pl },
] as const;

export type LanguageCode = (typeof LANGUAGES)[number]['code'];
export type { StringKey };

const KEY = 'omniops:language';
// Serbian, Croatian, Bosnian and Montenegrin speakers read the same Latin text.
const ALIASES: Record<string, LanguageCode> = { hr: 'sr', bs: 'sr', cnr: 'sr', sh: 'sr', me: 'sr' };

const isSupported = (code: string): code is LanguageCode => LANGUAGES.some((l) => l.code === code);

/** The supported language closest to the phone's settings. */
export function deviceLanguage(locales: { languageCode: string | null }[] = safeLocales()): LanguageCode {
  for (const locale of locales) {
    const code = locale.languageCode ? (ALIASES[locale.languageCode] ?? locale.languageCode) : null;
    if (code && isSupported(code)) return code;
  }
  return 'en';
}

function safeLocales() {
  try {
    return getLocales();
  } catch {
    return [];
  }
}

/** `key` in `code`, falling back to English, with {placeholders} filled in. */
export function translate(code: string, key: StringKey, vars?: Record<string, string | number>): string {
  const strings: Record<string, string> = LANGUAGES.find((l) => l.code === code)?.strings ?? en;
  const template = strings[key] ?? en[key] ?? key;
  return vars ? template.replace(/\{(\w+)\}/g, (m, name: string) => String(vars[name] ?? m)) : template;
}

interface LanguageValue {
  code: LanguageCode;
  setLanguage: (code: LanguageCode) => void;
  t: (key: StringKey, vars?: Record<string, string | number>) => string;
  /** Clock time in the language's format, e.g. 14:05. */
  time: (iso: string) => string;
  dateTime: (iso: string) => string;
}

const LanguageContext = createContext<LanguageValue | null>(null);

export function LanguageProvider({ children }: { children: ReactNode }) {
  const [code, setCode] = useState<LanguageCode>(() => deviceLanguage());
  useEffect(() => {
    AsyncStorage.getItem(KEY)
      .then((saved) => saved && isSupported(saved) && setCode(saved))
      .catch(() => {});
  }, []);
  const setLanguage = useCallback((next: LanguageCode) => {
    setCode(next);
    AsyncStorage.setItem(KEY, next).catch(() => {});
  }, []);
  const value = useMemo<LanguageValue>(
    () => ({
      code,
      setLanguage,
      t: (key, vars) => translate(code, key, vars),
      time: (iso) => new Date(iso).toLocaleTimeString(code, { hour: '2-digit', minute: '2-digit' }),
      dateTime: (iso) =>
        new Date(iso).toLocaleString(code, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }),
    }),
    [code, setLanguage],
  );
  return <LanguageContext value={value}>{children}</LanguageContext>;
}

export function useLanguage(): LanguageValue {
  const value = use(LanguageContext);
  if (!value) throw new Error('useLanguage must be used inside LanguageProvider');
  return value;
}
