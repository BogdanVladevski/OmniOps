import { applyPending, createOutbox, isRefusal } from '../utils/outbox';

jest.mock('../utils/api', () => ({ driverApi: {} }));
jest.mock('../utils/photoStore', () => ({}));

function memoryStorage() {
  const data = {};
  return { getItem: async (k) => data[k] ?? null, setItem: async (k, v) => { data[k] = v; }, data };
}

const refusal = (status, message) => Object.assign(new Error(message), { status });

function setup(api) {
  const photos = {
    stored: {},
    savePhoto: jest.fn(async (id, uri) => { photos.stored[id] = uri; return `ref:${id}`; }),
    loadPhoto: jest.fn(async (ref) => photos.stored[ref.slice(4)] ?? null),
    removePhoto: jest.fn(async (ref) => { delete photos.stored[ref.slice(4)]; }),
  };
  const storage = memoryStorage();
  return { box: createOutbox({ storage, api, photos }), photos, storage };
}

describe('outbox', () => {
  it('keeps everything while offline and sends it in order, with ids and times, when back online', async () => {
    let online = false;
    const calls = [];
    const net = () => { if (!online) throw new TypeError('Network request failed'); };
    const api = {
      start: jest.fn(async (jobId, at) => { net(); calls.push(['start', jobId, at]); }),
      uploadDocument: jest.fn(async (jobId, kind, photo, coords, t, extra) => { net(); calls.push(['doc', kind, photo.uri, extra]); }),
      deliver: jest.fn(async (jobId, body) => { net(); calls.push(['deliver', body]); }),
    };
    const { box, photos } = setup(api);

    await box.add('start', 'j1');
    const photo = await box.add('document', 'j1', { kind: 'DeliveryPhoto' }, 'file:///camera/1.jpg');
    await box.add('deliver', 'j1', { recipientName: 'Vesna' });

    expect(await box.flush()).toEqual({ sent: 0, waiting: 3, offline: true });
    expect(await box.list()).toHaveLength(3);

    online = true;
    expect(await box.flush()).toEqual({ sent: 3, waiting: 0, offline: false });
    expect(calls.map((c) => c[0])).toEqual(['start', 'doc', 'deliver']);
    expect(calls[1][2]).toBe('file:///camera/1.jpg');
    expect(calls[1][3]).toEqual({ clientId: photo.id, capturedAt: photo.at, note: undefined });
    expect(calls[2][1].deliveredAt).toBeTruthy();
    expect(photos.removePhoto).toHaveBeenCalledWith(`ref:${photo.id}`);
  });

  it('stops a job at a refused step but carries on with other jobs', async () => {
    const api = {
      start: jest.fn(async () => {}),
      deliver: jest.fn(async (jobId) => { if (jobId === 'j1') throw refusal(409, 'Log the load temperature first.'); }),
    };
    const { box } = setup(api);
    await box.add('deliver', 'j1', { recipientName: 'A' });
    await box.add('start', 'j1');
    await box.add('start', 'j2');

    const result = await box.flush();
    expect(result).toEqual({ sent: 1, waiting: 2, offline: false });
    const [refused, waiting] = await box.list();
    expect(refused.error).toBe('Log the load temperature first.');
    expect(waiting.error).toBeNull();
    expect(api.start).toHaveBeenCalledTimes(1);

    await box.discard(refused.id);
    expect(await box.flush()).toEqual({ sent: 1, waiting: 0, offline: false });
  });

  it('survives an app restart', async () => {
    const { storage } = setup({});
    const first = createOutbox({ storage, api: {}, photos: { savePhoto: async () => 'r' } });
    await first.add('start', 'j1');
    const second = createOutbox({ storage, api: { start: jest.fn(async () => {}) }, photos: {} });
    expect(await second.list()).toHaveLength(1);
  });
});

describe('isRefusal', () => {
  it('treats no connection, expired sign-in and server trouble as "try again later"', () => {
    expect(isRefusal(new TypeError('Network request failed'))).toBe(false);
    expect(isRefusal(refusal(401, 'x'))).toBe(false);
    expect(isRefusal(refusal(503, 'x'))).toBe(false);
    expect(isRefusal(refusal(429, 'x'))).toBe(false);
    expect(isRefusal(refusal(409, 'x'))).toBe(true);
    expect(isRefusal(refusal(400, 'x'))).toBe(true);
  });
});

describe('applyPending', () => {
  const job = { id: 'j1', status: 'Planned', photoCount: 0 };
  it('shows what the driver did before it reached the server', () => {
    const shown = applyPending(job, [
      { jobId: 'j1', type: 'start', at: '2026-11-22T08:00:00Z', payload: {} },
      { jobId: 'j1', type: 'document', at: '2026-11-22T09:00:00Z', payload: { kind: 'DeliveryPhoto' } },
      { jobId: 'j1', type: 'document', at: '2026-11-22T09:01:00Z', payload: { kind: 'TemperaturePhoto', temperatureCelsius: 4 } },
      { jobId: 'j1', type: 'deliver', at: '2026-11-22T09:02:00Z', payload: { recipientName: 'Vesna' } },
      { jobId: 'other', type: 'start', at: '2026-11-22T09:03:00Z', payload: {} },
    ]);
    expect(shown).toMatchObject({
      status: 'Completed', startedAtUtc: '2026-11-22T08:00:00Z', photoCount: 1, lastTemperatureCelsius: 4,
      recipientName: 'Vesna', deliveryPending: true, pendingCount: 4,
    });
  });

  it('shows an arrival before it is sent, keeping the earlier one', () => {
    const shown = applyPending({ id: 'j1', status: 'InProgress', deliveryArrivedAtUtc: '2026-11-22T10:00:00Z' }, [
      { jobId: 'j1', type: 'arrive', at: '2026-11-22T10:05:00Z', payload: { stop: 'delivery' } },
      { jobId: 'j1', type: 'arrive', at: '2026-11-22T07:00:00Z', payload: { stop: 'pickup' } },
    ]);
    expect(shown.deliveryArrivedAtUtc).toBe('2026-11-22T10:00:00Z');
    expect(shown.pickupArrivedAtUtc).toBe('2026-11-22T07:00:00Z');
  });

  it('leaves a job with nothing waiting untouched', () => {
    expect(applyPending(job, [])).toBe(job);
  });
});
