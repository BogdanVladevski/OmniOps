import { attentionItems, movingJobs, setupSteps } from '../utils/today';

const NOW = Date.parse('2026-11-22T12:00:00Z');
const ago = (min) => new Date(NOW - min * 60000).toISOString();
const job = (over) => ({ id: 'j', status: 'InProgress', customerName: 'Bitola Market', destination: 'Bitola', vehicleLabel: 'Truck-1', driverId: 'd', ...over });
const titles = (jobs, opts) => attentionItems(jobs, { now: NOW, ...opts }).map((i) => i.title);

describe('needs you', () => {
  it('is empty when everything is on track', () => {
    expect(titles([job({ lastPositionAtUtc: ago(2) }), job({ id: 'p', status: 'Planned' })])).toEqual([]);
  });

  it('puts a temperature problem first and says the reading', () => {
    const items = attentionItems([
      job({ id: 'a', lastPositionAtUtc: ago(30) }),
      job({ id: 'b', minTempCelsius: 2, maxTempCelsius: 8, temperatureOutOfRangeCount: 1, lastTemperatureCelsius: 9.5, lastTemperatureAtUtc: ago(5), lastPositionAtUtc: ago(1) }),
    ], { now: NOW });
    expect(items[0]).toMatchObject({ jobId: 'b', tone: 'critical', action: 'call' });
    expect(items[0].detail).toMatch(/9[.,]5 °C/);
    expect(items[1]).toMatchObject({ jobId: 'a', title: 'No signal from Truck-1' });
  });

  it('warns before free waiting time ends, then says it is chargeable', () => {
    expect(titles([job({ deliveryArrivedAtUtc: ago(100), lastPositionAtUtc: ago(1) })], { freeWaitingMinutes: 120 })).toEqual([]);
    expect(titles([job({ deliveryArrivedAtUtc: ago(110), lastPositionAtUtc: ago(1) })], { freeWaitingMinutes: 120 })[0]).toMatch(/^Free unloading time ends at/);
    expect(titles([job({ deliveryArrivedAtUtc: ago(135), lastPositionAtUtc: ago(1) })], { freeWaitingMinutes: 120 })[0])
      .toBe('Truck-1 waiting at Bitola for 2 h 15 min');
  });

  it('counts waiting at the pickup only before the trip starts', () => {
    expect(titles([job({ status: 'Planned', origin: 'Skopje depot', pickupArrivedAtUtc: ago(130) })])[0]).toMatch(/waiting at Skopje depot/);
    expect(titles([job({ pickupArrivedAtUtc: ago(300), lastPositionAtUtc: ago(1) })])).toEqual([]);
  });

  it('flags late, unstarted, unassigned jobs and reported problems', () => {
    const items = titles([
      job({ id: 'late', plannedDeliveryUtc: ago(20), lastPositionAtUtc: ago(1) }),
      job({ id: 'soon', status: 'Planned', plannedDeliveryUtc: new Date(NOW + 3600000).toISOString() }),
      job({ id: 'nodriver', status: 'Planned', driverId: null, customerName: 'Kam' }),
      job({ id: 'problem', status: 'Completed', exceptionCount: 1, customerName: 'Tinex' }),
    ]);
    expect(items).toEqual(['Problem reported — Tinex', 'Late — Bitola Market', 'Not started yet — Bitola Market', 'No driver for Kam']);
  });

  it('does not call a truck that already arrived late', () => {
    expect(titles([job({ plannedDeliveryUtc: ago(20), deliveryArrivedAtUtc: ago(10), lastPositionAtUtc: ago(1) })])).toEqual([]);
  });
});

describe('moving', () => {
  it('lists late and silent trucks first, then by arrival', () => {
    const order = movingJobs([
      job({ id: 'soon', etaUtc: new Date(NOW + 600000).toISOString(), lastPositionAtUtc: ago(1) }),
      job({ id: 'later', etaUtc: new Date(NOW + 3600000).toISOString(), lastPositionAtUtc: ago(1) }),
      job({ id: 'silent', lastPositionAtUtc: ago(40) }),
      job({ id: 'late', plannedDeliveryUtc: ago(5), lastPositionAtUtc: ago(1) }),
      job({ id: 'done', status: 'Completed' }),
    ], NOW).map((j) => j.id);
    expect(order).toEqual(['late', 'silent', 'soon', 'later']);
  });
});

describe('getting started', () => {
  it('ticks each step off as the company sets up', () => {
    expect(setupSteps({}).filter((s) => s.done)).toEqual([]);
    const steps = setupSteps({ vehicles: 2, drivers: 1, jobs: [{ status: 'InProgress' }], invoiceSettingsDone: false });
    expect(steps.map((s) => s.done)).toEqual([true, true, true, true, false]);
  });
});
