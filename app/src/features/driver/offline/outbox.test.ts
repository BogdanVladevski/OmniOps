import { ApiError } from '@/api/client';
import type { Job } from '@/api/types';
import { applyPending, createOutbox, isRefusal, type OutboxItem } from './outbox';

jest.mock('./photo-store', () => ({}));
jest.mock('../driver-api', () => ({ driverApi: {} }));

function memoryStorage() {
  const data: Record<string, string> = {};
  return { getItem: async (k: string) => data[k] ?? null, setItem: async (k: string, v: string) => void (data[k] = v) };
}

function fakePhotos() {
  const stored: Record<string, string> = {};
  return {
    stored,
    savePhoto: jest.fn(async (id: string, uri: string) => ((stored[id] = uri), `ref:${id}`)),
    loadPhoto: jest.fn(async (ref: string) => stored[ref.slice(4)] ?? null),
    removePhoto: jest.fn(async (ref: string | null) => void (ref && delete stored[ref.slice(4)])),
  };
}

const offline = () => {
  throw new TypeError('Network request failed');
};

describe('outbox', () => {
  it('keeps everything while offline and sends it in order, with ids and times, when back online', async () => {
    let online = false;
    const calls: unknown[][] = [];
    const send = {
      start: jest.fn(async (jobId: string, at?: string) => (online ? calls.push(['start', jobId, at]) : offline())),
      upload: jest.fn(async (_jobId: string, kind: string, uri: string, extra: object) =>
        online ? calls.push(['doc', kind, uri, extra]) : offline(),
      ),
      deliver: jest.fn(async (_jobId: string, body: object) => (online ? calls.push(['deliver', body]) : offline())),
    };
    const photos = fakePhotos();
    const box = createOutbox({ storage: memoryStorage(), send: send as never, photos });

    await box.add('j1', { type: 'start' });
    const photo = await box.add('j1', {
      type: 'document',
      payload: { kind: 'DeliveryPhoto' },
      photoUri: 'file:///camera/1.jpg',
    });
    await box.add('j1', { type: 'deliver', payload: { recipientName: 'Vesna' } });

    expect(await box.flush()).toEqual({ sent: 0, waiting: 3, offline: true });
    online = true;
    expect(await box.flush()).toEqual({ sent: 3, waiting: 0, offline: false });
    expect(calls.map((c) => c[0])).toEqual(['start', 'doc', 'deliver']);
    expect(calls[1]![2]).toBe('file:///camera/1.jpg');
    expect(calls[1]![3]).toMatchObject({ clientId: photo.id, capturedAt: photo.at });
    expect((calls[2]![1] as { deliveredAt: string }).deliveredAt).toBeTruthy();
    expect(photos.removePhoto).toHaveBeenCalledWith(`ref:${photo.id}`);
  });

  it('stops a job at a refused step but carries on with other jobs', async () => {
    const send = {
      start: jest.fn(async () => {}),
      deliver: jest.fn(async (jobId: string) => {
        if (jobId === 'j1') throw new ApiError('Log the load temperature first.', 409);
      }),
    };
    const box = createOutbox({ storage: memoryStorage(), send: send as never, photos: fakePhotos() });
    await box.add('j1', { type: 'deliver', payload: { recipientName: 'A' } });
    await box.add('j1', { type: 'start' });
    await box.add('j2', { type: 'start' });

    expect(await box.flush()).toEqual({ sent: 1, waiting: 2, offline: false });
    const [refused, waiting] = await box.list();
    expect(refused!.error).toBe('Log the load temperature first.');
    expect(waiting!.error).toBeNull();

    await box.discard(refused!.id);
    expect(await box.flush()).toEqual({ sent: 1, waiting: 0, offline: false });
  });

  it('survives an app restart', async () => {
    const storage = memoryStorage();
    await createOutbox({ storage, send: {} as never, photos: fakePhotos() }).add('j1', { type: 'start' });
    expect(await createOutbox({ storage, send: {} as never, photos: fakePhotos() }).list()).toHaveLength(1);
  });
});

describe('isRefusal', () => {
  it('treats no connection, expired sign-in and server trouble as "try again later"', () => {
    expect(isRefusal(new TypeError('Network request failed'))).toBe(false);
    expect(isRefusal(new ApiError('x', 401))).toBe(false);
    expect(isRefusal(new ApiError('x', 503))).toBe(false);
    expect(isRefusal(new ApiError('x', 429))).toBe(false);
    expect(isRefusal(new ApiError('x', 409))).toBe(true);
    expect(isRefusal(new ApiError('x', 400))).toBe(true);
  });
});

describe('applyPending', () => {
  const job = {
    id: 'j1',
    status: 'Planned',
    photoCount: 0,
    pickupDocumentCount: 0,
    signatureCount: 0,
    exceptionCount: 0,
    temperatureReadingCount: 0,
  } as Job;
  const item = (over: Partial<OutboxItem>) =>
    ({ jobId: 'j1', id: 'x', error: null, photoRef: null, payload: {}, ...over }) as OutboxItem;

  it('shows what the driver did before it reached the server', () => {
    const shown = applyPending(job, [
      item({ type: 'start', at: '2026-11-22T08:00:00Z' }),
      item({ type: 'document', at: '2026-11-22T09:00:00Z', payload: { kind: 'DeliveryPhoto' } }),
      item({
        type: 'document',
        at: '2026-11-22T09:01:00Z',
        payload: { kind: 'TemperaturePhoto', temperatureCelsius: 4 },
      }),
      item({ type: 'deliver', at: '2026-11-22T09:02:00Z', payload: { recipientName: 'Vesna' } }),
      item({ type: 'start', at: '2026-11-22T09:03:00Z', jobId: 'other' }),
    ]);
    expect(shown).toMatchObject({
      status: 'Delivered',
      startedAtUtc: '2026-11-22T08:00:00Z',
      photoCount: 1,
      lastTemperatureCelsius: 4,
      recipientName: 'Vesna',
      deliveryPending: true,
      pendingCount: 4,
    });
  });

  it('shows an arrival before it is sent, keeping the earlier one', () => {
    const shown = applyPending({ ...job, status: 'InProgress', deliveryArrivedAtUtc: '2026-11-22T10:00:00Z' }, [
      item({ type: 'arrive', at: '2026-11-22T10:05:00Z', payload: { stop: 'delivery' } }),
      item({ type: 'arrive', at: '2026-11-22T07:00:00Z', payload: { stop: 'pickup' } }),
    ]);
    expect(shown.deliveryArrivedAtUtc).toBe('2026-11-22T10:00:00Z');
    expect(shown.pickupArrivedAtUtc).toBe('2026-11-22T07:00:00Z');
  });

  it('leaves a job with nothing waiting untouched', () => {
    expect(applyPending(job, [])).toBe(job);
  });
});
