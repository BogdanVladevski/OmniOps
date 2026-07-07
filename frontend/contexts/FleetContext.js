import React, { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import * as SignalR from '@microsoft/signalr';
import { parseFleetVehicles } from '../utils/fleetConfig';
import { fleetApi, geofenceApi, getApiConfig } from '../utils/api';
import {
  loadCachedAlerts,
  loadCachedVehicles,
  saveCachedAlerts,
  saveCachedVehicles,
  getLastSyncUtc,
  setLastSyncUtc,
} from '../utils/offlineStorage';
import { fetchMobileSync, flushSyncQueue } from '../utils/syncQueue';

const FleetContext = createContext(null);

function normalizeAlert(raw) {
  return {
    vehicleId: raw.vehicleId,
    alertType: raw.alertType ?? 'playbook',
    title: raw.title ?? raw.vehicleId,
    instructions: raw.instructions ?? raw.message ?? '',
    generatedAt: raw.generatedAt ?? new Date().toISOString(),
  };
}

function alertFingerprint(alert) {
  const generatedAt = alert.generatedAt ?? '';
  const body = alert.instructions ?? alert.message ?? '';
  return `${alert.vehicleId}|${alert.alertType ?? 'playbook'}|${generatedAt}|${body.slice(0, 120)}`;
}

export function FleetProvider({ children }) {
  const vehicleIds = useMemo(() => parseFleetVehicles(), []);
  const { apiBaseUrl, apiToken, defaultFleetId } = getApiConfig();
  const [vehicles, setVehicles] = useState({});
  const [alerts, setAlerts] = useState([]);
  const [geofences, setGeofences] = useState([]);
  const [fleetMeta, setFleetMeta] = useState(null);
  const [dbVehicles, setDbVehicles] = useState([]);
  const [selectedVehicleId, setSelectedVehicleId] = useState(vehicleIds[0] ?? null);
  const [connectionStatus, setConnectionStatus] = useState('Connecting to tracking engine...');
  const [syncStatus, setSyncStatus] = useState('idle');
  const seenAlertsRef = useRef(new Set());

  useEffect(() => {
    (async () => {
      const [cachedVehicles, cachedAlerts] = await Promise.all([
        loadCachedVehicles(),
        loadCachedAlerts(),
      ]);
      if (Object.keys(cachedVehicles).length > 0) setVehicles(cachedVehicles);
      if (cachedAlerts.length > 0) {
        setAlerts(cachedAlerts);
        seenAlertsRef.current = new Set(cachedAlerts.map((a) => a.id));
      }
    })();
  }, []);

  const mergeTelemetry = useCallback((data) => {
    if (!data?.vehicleId) return;
    setVehicles((prev) => {
      const next = { ...prev, [data.vehicleId]: data };
      saveCachedVehicles(next);
      return next;
    });
  }, []);

  const appendAlert = useCallback((raw) => {
    const alert = normalizeAlert(raw);
    const fingerprint = alertFingerprint(alert);
    if (seenAlertsRef.current.has(fingerprint)) return;
    seenAlertsRef.current.add(fingerprint);

    setAlerts((prev) => {
      if (prev.some((item) => item.id === fingerprint)) return prev;
      const next = [{ ...alert, id: fingerprint }, ...prev];
      saveCachedAlerts(next);
      return next;
    });
  }, []);

  const loadFleetSnapshot = useCallback(async () => {
    if (!apiBaseUrl) return;
    try {
      const headers = apiToken ? { Authorization: `Bearer ${apiToken}` } : {};
      const response = await fetch(`${apiBaseUrl}/api/telemetry/fleet`, { headers });
      if (!response.ok) return;
      const payload = await response.json();
      const next = {};
      for (const item of payload.vehicles ?? []) {
        next[item.vehicleId] = item;
      }
      setVehicles((prev) => {
        const merged = { ...prev, ...next };
        saveCachedVehicles(merged);
        return merged;
      });
    } catch (error) {
      console.error('Failed to load fleet snapshot', error);
      const cached = await loadCachedVehicles();
      if (Object.keys(cached).length > 0) setVehicles(cached);
    }
  }, [apiBaseUrl, apiToken]);

  const loadGeofences = useCallback(async () => {
    try {
      const data = await geofenceApi.list(defaultFleetId);
      setGeofences(data ?? []);
    } catch (error) {
      console.error('Failed to load geofences', error);
    }
  }, [defaultFleetId]);

  const loadFleetMeta = useCallback(async () => {
    try {
      const [stats, vehiclesList] = await Promise.all([
        fleetApi.statistics(defaultFleetId),
        fleetApi.vehicles(defaultFleetId),
      ]);
      setFleetMeta(stats);
      setDbVehicles(vehiclesList ?? []);
    } catch (error) {
      console.error('Failed to load fleet metadata', error);
    }
  }, [defaultFleetId]);

  useEffect(() => {
    if (!apiBaseUrl) {
      setConnectionStatus('Missing EXPO_PUBLIC_API_URL. Create frontend/.env');
      return;
    }

    const hubUrl = `${apiBaseUrl}/api/stream/telemetry`;
    const connection = new SignalR.HubConnectionBuilder()
      .withUrl(hubUrl, apiToken ? { accessTokenFactory: () => apiToken } : undefined)
      .withAutomaticReconnect()
      .configureLogging(SignalR.LogLevel.Information)
      .build();

    connection.on('ReceiveTelemetryUpdate', mergeTelemetry);
    connection.on('ReceivePlaybookInstructions', appendAlert);
    connection.on('ReceiveAlert', appendAlert);
    let cancelled = false;

    const runSync = async () => {
      try {
        setSyncStatus('syncing');
        await flushSyncQueue();
        const since = await getLastSyncUtc();
        await fetchMobileSync(since ?? undefined);
        await setLastSyncUtc(new Date().toISOString());
        setSyncStatus('idle');
      } catch {
        setSyncStatus('offline');
      }
    };

    const start = async () => {
      try {
        await Promise.all([loadFleetSnapshot(), loadGeofences(), loadFleetMeta()]);
        await connection.start();
        if (cancelled) return;
        setConnectionStatus('Live fleet connected.');
        await connection.invoke('WatchFleet');
        await runSync();
      } catch (error) {
        if (!cancelled) {
          setConnectionStatus(
            `Cannot reach API at ${apiBaseUrl} — check Docker/API is running and phone is on same Wi‑Fi.`,
          );
          setSyncStatus('offline');
          console.error('SignalR error:', error);
        }
      }
    };

    connection.onreconnected(async () => {
      setConnectionStatus('Live fleet connected.');
      await runSync();
    });

    start();
    return () => {
      cancelled = true;
      connection.off('ReceiveTelemetryUpdate', mergeTelemetry);
      connection.off('ReceivePlaybookInstructions', appendAlert);
      connection.off('ReceiveAlert', appendAlert);
      connection.stop();
    };
  }, [apiBaseUrl, apiToken, appendAlert, loadFleetSnapshot, loadGeofences, loadFleetMeta, mergeTelemetry]);

  const value = useMemo(
    () => ({
      vehicleIds,
      vehicles,
      alerts,
      geofences,
      fleetMeta,
      dbVehicles,
      defaultFleetId,
      selectedVehicleId,
      setSelectedVehicleId,
      connectionStatus,
      syncStatus,
      refreshFleetMeta: loadFleetMeta,
    }),
    [vehicleIds, vehicles, alerts, geofences, fleetMeta, dbVehicles, defaultFleetId, selectedVehicleId, connectionStatus, syncStatus, loadFleetMeta],
  );

  return <FleetContext.Provider value={value}>{children}</FleetContext.Provider>;
}

export function useFleet() {
  const context = useContext(FleetContext);
  if (!context) throw new Error('useFleet must be used within FleetProvider');
  return context;
}
