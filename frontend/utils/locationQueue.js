// Per-job queue of GPS points waiting to reach the server. Survives app restarts; the server
// ignores points it already has, so a point is only removed after the server acknowledged it.
import AsyncStorage from '@react-native-async-storage/async-storage';
import { driverApi } from './api';
import { appendPending, removeSent } from './jobs';

const key = (jobId) => `omniops:gps:${jobId}`;
const BATCH = 500;
let flushing = Promise.resolve();

export async function pendingPoints(jobId) {
  const raw = await AsyncStorage.getItem(key(jobId));
  return raw ? JSON.parse(raw) : [];
}

export async function enqueuePoints(jobId, points) {
  const next = appendPending(await pendingPoints(jobId), points);
  await AsyncStorage.setItem(key(jobId), JSON.stringify(next));
  return next.length;
}

/** Sends queued points in batches; returns how many are still waiting. Calls are serialised. */
export function flushPoints(jobId) {
  flushing = flushing.catch(() => {}).then(async () => {
    let queue = await pendingPoints(jobId);
    while (queue.length > 0) {
      const batch = queue.slice(0, BATCH);
      await driverApi.sendLocations(jobId, batch);
      queue = removeSent(await pendingPoints(jobId), batch.map((p) => p.id));
      await AsyncStorage.setItem(key(jobId), JSON.stringify(queue));
    }
    return 0;
  });
  return flushing;
}
