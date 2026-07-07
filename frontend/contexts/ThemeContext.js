import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useColorScheme } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { paletteForMode } from '../theme/tokens';

const STORAGE_KEY = '@omniops/theme_mode';

const ThemeContext = createContext({
  colors: paletteForMode(false),
  isDark: false,
  themeMode: 'system',
  setThemeMode: () => {},
  toggle: () => {},
});

export function ThemeProvider({ children }) {
  const system = useColorScheme();
  const [themeMode, setThemeModeState] = useState('system');
  const [loaded, setLoaded] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const stored = await AsyncStorage.getItem(STORAGE_KEY);
        if (stored === 'light' || stored === 'dark' || stored === 'system') {
          setThemeModeState(stored);
        }
      } catch {
        // ignore
      } finally {
        setLoaded(true);
      }
    })();
  }, []);

  const isDark = themeMode === 'system' ? system === 'dark' : themeMode === 'dark';
  const colors = paletteForMode(isDark);

  const setThemeMode = useCallback(async (mode) => {
    setThemeModeState(mode);
    try {
      await AsyncStorage.setItem(STORAGE_KEY, mode);
    } catch {
      // ignore
    }
  }, []);

  const toggle = useCallback(() => {
    const next = isDark ? 'light' : 'dark';
    setThemeMode(next);
  }, [isDark, setThemeMode]);

  const value = useMemo(
    () => ({
      colors,
      isDark,
      themeMode,
      setThemeMode,
      toggle,
      ready: loaded,
    }),
    [colors, isDark, themeMode, setThemeMode, toggle, loaded],
  );

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}

export function useTheme() {
  return useContext(ThemeContext);
}
