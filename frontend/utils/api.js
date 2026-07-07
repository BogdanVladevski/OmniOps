import Constants from 'expo-constants';
import { Platform } from 'react-native';

const DEFAULT_FLEET_ID = 'f1000000-0000-0000-0000-000000000001';

/**
 * Physical devices and emulators cannot reach the dev machine via localhost.
 * Rewrite to Metro's LAN host (Expo Go) or the Android emulator bridge IP.
 */
export function resolveApiBaseUrl(configuredUrl) {
  let url = configuredUrl?.replace(/\/+$/, '') ?? '';
  if (!url || Platform.OS === 'web') return url;

  const isLocalhost = /localhost|127\.0\.0\.1/i.test(url);
  if (!isLocalhost) return url;

  if (Platform.OS === 'android' && !Constants.isDevice) {
    return url.replace(/localhost|127\.0\.0\.1/gi, '10.0.2.2');
  }

  const debuggerHost =
    Constants.expoGoConfig?.debuggerHost ??
    Constants.expoConfig?.hostUri?.replace(/^[^:]+:\/\//, '');

  if (debuggerHost) {
    const lanHost = debuggerHost.split(':')[0];
    return url.replace(/localhost|127\.0\.0\.1/gi, lanHost);
  }

  return url;
}

export function getApiConfig() {
  const configured = process.env.EXPO_PUBLIC_API_URL ?? '';
  const apiBaseUrl = resolveApiBaseUrl(configured);
  const apiToken = process.env.EXPO_PUBLIC_API_TOKEN ?? '';
  return { apiBaseUrl, apiToken, defaultFleetId: DEFAULT_FLEET_ID };
}

export function parseApiError(status, text) {
  if (!text) return `Request failed (${status})`;
  if (text.includes('<!DOCTYPE') || text.includes('System.InvalidOperationException')) {
    const match = text.match(/System\.\w+Exception:\s*([^\r\n<]+)/);
    return match?.[1]?.trim() ?? 'Server error — check that the API is running and configured correctly.';
  }
  try {
    const json = JSON.parse(text);
    if (json.title || json.detail) return json.detail ?? json.title;
    if (json.message) return json.message;
  } catch {
    // not JSON
  }
  return text.length > 200 ? `${text.slice(0, 200)}…` : text;
}

export async function apiFetch(path, options = {}) {
  const { apiBaseUrl, apiToken } = getApiConfig();
  if (!apiBaseUrl) throw new Error('Missing EXPO_PUBLIC_API_URL');

  const headers = {
    'Content-Type': 'application/json',
    ...(apiToken ? { Authorization: `Bearer ${apiToken}` } : {}),
    ...(options.headers ?? {}),
  };

  const response = await fetch(`${apiBaseUrl}${path}`, { ...options, headers });
  if (!response.ok) {
    const text = await response.text();
    throw new Error(parseApiError(response.status, text));
  }
  if (response.status === 204) return null;
  return response.json();
}

export const fleetApi = {
  list: () => apiFetch('/api/fleets'),
  statistics: (fleetId) => apiFetch(`/api/fleets/${fleetId}/statistics`),
  vehicles: (fleetId) => apiFetch(`/api/fleets/${fleetId}/vehicles`),
  clusters: (fleetId, radiusMeters = 500) =>
    apiFetch(`/api/fleets/${fleetId}/clusters?radiusMeters=${radiusMeters}`),
  heatmap: (fleetId, fromUtc, toUtc, gridSize = 0.01) =>
    apiFetch(
      `/api/fleets/${fleetId}/heatmap?fromUtc=${encodeURIComponent(fromUtc)}&toUtc=${encodeURIComponent(toUtc)}&gridSize=${gridSize}`,
    ),
  createDriver: (body) => apiFetch('/api/drivers', { method: 'POST', body: JSON.stringify(body) }),
  assignDriver: (vehicleId, driverId) =>
    apiFetch(`/api/vehicles/${vehicleId}/assign-driver`, {
      method: 'POST',
      body: JSON.stringify({ driverId }),
    }),
};

export const geofenceApi = {
  list: (fleetId) => apiFetch(`/api/geofences?fleetId=${fleetId}`),
};

export const telemetryApi = {
  aggregations: (vehicleId, fromUtc, toUtc, bucketMinutes = 5) =>
    apiFetch(
      `/api/telemetry/${vehicleId}/aggregations?fromUtc=${encodeURIComponent(fromUtc)}&toUtc=${encodeURIComponent(toUtc)}&bucketMinutes=${bucketMinutes}`,
    ),
};

export const incidentApi = {
  list: (fleetId, status) => {
    const params = new URLSearchParams({ fleetId });
    if (status) params.set('status', status);
    return apiFetch(`/api/incidents?${params}`);
  },
  resolve: (incidentId, notes) =>
    apiFetch(`/api/incidents/${incidentId}/resolve`, {
      method: 'POST',
      body: JSON.stringify({ notes }),
    }),
  addNote: (incidentId, text) =>
    apiFetch(`/api/incidents/${incidentId}/notes`, {
      method: 'POST',
      body: JSON.stringify({ text }),
    }),
};

export const analyticsApi = {
  fleet: (fleetId, fromUtc, toUtc) =>
    apiFetch(
      `/api/analytics/fleet/${fleetId}?fromUtc=${encodeURIComponent(fromUtc)}&toUtc=${encodeURIComponent(toUtc)}`,
    ),
  drivers: (fleetId, fromUtc, toUtc) =>
    apiFetch(
      `/api/analytics/drivers/${fleetId}?fromUtc=${encodeURIComponent(fromUtc)}&toUtc=${encodeURIComponent(toUtc)}`,
    ),
  operational: (fleetId, fromUtc, toUtc) =>
    apiFetch(
      `/api/analytics/operational/${fleetId}?fromUtc=${encodeURIComponent(fromUtc)}&toUtc=${encodeURIComponent(toUtc)}`,
    ),
};

export const predictionApi = {
  vehicleHealth: (vehicleId) => apiFetch(`/api/predictions/vehicles/${vehicleId}/health`),
  maintenance: (vehicleId) => apiFetch(`/api/predictions/vehicles/${vehicleId}/maintenance`),
  driverRisk: (driverId) => apiFetch(`/api/predictions/drivers/${driverId}/risk`),
};

export const copilotApi = {
  ask: (question, fleetId) =>
    apiFetch('/api/copilot/ask', {
      method: 'POST',
      body: JSON.stringify({ question, fleetId }),
    }),
};

export const tenantApi = {
  workspaces: (organizationId) => apiFetch(`/api/v1/tenant/organizations/${organizationId}/workspaces`),
  settings: (organizationId) => apiFetch(`/api/v1/tenant/organizations/${organizationId}/settings`),
};

export const notificationApi = {
  list: (limit = 50) => apiFetch(`/api/v1/notifications?limit=${limit}`),
  preferences: () => apiFetch('/api/v1/notifications/preferences'),
  updatePreferences: (body) =>
    apiFetch('/api/v1/notifications/preferences', { method: 'PUT', body: JSON.stringify(body) }),
};

export const adminApi = {
  auditLogs: (params = {}) => {
    const q = new URLSearchParams();
    if (params.fromUtc) q.set('fromUtc', params.fromUtc);
    if (params.toUtc) q.set('toUtc', params.toUtc);
    if (params.entityType) q.set('entityType', params.entityType);
    return apiFetch(`/api/v1/admin/audit-logs?${q}`);
  },
  apiKeys: () => apiFetch('/api/v1/admin/api-keys'),
  createApiKey: (body) =>
    apiFetch('/api/v1/admin/api-keys', { method: 'POST', body: JSON.stringify(body) }),
};

export const mobileApi = {
  registerPushToken: (pushToken, platform) =>
    apiFetch('/api/v1/mobile/push-token', {
      method: 'POST',
      body: JSON.stringify({ pushToken, platform }),
    }),
  sync: (sinceUtc) => {
    const q = sinceUtc ? `?sinceUtc=${encodeURIComponent(sinceUtc)}` : '';
    return apiFetch(`/api/v1/mobile/sync${q}`);
  },
};

export const demoApi = {
  status: () => apiFetch('/api/v1/demo/status'),
  bootstrap: (packetsPerVehicle = 8) =>
    apiFetch('/api/v1/demo/bootstrap', {
      method: 'POST',
      body: JSON.stringify({ packetsPerVehicle }),
    }),
};
