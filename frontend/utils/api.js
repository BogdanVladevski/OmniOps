import Constants from 'expo-constants';
import { Platform } from 'react-native';
import { fromResponse, getSession, setSession } from './session';

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
  const session = getSession();
  // The signed-in session decides who we are; the env token is only for local tooling without sign-in.
  const apiToken = session?.accessToken ?? process.env.EXPO_PUBLIC_API_TOKEN ?? '';
  const defaultFleetId = session?.account?.defaultFleetId ?? DEFAULT_FLEET_ID;
  return { apiBaseUrl, apiToken, defaultFleetId };
}

let refreshing = null;

/** Renews the session once (single flight). Signs out if the session can no longer be renewed. */
export function refreshSession() {
  const session = getSession();
  if (!session?.refreshToken) return Promise.resolve(false);
  refreshing ??= (async () => {
    try {
      const { apiBaseUrl } = getApiConfig();
      const response = await fetch(`${apiBaseUrl}/api/v1/account/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: session.refreshToken }),
      });
      if (response.status === 401) {
        await setSession(null);
        return false;
      }
      if (!response.ok) return false; // offline or server trouble: keep the session, try later
      await setSession(fromResponse(await response.json()));
      return true;
    } catch {
      return false;
    } finally {
      refreshing = null;
    }
  })();
  return refreshing;
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

export async function apiFetch(path, options = {}, retried = false) {
  const { apiBaseUrl, apiToken } = getApiConfig();
  if (!apiBaseUrl) throw new Error('Missing EXPO_PUBLIC_API_URL');

  const headers = {
    'Content-Type': 'application/json',
    ...(apiToken ? { Authorization: `Bearer ${apiToken}` } : {}),
    ...(options.headers ?? {}),
  };

  const response = await fetch(`${apiBaseUrl}${path}`, { ...options, headers });
  // Access tokens are short-lived: renew once and retry.
  if (response.status === 401 && !retried && getSession()?.refreshToken && (await refreshSession())) {
    return apiFetch(path, options, true);
  }
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
  createVehicle: (body) => apiFetch('/api/vehicles', { method: 'POST', body: JSON.stringify(body) }),
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

/** Absolute URL for an API path, e.g. a job's public tracking page. */
export function apiUrl(path) {
  return `${getApiConfig().apiBaseUrl}${path}`;
}

/** Headers for requests the app makes outside apiFetch (images, uploads). */
export function authHeaders() {
  const { apiToken } = getApiConfig();
  return apiToken ? { Authorization: `Bearer ${apiToken}` } : {};
}

export const jobsApi = {
  list: (scope = 'active') => apiFetch(`/api/v1/jobs?scope=${scope}`),
  get: (jobId) => apiFetch(`/api/v1/jobs/${jobId}`),
  assignable: () => apiFetch('/api/v1/jobs/assignable'),
  create: (body) => apiFetch('/api/v1/jobs', { method: 'POST', body: JSON.stringify(body) }),
  documentUrl: (jobId, documentId) => apiUrl(`/api/v1/jobs/${jobId}/documents/${documentId}`),
};

export const driverApi = {
  jobs: (driverId) => apiFetch(`/api/v1/driver/jobs?driverId=${driverId}`),
  start: (jobId) => apiFetch(`/api/v1/driver/jobs/${jobId}/start`, { method: 'POST' }),
  sendLocations: (jobId, points) =>
    apiFetch(`/api/v1/driver/jobs/${jobId}/locations`, { method: 'POST', body: JSON.stringify({ points }) }),
  deliver: (jobId, body) =>
    apiFetch(`/api/v1/driver/jobs/${jobId}/deliver`, { method: 'POST', body: JSON.stringify(body) }),

  /**
   * Uploads one photo for a job. `photo` is an image-picker asset ({ uri, mimeType, fileName }).
   * kind: 'DeliveryPhoto' | 'PickupDocument' | 'TemperaturePhoto' (needs temperatureCelsius).
   */
  async uploadDocument(jobId, kind, photo, coords, temperatureCelsius) {
    const form = new FormData();
    const type = photo.mimeType ?? 'image/jpeg';
    const name = photo.fileName ?? `${kind}-${Date.now()}.jpg`;
    if (Platform.OS === 'web') {
      const blob = await (await fetch(photo.uri)).blob();
      form.append('file', blob, name);
    } else {
      form.append('file', { uri: photo.uri, name, type });
    }
    const params = new URLSearchParams({ kind });
    if (coords) { params.set('latitude', coords.latitude); params.set('longitude', coords.longitude); }
    if (temperatureCelsius != null) params.set('temperatureCelsius', temperatureCelsius);
    const response = await fetch(apiUrl(`/api/v1/driver/jobs/${jobId}/documents?${params}`), {
      method: 'POST',
      headers: authHeaders(),
      body: form,
    });
    if (!response.ok) throw new Error(parseApiError(response.status, await response.text()));
    return response.json();
  },
};

/**
 * Looks up coordinates for an address with OpenStreetMap's Nominatim (free, 1 request/second,
 * fine for a dispatcher creating jobs; a paid geocoder is needed at scale). Returns null if not found.
 */
export async function geocode(address) {
  const q = encodeURIComponent(address.trim());
  if (!q) return null;
  const response = await fetch(`https://nominatim.openstreetmap.org/search?format=json&limit=1&q=${q}`, {
    headers: { Accept: 'application/json' },
  });
  if (!response.ok) return null;
  const [hit] = await response.json();
  return hit ? { latitude: Number(hit.lat), longitude: Number(hit.lon), label: hit.display_name } : null;
}

export const billingApi = {
  profile: () => apiFetch('/api/v1/billing/profile'),
  saveProfile: (body) => apiFetch('/api/v1/billing/profile', { method: 'PUT', body: JSON.stringify(body) }),
  overview: () => apiFetch('/api/v1/invoices'),
  get: (invoiceId) => apiFetch(`/api/v1/invoices/${invoiceId}`),
  /** send: 'email' | 'manual' | undefined (issue only). */
  create: (body) => apiFetch('/api/v1/invoices', { method: 'POST', body: JSON.stringify(body) }),
  send: (invoiceId, email) =>
    apiFetch(`/api/v1/invoices/${invoiceId}/send`, { method: 'POST', body: JSON.stringify({ email }) }),
  markPaid: (invoiceId, paidAt) =>
    apiFetch(`/api/v1/invoices/${invoiceId}/paid`, { method: 'POST', body: JSON.stringify({ paidAt: paidAt ?? null }) }),
};

export const accountApi = {
  options: () => apiFetch('/api/v1/account/options'),
  signIn: (email, password) => apiFetch('/api/v1/account/sign-in', { method: 'POST', body: JSON.stringify({ email, password }) }),
  signUp: (body) => apiFetch('/api/v1/account/sign-up', { method: 'POST', body: JSON.stringify(body) }),
  pair: (code) => apiFetch('/api/v1/account/pair', { method: 'POST', body: JSON.stringify({ code }) }),
  demo: () => apiFetch('/api/v1/account/demo', { method: 'POST' }),
  driverCode: (driverId) => apiFetch('/api/v1/account/driver-codes', { method: 'POST', body: JSON.stringify({ driverId }) }),

  // Team (owners)
  team: () => apiFetch('/api/v1/account/team'),
  invite: (email, role) => apiFetch('/api/v1/account/team/invitations', { method: 'POST', body: JSON.stringify({ email, role }) }),
  revokeInvitation: (id) => apiFetch(`/api/v1/account/team/invitations/${id}`, { method: 'DELETE' }),
  setRole: (userId, role) => apiFetch(`/api/v1/account/team/${userId}/role`, { method: 'PUT', body: JSON.stringify({ role }) }),
  removeTeammate: (userId) => apiFetch(`/api/v1/account/team/${userId}`, { method: 'DELETE' }),
  resetLink: (userId) => apiFetch(`/api/v1/account/team/${userId}/reset-link`, { method: 'POST' }),
  updateCompany: (name) => apiFetch('/api/v1/account/company', { method: 'PUT', body: JSON.stringify({ name }) }),

  // Links opened from an email or message
  invitation: (token) => apiFetch(`/api/v1/account/invitations/${encodeURIComponent(token)}`),
  accept: (token, name, password) =>
    apiFetch(`/api/v1/account/invitations/${encodeURIComponent(token)}/accept`, { method: 'POST', body: JSON.stringify({ name, password }) }),
  forgotPassword: (email) => apiFetch('/api/v1/account/forgot-password', { method: 'POST', body: JSON.stringify({ email }) }),
  resetPassword: (token, password) => apiFetch('/api/v1/account/reset-password', { method: 'POST', body: JSON.stringify({ token, password }) }),

  // Signed in
  changePassword: (currentPassword, newPassword) =>
    apiFetch('/api/v1/account/password', { method: 'POST', body: JSON.stringify({ currentPassword, newPassword }) }),
  updateProfile: (displayName) => apiFetch('/api/v1/account/me', { method: 'PUT', body: JSON.stringify({ displayName }) }),
  async signOutEverywhere() {
    try {
      await apiFetch('/api/v1/account/sign-out-everywhere', { method: 'POST' });
    } finally {
      await setSession(null);
    }
  },
  async signOut() {
    const refreshToken = getSession()?.refreshToken;
    await setSession(null);
    if (refreshToken) {
      // Best effort: the session is already gone on this device either way.
      apiFetch('/api/v1/account/sign-out', { method: 'POST', body: JSON.stringify({ refreshToken }) }).catch(() => {});
    }
  },
};
