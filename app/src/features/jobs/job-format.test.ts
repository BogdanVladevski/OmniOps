import type { Job } from '@/api/types';
import { aJob } from '@/test/factories';
import {
  appendPending,
  formatRange,
  groupJobsForToday,
  hasRecentReading,
  isOutOfRange,
  isPositionStale,
  jobProgress,
  jobStatusMeta,
  jobTitle,
  parseTemperatureInput,
  removeSent,
  temperatureStatus,
  toLocationPoint,
} from './job-format';

describe('toLocationPoint', () => {
  const reading = (
    coords: {
      latitude: number;
      longitude: number;
      speed?: number | null;
      heading?: number | null;
      accuracy?: number | null;
    },
    timestamp = Date.UTC(2026, 10, 22, 9, 30),
  ) => ({ coords, timestamp });

  it('converts speed from m/s to km/h and keeps the reading time', () => {
    const p = toLocationPoint(
      reading({ latitude: 41.99, longitude: 21.43, speed: 20, heading: 180, accuracy: 6 }),
      'id-1',
    );
    expect(p).toEqual({
      id: 'id-1',
      latitude: 41.99,
      longitude: 21.43,
      speedKmh: 72,
      heading: 180,
      accuracyMeters: 6,
      recordedAtUtc: '2026-11-22T09:30:00.000Z',
    });
  });

  it('reports unknown speed and heading as null, not zero', () => {
    // iOS reports -1 and some Androids null when the value is unknown.
    const p = toLocationPoint(reading({ latitude: 1, longitude: 2, speed: -1, heading: null, accuracy: -1 }));
    expect(p.speedKmh).toBeNull();
    expect(p.heading).toBeNull();
    expect(p.accuracyMeters).toBeNull();
  });

  it('gives every point a unique id so resends can be recognised', () => {
    const a = toLocationPoint(reading({ latitude: 1, longitude: 2 }));
    const b = toLocationPoint(reading({ latitude: 1, longitude: 2 }));
    expect(a.id).not.toEqual(b.id);
  });
});

describe('pending queue', () => {
  it('drops the oldest points beyond the cap', () => {
    const q = appendPending([{ id: 'a' }, { id: 'b' }], [{ id: 'c' }], 2);
    expect(q.map((p) => p.id)).toEqual(['b', 'c']);
  });

  it('removes only acknowledged points', () => {
    expect(removeSent([{ id: 'a' }, { id: 'b' }, { id: 'c' }], ['a', 'c']).map((p) => p.id)).toEqual(['b']);
  });
});

describe('job display', () => {
  it('uses plain words for statuses', () => {
    expect(jobStatusMeta('InProgress').label).toBe('On the way');
    expect(jobStatusMeta('Delivered').label).toBe('Delivered');
  });

  it('titles a job by customer, falling back to destination', () => {
    expect(jobTitle({ customerName: 'Apteka', destination: 'Bitola' })).toBe('Apteka');
    expect(jobTitle({ destination: 'Bitola' })).toBe('Bitola');
  });

  it('flags a running job whose phone stopped reporting', () => {
    const now = Date.UTC(2026, 10, 22, 10, 0);
    const job = (minutesAgo: number) =>
      aJob({ status: 'InProgress', lastPositionAtUtc: new Date(now - minutesAgo * 60000).toISOString() });
    expect(isPositionStale(job(2), now)).toBe(false);
    expect(isPositionStale(job(25), now)).toBe(true);
    expect(isPositionStale({ ...job(25), status: 'Delivered' }, now)).toBe(false);
  });
});

describe('live map grouping', () => {
  const now = Date.UTC(2026, 11, 1, 12, 0);
  const iso = (minutes: number) => new Date(now + minutes * 60000).toISOString();
  const jobs = [
    aJob({ id: 'late', status: 'InProgress', lastPositionAtUtc: iso(-1), etaUtc: iso(90), remainingKm: 80 }),
    aJob({ id: 'soon', status: 'InProgress', lastPositionAtUtc: iso(-1), etaUtc: iso(20), remainingKm: 15 }),
    aJob({ id: 'silent', status: 'InProgress', lastPositionAtUtc: iso(-45), etaUtc: iso(-10), remainingKm: 30 }),
    aJob({ id: 'plan', status: 'Planned' }),
    aJob({ id: 'done-early', status: 'Delivered', deliveredAtUtc: iso(-120) }),
    aJob({ id: 'done-late', status: 'Delivered', deliveredAtUtc: iso(-30) }),
  ];

  it('puts silent trucks first, then the soonest arrival', () => {
    expect(groupJobsForToday(jobs, now).onTheWay.map((j) => j.id)).toEqual(['silent', 'soon', 'late']);
  });

  it('lists the latest delivery first', () => {
    expect(groupJobsForToday(jobs, now).delivered.map((j) => j.id)).toEqual(['done-late', 'done-early']);
  });

  it('never shows an arrival time for a truck that stopped reporting', () => {
    const p = jobProgress(jobs[2]!, now);
    expect(p.primary).toBe('No signal');
    expect(p.tone).toBe('warning');
  });

  it('shows distance left for a reporting truck', () => {
    expect(jobProgress(jobs[1]!, now).secondary).toBe('about 15 km');
  });
});

describe('temperature log', () => {
  const now = Date.UTC(2026, 11, 1, 12, 0);
  const chilled: Job = aJob({
    minTempCelsius: 2,
    maxTempCelsius: 8,
    status: 'InProgress',
    temperatureReadingCount: 1,
    temperatureOutOfRangeCount: 0,
  });

  it('reads what drivers type from the display, including a decimal comma', () => {
    expect(parseTemperatureInput('4,5')).toBe(4.5);
    expect(parseTemperatureInput(' -18 ')).toBe(-18);
    expect(parseTemperatureInput('−20,0')).toBe(-20);
    expect(parseTemperatureInput('5 °C')).toBe(5);
    expect(parseTemperatureInput('+3')).toBe(3);
  });

  it('refuses things that are not a plausible reading', () => {
    ['', 'abc', '45', '4.5.1', '-41', '1e1'].forEach((t) => expect(parseTemperatureInput(t)).toBeNull());
  });

  it('flags readings outside the range only for refrigerated jobs', () => {
    expect(isOutOfRange(chilled, 9)).toBe(true);
    expect(isOutOfRange(chilled, 8)).toBe(false);
    expect(isOutOfRange({}, 30)).toBe(false);
  });

  it('puts a bad reading above an overdue check', () => {
    const job = {
      ...chilled,
      temperatureOutOfRangeCount: 1,
      temperatureCheckDueUtc: new Date(now - 60000).toISOString(),
    };
    expect(temperatureStatus(job, now)?.tone).toBe('critical');
    expect(formatRange(job)).toBe('2 to 8 °C');
  });

  it('warns when a check is overdue', () => {
    expect(
      temperatureStatus({ ...chilled, temperatureCheckDueUtc: new Date(now - 60000).toISOString() }, now)?.label,
    ).toBe('Temperature check overdue');
  });

  it('says nothing about temperature for ordinary loads', () => {
    expect(temperatureStatus(aJob({ status: 'InProgress' }), now)).toBeNull();
  });

  it('accepts a reading from the last 30 minutes for delivery', () => {
    expect(hasRecentReading(new Date(now - 10 * 60000).toISOString(), now)).toBe(true);
    expect(hasRecentReading(new Date(now - 45 * 60000).toISOString(), now)).toBe(false);
    expect(hasRecentReading(null, now)).toBe(false);
  });
});
