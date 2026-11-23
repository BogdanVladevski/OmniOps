// Presentation and rules for delivery jobs, shared by the office and the driver app.
import type { Job, JobStatus, LocationPoint } from '@/api/types';
import { clock } from '@/lib/time';
import type { Tone } from '@/theme/tokens';

type JobLike = Pick<Job, 'status'> & Partial<Job>;

const STATUS: Record<JobStatus, { label: string; tone: Tone }> = {
  Planned: { label: 'Planned', tone: 'neutral' },
  InProgress: { label: 'On the way', tone: 'accent' },
  Delivered: { label: 'Delivered', tone: 'ok' },
  Cancelled: { label: 'Cancelled', tone: 'neutral' },
};

export const jobStatusMeta = (status: string) =>
  STATUS[status as JobStatus] ?? { label: status || 'Unknown', tone: 'neutral' as Tone };

/** The customer, or where it goes. */
export const jobTitle = (job: Partial<Job> | null | undefined) => job?.customerName || job?.destination || 'Delivery';

/** If the last GPS report is older than this, the office sees the truck as "not reporting". */
export const STALE_POSITION_MINUTES = 10;

export function isPositionStale(job: JobLike, now = Date.now()) {
  if (job.status !== 'InProgress' || !job.lastPositionAtUtc) return false;
  return now - new Date(job.lastPositionAtUtc).getTime() > STALE_POSITION_MINUTES * 60_000;
}

/** Random RFC 4122 v4 id (the server uses it to recognise a resend). */
export function newId(): string {
  if (typeof globalThis.crypto?.randomUUID === 'function') return globalThis.crypto.randomUUID();
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, (c) => {
    const r = (Math.random() * 16) | 0;
    return (c === 'x' ? r : (r & 0x3) | 0x8).toString(16);
  });
}

interface PhoneLocation {
  coords?: {
    latitude: number;
    longitude: number;
    speed?: number | null;
    heading?: number | null;
    accuracy?: number | null;
  };
  timestamp?: number;
}

/** An expo-location reading as an API point. Unknown speed or heading (-1 or null) stays null, never 0. */
export function toLocationPoint(location: PhoneLocation, id = newId()): LocationPoint {
  const c = location.coords ?? { latitude: 0, longitude: 0 };
  const known = (v: number | null | undefined) => (typeof v === 'number' && Number.isFinite(v) && v >= 0 ? v : null);
  const speedMs = known(c.speed);
  const heading = known(c.heading);
  return {
    id,
    latitude: c.latitude,
    longitude: c.longitude,
    speedKmh: speedMs == null ? null : Math.min(300, Math.round(speedMs * 3.6 * 10) / 10),
    heading: heading == null ? null : Math.min(360, heading),
    accuracyMeters: known(c.accuracy),
    recordedAtUtc: new Date(location.timestamp ?? Date.now()).toISOString(),
  };
}

/** Adds points to a pending queue, newest last, capped so a phone offline for days cannot fill its storage. */
export function appendPending<T>(queue: T[] | null | undefined, points: T[], max = 5000): T[] {
  const merged = [...(queue ?? []), ...points];
  return merged.length > max ? merged.slice(merged.length - max) : merged;
}

export function removeSent<T extends { id: string }>(queue: T[] | null | undefined, sentIds: string[]): T[] {
  const sent = new Set(sentIds);
  return (queue ?? []).filter((p) => !sent.has(p.id));
}

/** Today's jobs: on the way (silent first, then soonest arrival), not started, delivered (newest first). */
export function groupJobsForToday(jobs: Job[], now = Date.now()) {
  const eta = (j: Job) => (j.etaUtc ? new Date(j.etaUtc).getTime() : Number.MAX_SAFE_INTEGER);
  return {
    onTheWay: jobs
      .filter((j) => j.status === 'InProgress')
      .sort((a, b) => Number(isPositionStale(b, now)) - Number(isPositionStale(a, now)) || eta(a) - eta(b)),
    planned: jobs.filter((j) => j.status === 'Planned'),
    delivered: jobs
      .filter((j) => j.status === 'Delivered')
      .sort((a, b) => new Date(b.deliveredAtUtc ?? 0).getTime() - new Date(a.deliveredAtUtc ?? 0).getTime()),
  };
}

