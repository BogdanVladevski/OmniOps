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

const clock = (iso) => new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

/**
 * Today's jobs for the live map: trucks on the way (silent ones first, then soonest arrival),
 * jobs not started yet, and deliveries already made.
 */
export function groupJobsForToday(jobs, now = Date.now()) {
  const onTheWay = (jobs ?? []).filter((j) => j.status === 'InProgress');
  const eta = (j) => (j.etaUtc ? new Date(j.etaUtc).getTime() : Number.MAX_SAFE_INTEGER);
  onTheWay.sort((a, b) => Number(isPositionStale(b, now)) - Number(isPositionStale(a, now)) || eta(a) - eta(b));
  return {
    onTheWay,
    planned: (jobs ?? []).filter((j) => j.status === 'Planned'),
    delivered: (jobs ?? [])
      .filter((j) => j.status === 'Completed')
      .sort((a, b) => new Date(b.deliveredAtUtc) - new Date(a.deliveredAtUtc)),
  };
}

/** Short right-hand status for a job row: { primary, secondary, tone }. */
export function jobProgress(job, now = Date.now()) {
  switch (job?.status) {
    case 'InProgress': {
      if (!job.lastPositionAtUtc) return { primary: 'Started', secondary: 'Waiting for location', tone: 'neutral' };
      if (isPositionStale(job, now)) return { primary: 'No signal', secondary: `Last seen ${clock(job.lastPositionAtUtc)}`, tone: 'warning' };
      if (job.remainingKm === 0) return { primary: 'Arriving', secondary: 'At the destination', tone: 'accent' };
      if (job.etaUtc) return { primary: clock(job.etaUtc), secondary: `about ${Math.round(job.remainingKm)} km`, tone: 'accent' };
      return { primary: 'On the way', secondary: '', tone: 'accent' };
    }
    case 'Planned':
      return { primary: 'Not started', secondary: job.plannedDeliveryUtc ? `Due ${clock(job.plannedDeliveryUtc)}` : '', tone: 'neutral' };
    case 'Completed':
      return { primary: 'Delivered', secondary: job.deliveredAtUtc ? clock(job.deliveredAtUtc) : '', tone: 'ok' };
    default:
      return { primary: jobStatusMeta(job?.status).label, secondary: '', tone: 'neutral' };
  }
}

// ---- Temperature log (refrigerated loads) -------------------------------------------------

export const TEMPERATURE_PRESETS = [
  { key: 'none', label: 'Not refrigerated', range: null },
  { key: 'chilled', label: 'Chilled 2–8 °C', range: [2, 8] },
  { key: 'frozen', label: 'Frozen −25 to −18 °C', range: [-25, -18] },
  { key: 'custom', label: 'Custom', range: null },
];

/** A delivery reading must be at most this old (matches the server rule). */
export const DELIVERY_READING_MAX_MINUTES = 30;

export function isRefrigerated(job) {
  return job?.minTempCelsius != null && job?.maxTempCelsius != null;
}

const fmt = (n) => `${Number(n).toLocaleString(undefined, { maximumFractionDigits: 1 })}`.replace('-', '−');

export function formatRange(job) {
  return isRefrigerated(job) ? `${fmt(job.minTempCelsius)} to ${fmt(job.maxTempCelsius)} °C` : null;
}

export function formatCelsius(value) {
  return value == null ? '—' : `${fmt(value)} °C`;
}

export function isOutOfRange(job, celsius) {
  return isRefrigerated(job) && celsius != null && (celsius < job.minTempCelsius || celsius > job.maxTempCelsius);
}

/**
 * Parses what a driver typed from the fridge display: accepts a decimal comma ("4,5"), a leading plus,
 * and the Unicode minus. Returns null when it is not a usable number.
 */
export function parseTemperatureInput(text) {
  const cleaned = String(text ?? '').trim().replace(',', '.').replace('−', '-').replace(/\s*°?\s*c?$/i, '');
  if (!/^[+-]?\d{1,2}(\.\d{1,2})?$/.test(cleaned)) return null;
  const value = Number(cleaned);
  return value >= -40 && value <= 40 ? value : null;
}

/** Where the temperature log stands: { label, tone } for office and driver screens, or null if not refrigerated. */
export function temperatureStatus(job, now = Date.now()) {
  if (!isRefrigerated(job)) return null;
  if (job.temperatureOutOfRangeCount > 0) {
    return { label: `${job.temperatureOutOfRangeCount} reading${job.temperatureOutOfRangeCount === 1 ? '' : 's'} outside ${formatRange(job)}`, tone: 'critical' };
  }
  if (job.status === 'InProgress' && job.temperatureCheckDueUtc && new Date(job.temperatureCheckDueUtc).getTime() <= now) {
    return { label: job.temperatureReadingCount ? 'Temperature check overdue' : 'No temperature logged yet', tone: 'warning' };
  }
  if (job.lastTemperatureCelsius != null) {
    return { label: `${formatCelsius(job.lastTemperatureCelsius)} · within range`, tone: 'ok' };
  }
  return { label: `Keep at ${formatRange(job)}`, tone: 'neutral' };
}

/** True if a reading exists recent enough to confirm delivery. */
export function hasRecentReading(lastReadingIso, now = Date.now()) {
  return !!lastReadingIso && now - new Date(lastReadingIso).getTime() <= DELIVERY_READING_MAX_MINUTES * 60 * 1000;
}
