// Whether this device is a driver's phone (and whose). Remembered on the device.
// Note: this picks a driver from the company list; proper per-driver sign-in comes with accounts.
import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import AsyncStorage from '@react-native-async-storage/async-storage';

const KEY = 'omniops:driverMode';
const DriverModeContext = createContext({ driver: null, ready: false, startDriverMode: () => {}, exitDriverMode: () => {} });

export function DriverModeProvider({ children }) {
  const [driver, setDriver] = useState(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    AsyncStorage.getItem(KEY)
      .then((raw) => setDriver(raw ? JSON.parse(raw) : null))
      .catch(() => setDriver(null))
      .finally(() => setReady(true));
  }, []);

  const startDriverMode = useCallback(async (next) => {
    await AsyncStorage.setItem(KEY, JSON.stringify(next));
    setDriver(next);
  }, []);

  const exitDriverMode = useCallback(async () => {
    await AsyncStorage.removeItem(KEY);
    setDriver(null);
  }, []);

  const value = useMemo(() => ({ driver, ready, startDriverMode, exitDriverMode }), [driver, ready, startDriverMode, exitDriverMode]);
  return <DriverModeContext.Provider value={value}>{children}</DriverModeContext.Provider>;
}

export const useDriverMode = () => useContext(DriverModeContext);
