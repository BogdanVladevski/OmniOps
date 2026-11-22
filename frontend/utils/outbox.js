// Everything a driver does on a job — start, photos, temperature readings, signature, problem reports,
// delivery — is saved on the phone first and sent in order when there is signal. Each item has its own id,
// which the server uses to ignore a resend, so retrying after a dropped connection is always safe.
import AsyncStorage from '@react-native-async-storage/async-storage';
import { driverApi } from './api';
import { newId } from './jobs';
import { loadPhoto, removePhoto, savePhoto } from './photoStore';

const KEY = 'omniops:outbox';

/** True for "the server said no" (keep the item, show why); false for no connection (just retry later). */
export const isRefusal = (error) => typeof error?.status === 'number' && error.status !== 401 && error.status < 500 && error.status !== 429;

/**
 * Applies what is still waiting to be sent to a job as the server returned it, so the screen shows what the
 * driver did even while offline. Waiting items are marked so the screen can say "not sent yet".
 */
export function applyPending(job, items) {
  const mine = items.filter((i) => i.jobId === job.id);
  if (!mine.length) return job;
  const next = { ...job, pendingCount: mine.length };
  for (const item of mine) {
    if (item.type === 'start' && next.status === 'Planned') {
      next.status = 'InProgress';
      next.startedAtUtc = item.at;
    } else if (item.type === 'deliver' && next.status === 'InProgress') {
      next.status = 'Completed';
      next.deliveredAtUtc = item.at;
      next.recipientName = item.payload.recipientName;
      next.deliveryPending = true;
    } else if (item.type === 'document') {
      const { kind, temperatureCelsius } = item.payload;
      const counter = {
        DeliveryPhoto: 'photoCount', PickupDocument: 'pickupDocumentCount', Signature: 'signatureCount', Exception: 'exceptionCount',
      }[kind];
      if (counter) next[counter] = (next[counter] ?? 0) + 1;
      if (kind === 'TemperaturePhoto') {
        next.lastTemperatureCelsius = temperatureCelsius;
        next.lastTemperatureAtUtc = item.at;
        next.temperatureReadingCount = (next.temperatureReadingCount ?? 0) + 1;
      }
    }
  }
  return next;
}

/** The outbox, with its storage and sender injectable for tests. */
export function createOutbox({ storage = AsyncStorage, api = driverApi, photos = { savePhoto, loadPhoto, removePhoto } } = {}) {
  const listeners = new Set();
  let items = null;
  let flushing = null;

  const load = async () => {
    if (items) return items;
    try {
      items = JSON.parse((await storage.getItem(KEY)) ?? '[]');
    } catch {
      items = [];
    }
    return items;
  };
  const save = async () => {
    await storage.setItem(KEY, JSON.stringify(items));
    listeners.forEach((fn) => fn([...items]));
  };

  async function send(item) {
    const at = item.at;
    switch (item.type) {
      case 'start':
        return api.start(item.jobId, at);
      case 'deliver':
        return api.deliver(item.jobId, { ...item.payload, deliveredAt: at });
      case 'document': {
        const { kind, coords, temperatureCelsius, note } = item.payload;
        const uri = await photos.loadPhoto(item.photoRef);
        if (!uri) {
          const gone = new Error('The photo is no longer on this phone. Take it again.');
          gone.status = 410;
          throw gone;
        }
        return api.uploadDocument(item.jobId, kind, { uri }, coords, temperatureCelsius, { clientId: item.id, capturedAt: at, note });
      }
      default:
        return null;
    }
  }

  return {
    subscribe(fn) {
      listeners.add(fn);
      load().then((list) => fn([...list]));
      return () => listeners.delete(fn);
    },

    async list() {
      return [...(await load())];
    },

    /** Adds an action. `photoUri` (for documents) is copied to safe storage first. */
    async add(type, jobId, payload = {}, photoUri = null) {
      await load();
      const id = newId();
      const photoRef = photoUri ? await photos.savePhoto(id, photoUri) : null;
      const item = { id, type, jobId, payload, photoRef, at: new Date().toISOString(), error: null };
      items.push(item);
      await save();
      return item;
    },

    /** Removes an item the server refused (the driver decided to drop it). */
    async discard(id) {
      await load();
      const item = items.find((i) => i.id === id);
      items = items.filter((i) => i.id !== id);
      await save();
      if (item?.photoRef) await photos.removePhoto(item.photoRef);
    },

    /**
     * Sends in order. A job whose item was refused stops there (later steps depend on it) until the driver
     * deals with it; other jobs carry on. Without a connection everything stays for the next try.
     * Resolves to { sent, waiting, offline }.
     */
    flush() {
      flushing ??= (async () => {
        await load();
        let sent = 0;
        let offline = false;
        const blocked = new Set();
        for (const item of [...items]) {
          if (blocked.has(item.jobId)) continue;
          if (item.error) { blocked.add(item.jobId); continue; }
          try {
            await send(item);
            items = items.filter((i) => i.id !== item.id);
            sent += 1;
            await save();
            if (item.photoRef) await photos.removePhoto(item.photoRef);
          } catch (e) {
            if (isRefusal(e)) {
              item.error = e.message;
              blocked.add(item.jobId);
              await save();
            } else {
              offline = true;
              break;
            }
          }
        }
        return { sent, waiting: items.length, offline };
      })().finally(() => { flushing = null; });
      return flushing;
    },
  };
}

export const outbox = createOutbox();
