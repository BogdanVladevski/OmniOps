// Everything a driver does on a job (start, arrived, photos, temperature readings, signature, problems, delivery) is
// saved on the phone first and sent in order when there is signal. Each item has its own id, which the server uses to
// ignore a resend, so retrying after a dropped connection is always safe.
import AsyncStorage from '@react-native-async-storage/async-storage';
import { ApiError } from '@/api/client';
import type { DocumentKind, Job } from '@/api/types';
import { newId } from '@/features/jobs/job-format';
import { driverApi, type Coords } from '../driver-api';
import { loadPhoto, removePhoto, savePhoto } from './photo-store';

const KEY = 'omniops:outbox';

export type OutboxItem =
  | {
      id: string;
      at: string;
      jobId: string;
      error: string | null;
      photoRef: null;
      type: 'start';
      payload: Record<string, never>;
    }
  | {
      id: string;
      at: string;
      jobId: string;
      error: string | null;
      photoRef: null;
      type: 'arrive';
      payload: { stop: 'pickup' | 'delivery'; latitude?: number | null; longitude?: number | null };
    }
  | {
      id: string;
      at: string;
      jobId: string;
      error: string | null;
      photoRef: null;
      type: 'deliver';
      payload: { recipientName: string; note?: string | null; latitude?: number | null; longitude?: number | null };
    }
  | {
      id: string;
      at: string;
      jobId: string;
      error: string | null;
      photoRef: string | null;
      type: 'document';
      payload: { kind: DocumentKind; coords?: Coords | null; temperatureCelsius?: number | null; note?: string | null };
    };

export type OutboxAction =
  | { type: 'start'; payload?: Record<string, never> }
  | { type: 'arrive'; payload: Extract<OutboxItem, { type: 'arrive' }>['payload'] }
  | { type: 'deliver'; payload: Extract<OutboxItem, { type: 'deliver' }>['payload'] }
  | { type: 'document'; payload: Extract<OutboxItem, { type: 'document' }>['payload']; photoUri: string };

/** True for "the server said no" (keep the item, show why); false for no connection or trouble (retry later). */
export const isRefusal = (error: unknown) =>
  error instanceof ApiError && error.status !== 401 && error.status !== 429 && error.status < 500;

/** Applies what is still waiting to a job as the server returned it, so the screen shows what the driver did offline. */
export function applyPending(
  job: Job,
  items: OutboxItem[],
): Job & { pendingCount?: number; deliveryPending?: boolean } {
  const mine = items.filter((i) => i.jobId === job.id);
  if (!mine.length) return job;
  const next: Job & { pendingCount?: number; deliveryPending?: boolean } = { ...job, pendingCount: mine.length };
  for (const item of mine) {
    if (item.type === 'arrive') {
      const field = item.payload.stop === 'pickup' ? 'pickupArrivedAtUtc' : 'deliveryArrivedAtUtc';
      if (!next[field] || item.at < next[field]!) next[field] = item.at;
    } else if (item.type === 'start' && next.status === 'Planned') {
      next.status = 'InProgress';
      next.startedAtUtc = item.at;
    } else if (item.type === 'deliver' && next.status === 'InProgress') {
      next.status = 'Delivered';
      next.deliveredAtUtc = item.at;
      next.recipientName = item.payload.recipientName;
      next.deliveryPending = true;
    } else if (item.type === 'document') {
      const { kind, temperatureCelsius } = item.payload;
      if (kind === 'DeliveryPhoto') next.photoCount += 1;
      else if (kind === 'PickupDocument') next.pickupDocumentCount += 1;
      else if (kind === 'Signature') next.signatureCount += 1;
      else if (kind === 'Exception') next.exceptionCount += 1;
      else if (kind === 'TemperaturePhoto') {
        next.lastTemperatureCelsius = temperatureCelsius ?? null;
        next.lastTemperatureAtUtc = item.at;
        next.temperatureReadingCount += 1;
      }
    }
  }
  return next;
}