/** Short right-hand status for a job row. */
export function jobProgress(job: Job, now = Date.now()): { primary: string; secondary: string; tone: Tone } {
  switch (job.status) {
    case 'InProgress':
      if (!job.lastPositionAtUtc) return { primary: 'Started', secondary: 'Waiting for location', tone: 'neutral' };
      if (isPositionStale(job, now))
        return { primary: 'No signal', secondary: `Last seen ${clock(job.lastPositionAtUtc)}`, tone: 'warning' };
      if (job.remainingKm === 0) return { primary: 'Arriving', secondary: 'At the destination', tone: 'accent' };
      if (job.etaUtc)
        return {
          primary: clock(job.etaUtc),
          secondary: `about ${Math.round(job.remainingKm ?? 0)} km`,
          tone: 'accent',
        };
      return { primary: 'On the way', secondary: '', tone: 'accent' };
    case 'Planned':
      return {
        primary: 'Not started',
        secondary: job.plannedDeliveryUtc ? `Due ${clock(job.plannedDeliveryUtc)}` : '',
        tone: 'neutral',
      };
    case 'Delivered':
      return { primary: 'Delivered', secondary: job.deliveredAtUtc ? clock(job.deliveredAtUtc) : '', tone: 'ok' };
    default:
      return { primary: jobStatusMeta(job.status).label, secondary: '', tone: 'neutral' };
  }
}

// ---- Temperature log (refrigerated loads) ----

export const TEMPERATURE_PRESETS = [
  { key: 'none', label: 'Not refrigerated', range: null },
  { key: 'chilled', label: 'Chilled 2–8 °C', range: [2, 8] },
  { key: 'frozen', label: 'Frozen −25 to −18 °C', range: [-25, -18] },
  { key: 'custom', label: 'Custom', range: null },
] as const;

/** A delivery reading must be at most this old (matches the server rule). */
export const DELIVERY_READING_MAX_MINUTES = 30;

export const isRefrigerated = (job: Partial<Job> | null | undefined) =>
  job?.minTempCelsius != null && job?.maxTempCelsius != null;

const fmt = (n: number) => n.toLocaleString(undefined, { maximumFractionDigits: 1 }).replace('-', '−');

export const formatRange = (job: Partial<Job>) =>
  isRefrigerated(job) ? `${fmt(job.minTempCelsius!)} to ${fmt(job.maxTempCelsius!)} °C` : null;

export const formatCelsius = (value: number | null | undefined) => (value == null ? '—' : `${fmt(value)} °C`);

export const isOutOfRange = (job: Partial<Job>, celsius: number | null | undefined) =>
  isRefrigerated(job) && celsius != null && (celsius < job.minTempCelsius! || celsius > job.maxTempCelsius!);

/** Reads what a driver typed from the fridge display: "4,5", "+4", "−18". Null when it isn't a usable number. */
export function parseTemperatureInput(text: string | null | undefined): number | null {
  const cleaned = String(text ?? '')
    .trim()
    .replace(',', '.')
    .replace('−', '-')
    .replace(/\s*°?\s*c?$/i, '');
  if (!/^[+-]?\d{1,2}(\.\d{1,2})?$/.test(cleaned)) return null;
  const value = Number(cleaned);
  return value >= -40 && value <= 40 ? value : null;
}

/** Where the temperature log stands, or null if the load is not refrigerated. */
export function temperatureStatus(job: Job, now = Date.now()): { label: string; tone: Tone } | null {
  if (!isRefrigerated(job)) return null;
  if (job.temperatureOutOfRangeCount > 0) {
    const n = job.temperatureOutOfRangeCount;
    return { label: `${n} reading${n === 1 ? '' : 's'} outside ${formatRange(job)}`, tone: 'critical' };
  }
  if (
    job.status === 'InProgress' &&
    job.temperatureCheckDueUtc &&
    new Date(job.temperatureCheckDueUtc).getTime() <= now
  ) {
    return {
      label: job.temperatureReadingCount ? 'Temperature check overdue' : 'No temperature logged yet',
      tone: 'warning',
    };
  }
  if (job.lastTemperatureCelsius != null)
    return { label: `${formatCelsius(job.lastTemperatureCelsius)} · within range`, tone: 'ok' };
  return { label: `Keep at ${formatRange(job)}`, tone: 'neutral' };
}

/** True if a reading exists recent enough to confirm delivery. */
export const hasRecentReading = (lastReadingIso: string | null | undefined, now = Date.now()) =>
  !!lastReadingIso && now - new Date(lastReadingIso).getTime() <= DELIVERY_READING_MAX_MINUTES * 60_000;
