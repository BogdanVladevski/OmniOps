const DEFAULT_REPLAY_WINDOW = {
  beforeMinutes: 5,
  afterMinutes: 2,
};

export function buildReplayWindow(anchorIso, { beforeMinutes, afterMinutes } = DEFAULT_REPLAY_WINDOW) {
  const anchor = new Date(anchorIso);
  const fromUtc = new Date(anchor.getTime() - beforeMinutes * 60_000).toISOString();
  const toUtc = new Date(anchor.getTime() + afterMinutes * 60_000).toISOString();
  return { fromUtc, toUtc };
}

export async function fetchShipmentReplay({ apiBaseUrl, apiToken, shipmentId, fromUtc, toUtc, anchorUtc }) {
  const base = apiBaseUrl.replace(/\/+$/, '');
  const params = new URLSearchParams();

  if (fromUtc && toUtc) {
    params.set('fromUtc', fromUtc);
    params.set('toUtc', toUtc);
  } else if (anchorUtc) {
    params.set('anchorUtc', anchorUtc);
  }

  const headers = apiToken ? { Authorization: `Bearer ${apiToken}` } : {};
  const response = await fetch(`${base}/api/shipments/${shipmentId}/replay?${params}`, { headers });

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || `Replay request failed (${response.status})`);
  }

  return response.json();
}