interface Storage {
  getItem(key: string): Promise<string | null>;
  setItem(key: string, value: string): Promise<void>;
}

interface Photos {
  savePhoto(id: string, uri: string): Promise<string>;
  loadPhoto(ref: string): Promise<string | null>;
  removePhoto(ref: string | null): Promise<void>;
}

export interface FlushResult {
  sent: number;
  waiting: number;
  offline: boolean;
}

/** The outbox, with storage, sender and photo store injectable for tests. */
export function createOutbox({
  storage = AsyncStorage as Storage,
  send = driverApi,
  photos = { savePhoto, loadPhoto, removePhoto } as Photos,
} = {}) {
  const listeners = new Set<(items: OutboxItem[]) => void>();
  let items: OutboxItem[] | null = null;
  let flushing: Promise<FlushResult> | null = null;

  const load = async (): Promise<OutboxItem[]> => {
    if (items) return items;
    try {
      items = JSON.parse((await storage.getItem(KEY)) ?? '[]') as OutboxItem[];
    } catch {
      items = [];
    }
    return items;
  };
  const save = async () => {
    await storage.setItem(KEY, JSON.stringify(items));
    listeners.forEach((listener) => listener([...items!]));
  };

  async function deliver(item: OutboxItem) {
    switch (item.type) {
      case 'start':
        return send.start(item.jobId, item.at);
      case 'arrive':
        return send.arrived(item.jobId, { ...item.payload, arrivedAt: item.at });
      case 'deliver':
        return send.deliver(item.jobId, { ...item.payload, deliveredAt: item.at });
      case 'document': {
        const uri = item.photoRef ? await photos.loadPhoto(item.photoRef) : null;
        if (!uri) throw new ApiError('The photo is no longer on this phone. Take it again.', 410);
        return send.upload(item.jobId, item.payload.kind, uri, {
          ...item.payload,
          clientId: item.id,
          capturedAt: item.at,
        });
      }
    }
  }

  return {
    subscribe(listener: (items: OutboxItem[]) => void) {
      listeners.add(listener);
      load().then((list) => listener([...list]));
      return () => {
        listeners.delete(listener);
      };
    },

    async list() {
      return [...(await load())];
    },

    /** Adds an action; a photo is copied to safe storage first. */
    async add(jobId: string, action: OutboxAction): Promise<OutboxItem> {
      await load();
      const id = newId();
      const photoRef = action.type === 'document' ? await photos.savePhoto(id, action.photoUri) : null;
      const item = {
        id,
        jobId,
        at: new Date().toISOString(),
        error: null,
        photoRef,
        type: action.type,
        payload: action.payload ?? {},
      } as OutboxItem;
      items!.push(item);
      await save();
      return item;
    },

    /** Drops an item the server refused (the driver decided to). */
    async discard(id: string) {
      await load();
      const item = items!.find((i) => i.id === id);
      items = items!.filter((i) => i.id !== id);
      await save();
      if (item?.photoRef) await photos.removePhoto(item.photoRef);
    },

    /**
     * Sends in order. A job whose item was refused stops there (later steps depend on it) until the driver deals with
     * it; other jobs carry on. Without a connection everything stays for the next try.
     */
    flush(): Promise<FlushResult> {
      flushing ??= (async () => {
        await load();
        let sent = 0;
        let offline = false;
        const blocked = new Set<string>();
        for (const item of [...items!]) {
          if (blocked.has(item.jobId)) continue;
          if (item.error) {
            blocked.add(item.jobId);
            continue;
          }
          try {
            await deliver(item);
            items = items!.filter((i) => i.id !== item.id);
            sent += 1;
            await save();
            if (item.photoRef) await photos.removePhoto(item.photoRef);
          } catch (error) {
            if (isRefusal(error)) {
              item.error = (error as Error).message;
              blocked.add(item.jobId);
              await save();
            } else {
              offline = true;
              break;
            }
          }
        }
        return { sent, waiting: items!.length, offline };
      })().finally(() => {
        flushing = null;
      });
      return flushing;
    },
  };
}

export const outbox = createOutbox();
