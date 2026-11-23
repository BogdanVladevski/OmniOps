// Per-job queue of GPS points waiting to reach the server. Survives restarts; the server skips points it already has,
// so a point is only removed after the server acknowledged it.
import AsyncStorage from '@react-native-async-storage/async-storage';
import type { LocationPoint } from '@/api/types';
import { appendPending, removeSent } from '@/features/jobs/job-format';
import { driverApi } from '../driver-api';

const key = (jobId: string) => `omniops:gps:${jobId}`;
const BATCH = 500;
let flushing: Promise<unknown> = Promise.resolve();

export async function pendingPoints(jobId: string): Promise<LocationPoint[]> {
  const raw = await AsyncStorage.getItem(key(jobId));
  return raw ? (JSON.parse(raw) as LocationPoint[]) : [];
}

export async function enqueuePoints(jobId: string, points: LocationPoint[]): Promise<number> {
  const next = appendPending(await pendingPoints(jobId), points);
  await AsyncStorage.setItem(key(jobId), JSON.stringify(next));
  return next.length;
}

/** Sends queued points in batches; calls are serialised. Rejects (keeping the rest) when offline. */
export function flushPoints(jobId: string): Promise<void> {
  const run = flushing
    .catch(() => {})
    .then(async () => {
      let queue = await pendingPoints(jobId);
      while (queue.length > 0) {
        const batch = queue.slice(0, BATCH);
        await driverApi.sendLocations(jobId, batch);
        queue = removeSent(
          await pendingPoints(jobId),
          batch.map((p) => p.id),
        );
        await AsyncStorage.setItem(key(jobId), JSON.stringify(queue));
      }
    });
  flushing = run;
  return run;
}
