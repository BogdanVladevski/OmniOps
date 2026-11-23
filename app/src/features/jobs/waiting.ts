// Waiting time (detention) at pickup and delivery: shared wording.
import type { Wait } from '@/api/types';

/** 135 → "2 h 15 min"; 45 → "45 min"; 120 → "2 h". Units can be passed for other languages. */
export function formatDuration(minutes: number | null | undefined, units = { h: 'h', min: 'min' }): string {
  const m = Math.max(0, Math.floor(minutes ?? 0));
  if (m < 60) return `${m} ${units.min}`;
  const h = Math.floor(m / 60);
  const rest = m % 60;
  return rest ? `${h} ${units.h} ${String(rest).padStart(2, '0')} ${units.min}` : `${h} ${units.h}`;
}

/** Minutes since an ISO time (for a wait still running on screen). */
export const minutesSince = (iso: string | null | undefined, now = Date.now()) =>
  iso ? Math.max(0, Math.floor((now - new Date(iso).getTime()) / 60_000)) : 0;

export const stopName = (stop: string) => (stop === 'pickup' ? 'Loading' : 'Unloading');

/** How the arrival was recorded: the evidence a customer will ask about. */
export function evidence(wait: Pick<Wait, 'source' | 'distanceFromStopMeters' | 'verified'>): string {
  if (wait.source === 'Gps') return 'Arrival recorded by the phone’s GPS at the address';
  if (wait.distanceFromStopMeters == null) return 'Driver tapped “I’ve arrived” (no address location to compare)';
  const d = wait.distanceFromStopMeters;
  const distance = d >= 1000 ? `${(d / 1000).toFixed(1)} km` : `${Math.round(d)} m`;
  return wait.verified
    ? `Driver tapped “I’ve arrived” ${distance} from the address`
    : `Driver tapped “I’ve arrived” ${distance} from the address, too far to count as proof`;
}
