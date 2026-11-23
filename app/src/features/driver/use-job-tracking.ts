// Shares the phone's position while a job is under way (foreground; background tracking comes with the native build).
// Points are queued on the phone and only reported as sent once the server has them.
import * as Location from 'expo-location';
import { useEffect, useState } from 'react';
import { toLocationPoint } from '@/features/jobs/job-format';
import type { Coords } from './driver-api';
import { enqueuePoints, flushPoints, pendingPoints } from './offline/location-queue';
import { outbox } from './offline/outbox';

export interface TrackingState {
  status: 'off' | 'starting' | 'sharing' | 'denied' | 'error';
  lastSentAt: string | null;
  pending: number;
  error: string | null;
}

const FLUSH_EVERY_MS = 20_000;
const SEND_AT_POINTS = 5;

export function useJobTracking(jobId: string, active: boolean): TrackingState {
  const [state, setState] = useState<TrackingState>({ status: 'off', lastSentAt: null, pending: 0, error: null });

  useEffect(() => {
    if (!active) {
      setState((s) => ({ ...s, status: 'off' }));
      return;
    }
    let subscription: Location.LocationSubscription | null = null;
    let cancelled = false;

    const flush = async () => {
      try {
        // A "Start trip" made offline must reach the server before the route does.
        if ((await outbox.flush()).offline) throw new Error('offline');
        const waiting = (await pendingPoints(jobId)).length;
        await flushPoints(jobId);
        if (!cancelled)
          setState((s) => ({
            ...s,
            lastSentAt: waiting ? new Date().toISOString() : s.lastSentAt,
            pending: 0,
            error: null,
          }));
      } catch (e) {
        const pending = (await pendingPoints(jobId)).length;
        if (!cancelled) setState((s) => ({ ...s, pending, error: e instanceof Error ? e.message : String(e) }));
      }
    };

    (async () => {
      setState((s) => ({ ...s, status: 'starting' }));
      const { status } = await Location.requestForegroundPermissionsAsync();
      if (cancelled) return;
      if (status !== Location.PermissionStatus.GRANTED) {
        setState((s) => ({ ...s, status: 'denied' }));
        return;
      }
      setState((s) => ({ ...s, status: 'sharing' }));
      subscription = await Location.watchPositionAsync(
        { accuracy: Location.Accuracy.Balanced, timeInterval: 15_000, distanceInterval: 50 },
        async (location) => {
          const pending = await enqueuePoints(jobId, [toLocationPoint(location)]);
          if (!cancelled) setState((s) => ({ ...s, pending }));
          if (pending >= SEND_AT_POINTS) void flush();
        },
      );
      void flush();
    })().catch(
      (e: unknown) =>
        !cancelled && setState((s) => ({ ...s, status: 'error', error: e instanceof Error ? e.message : String(e) })),
    );

    const timer = setInterval(() => void flush(), FLUSH_EVERY_MS);
    return () => {
      cancelled = true;
      clearInterval(timer);
      try {
        subscription?.remove();
      } catch {
        // expo-location on the web can throw here although the watch is gone; stopping must never break the screen.
      }
      void flush();
    };
  }, [jobId, active]);

  return state;
}

/** Where the phone is now, if it knows; never blocks an action for long. */
export async function currentCoords(): Promise<Coords | null> {
  try {
    const fix =
      (await Location.getLastKnownPositionAsync()) ??
      (await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced }));
    return fix ? { latitude: fix.coords.latitude, longitude: fix.coords.longitude } : null;
  } catch {
    return null;
  }
}
