// Presentation and data rules for delivery jobs, shared by the office board and the driver app.

const STATUS = {
  Planned: { label: 'Planned', tone: 'neutral' },
  InProgress: { label: 'On the way', tone: 'accent' },
  Completed: { label: 'Delivered', tone: 'ok' },
  Cancelled: { label: 'Cancelled', tone: 'neutral' },
};

export function jobStatusMeta(status) {
  return STATUS[status] ?? { label: status ?? 'Unknown', tone: 'neutral' };
}

/** "Apteka Zegin · Bitola" style title, falling back gracefully. */
export function jobTitle(job) {
  return job?.customerName || job?.destination || 'Delivery';
}

/** If the last GPS report is older than this, the office sees the truck as "not reporting". */
export const STALE_POSITION_MINUTES = 10;

export function isPositionStale(job, now = Date.now()) {
  if (job?.status !== 'InProgress' || !job.lastPositionAtUtc) return false;
  return now - new Date(job.lastPositionAtUtc).getTime() > STALE_POSITION_MINUTES * 60 * 1000;
}

/** Random RFC 4122 v4 id (the server uses it to drop resent points). */
export function newId() {
  if (typeof globalThis.crypto?.randomUUID === 'function') return globalThis.crypto.randomUUID();
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}

/**
 * Converts an expo-location reading into an API point. Platforms report unknown speed/heading
 * as -1 or null; those become null rather than a fake 0.
 */
export function toLocationPoint(location, id = newId()) {
  const c = location?.coords ?? {};
  const known = (v) => (typeof v === 'number' && Number.isFinite(v) && v >= 0 ? v : null);
  const speedMs = known(c.speed);
  return {
    id,
    latitude: c.latitude,
    longitude: c.longitude,
    speedKmh: speedMs == null ? null : Math.min(300, Math.round(speedMs * 3.6 * 10) / 10),
    heading: known(c.heading) == null ? null : Math.min(360, c.heading),
    accuracyMeters: known(c.accuracy),
    recordedAtUtc: new Date(location?.timestamp ?? Date.now()).toISOString(),
  };
}

/** Adds points to a pending queue, newest last, capped so a phone offline for days cannot fill storage. */
export function appendPending(queue, points, max = 5000) {
  const merged = [...(queue ?? []), ...points];
  return merged.length > max ? merged.slice(merged.length - max) : merged;
}

/** Removes acknowledged points (by id) from the queue. */
export function removeSent(queue, sentIds) {
  const sent = new Set(sentIds);
  return (queue ?? []).filter((p) => !sent.has(p.id));
}
