import React, { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import * as SignalR from '@microsoft/signalr';
import { parseFleetVehicles } from '../utils/fleetConfig';

const FleetContext = createContext(null);

function alertFingerprint(alert) {
  const generatedAt = alert.generatedAt ?? '';
  const instructions = alert.instructions ?? '';
  return `${alert.vehicleId}|${generatedAt}|${instructions.slice(0, 120)}`;
}

export function FleetProvider({ children }) {
  const vehicleIds = useMemo(() => parseFleetVehicles(), []);
  const [vehicles, setVehicles] = useState({});
  const [alerts, setAlerts] = useState([]);
  const [selectedVehicleId, setSelectedVehicleId] = useState(vehicleIds[0] ?? null);
  const [connectionStatus, setConnectionStatus] = useState('Connecting to tracking engine...');
  const alertIdRef = useRef(0);
  const seenAlertsRef = useRef(new Set());

  const apiBaseUrl = process.env.EXPO_PUBLIC_API_URL;
  const apiToken = process.env.EXPO_PUBLIC_API_TOKEN;

  const mergeTelemetry = useCallback((data) => {
    if (!data?.vehicleId) return;
    setVehicles((prev) => ({ ...prev, [data.vehicleId]: data }));
  }, []);

  const appendAlert = useCallback((alert) => {
    const fingerprint = alertFingerprint(alert);
    if (seenAlertsRef.current.has(fingerprint)) {
      return;
    }
    seenAlertsRef.current.add(fingerprint);

    alertIdRef.current += 1;
    setAlerts((prev) => [
      {
        id: `alert-${alertIdRef.current}`,
        vehicleId: alert.vehicleId,
        instructions: alert.instructions,
        generatedAt: alert.generatedAt ?? new Date().toISOString(),
      },
      ...prev,
    ]);
  }, []);
  const loadFleetSnapshot = useCallback(async () => {
    if (!apiBaseUrl) return;

    try {
      const headers = apiToken ? { Authorization: `Bearer ${apiToken}` } : {};
      const response = await fetch(`${apiBaseUrl.replace(/\/+$/, '')}/api/telemetry/fleet`, { headers });
      if (!response.ok) return;

      const payload = await response.json();
      const next = {};
      for (const item of payload.vehicles ?? []) {
        next[item.vehicleId] = item;
      }
      setVehicles((prev) => ({ ...prev, ...next }));
    } catch (error) {
      console.error('Failed to load fleet snapshot', error);
    }
  }, [apiBaseUrl, apiToken]);

  useEffect(() => {
    if (!apiBaseUrl) {
      setConnectionStatus('Missing EXPO_PUBLIC_API_URL. Create frontend/.env');
      return;
    }

    const hubUrl = `${apiBaseUrl.replace(/\/+$/, '')}/api/stream/telemetry`;
    const connection = new SignalR.HubConnectionBuilder()
      .withUrl(hubUrl, apiToken ? { accessTokenFactory: () => apiToken } : undefined)
      .withAutomaticReconnect()
      .configureLogging(SignalR.LogLevel.Information)
      .build();

    connection.on('ReceiveTelemetryUpdate', mergeTelemetry);
    connection.on('ReceivePlaybookInstructions', appendAlert);
    let cancelled = false;

    const start = async () => {
      try {
        await loadFleetSnapshot();
        await connection.start();
        if (cancelled) return;

        setConnectionStatus('Live fleet connected.');
        // Fleet group receives all telemetry + playbook broadcasts; avoid also
        // joining per-vehicle groups or the same event is delivered twice.
        await connection.invoke('WatchFleet');      } catch (error) {
        if (!cancelled) {
          setConnectionStatus('Fleet link offline.');
          console.error('SignalR error:', error);
        }
      }
    };

    start();

    return () => {
      cancelled = true;
      connection.stop();
    };
  }, [apiBaseUrl, apiToken, appendAlert, loadFleetSnapshot, mergeTelemetry]);
  const value = useMemo(
    () => ({
      vehicleIds,
      vehicles,
      alerts,
      selectedVehicleId,
      setSelectedVehicleId,
      connectionStatus,
    }),
    [vehicleIds, vehicles, alerts, selectedVehicleId, connectionStatus],
  );

  return <FleetContext.Provider value={value}>{children}</FleetContext.Provider>;
}

export function useFleet() {
  const context = useContext(FleetContext);
  if (!context) {
    throw new Error('useFleet must be used within FleetProvider');
  }
  return context;
}
