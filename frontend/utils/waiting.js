// Waiting time (detention) at pickup and delivery: shared wording for the office screens.

/** 135 → "2 h 15 min"; 45 → "45 min"; 120 → "2 h". Units can be passed for other languages. */
export function formatDuration(minutes, units = { h: 'h', min: 'min' }) {
  const m = Math.max(0, Math.floor(minutes ?? 0));
  if (m < 60) return `${m} ${units.min}`;
  const h = Math.floor(m / 60);
  const rest = m % 60;
  return rest ? `${h} ${units.h} ${String(rest).padStart(2, '0')} ${units.min}` : `${h} ${units.h}`;
}

/** Minutes since an ISO time (for a wait that is still running on screen). */
export function minutesSince(iso, now = Date.now()) {
  return iso ? Math.max(0, Math.floor((now - new Date(iso).getTime()) / 60000)) : 0;
}

export const stopName = (stop) => (stop === 'pickup' ? 'Loading' : 'Unloading');

/** One line explaining how the arrival was recorded — the evidence the customer will ask about. */
export function evidence(wait) {
  if (wait.source === 'Gps') return 'Arrival recorded by the phone’s GPS at the address';
  if (wait.distanceFromStopMeters == null) return 'Driver tapped “I’ve arrived” (no address location to compare)';
  const km = wait.distanceFromStopMeters >= 1000 ? `${(wait.distanceFromStopMeters / 1000).toFixed(1)} km` : `${Math.round(wait.distanceFromStopMeters)} m`;
  return wait.verified
    ? `Driver tapped “I’ve arrived” ${km} from the address`
    : `Driver tapped “I’ve arrived” ${km} from the address — too far to count as proof`;
}
