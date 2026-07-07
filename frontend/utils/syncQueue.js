import { apiFetch } from './api';
import { clearSyncQueue, loadSyncQueue, saveSyncQueue } from './offlineStorage';

export async function flushSyncQueue() {
  const queue = await loadSyncQueue();
  if (queue.length === 0) return { flushed: 0, failed: 0 };

  const remaining = [];
  let flushed = 0;

  for (const item of queue) {
    try {
      await apiFetch(item.path, {
        method: item.method ?? 'POST',
        body: item.body ? JSON.stringify(item.body) : undefined,
      });
      flushed += 1;
    } catch {
      remaining.push(item);
    }
  }

  await saveSyncQueue(remaining);
  return { flushed, failed: remaining.length };
}

export async function fetchMobileSync(sinceUtc) {
  const query = sinceUtc ? `?sinceUtc=${encodeURIComponent(sinceUtc)}` : '';
  return apiFetch(`/api/v1/mobile/sync${query}`);
}
