// Driver app languages. Each driver's phone uses its own language (from the phone, or chosen in the app),
// independent of the office. Missing keys fall back to English.
import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { getLocales } from 'expo-localization';
import en from './en';
import mk from './mk';
import sq from './sq';
import sr from './sr';
import bg from './bg';
import ro from './ro';
import de from './de';
import tr from './tr';
import pl from './pl';

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
];

const KEY = 'omniops:language';
// Serbian, Croatian, Bosnian and Montenegrin speakers read the same Latin text.
const ALIASES = { hr: 'sr', bs: 'sr', cnr: 'sr', sh: 'sr', me: 'sr' };

/** The supported language closest to the phone's settings. */
export function deviceLanguage(locales = safeLocales()) {
  for (const l of locales) {
    const code = ALIASES[l.languageCode] ?? l.languageCode;
    if (LANGUAGES.some((x) => x.code === code)) return code;
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

/** Looks up `key` in `code`, falling back to English, and fills in {placeholders}. */
export function translate(code, key, vars) {
  const strings = LANGUAGES.find((l) => l.code === code)?.strings ?? en;
  const template = strings[key] ?? en[key] ?? key;
  return vars ? template.replace(/\{(\w+)\}/g, (m, name) => (vars[name] ?? m)) : template;
}

const LanguageContext = createContext({ code: 'en', t: (k, v) => translate('en', k, v), setLanguage: () => {} });

export function LanguageProvider({ children }) {
  const [code, setCode] = useState(deviceLanguage);
  useEffect(() => {
    AsyncStorage.getItem(KEY).then((saved) => saved && LANGUAGES.some((l) => l.code === saved) && setCode(saved)).catch(() => {});
  }, []);
  const setLanguage = useCallback((next) => {
    setCode(next);
    AsyncStorage.setItem(KEY, next).catch(() => {});
  }, []);
  const value = useMemo(() => ({
    code,
    setLanguage,
    t: (key, vars) => translate(code, key, vars),
    /** Clock time in the language's format, e.g. 14:05. */
    time: (iso) => new Date(iso).toLocaleTimeString(code, { hour: '2-digit', minute: '2-digit' }),
    dateTime: (iso) => new Date(iso).toLocaleString(code, { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' }),
  }), [code, setLanguage]);
  return <LanguageContext.Provider value={value}>{children}</LanguageContext.Provider>;
}

export const useLanguage = () => useContext(LanguageContext);
