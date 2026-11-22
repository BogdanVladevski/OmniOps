import { alertMeta, alertSummary, groupAlerts } from '../utils/alerts';

const at = (min) => new Date(Date.UTC(2026, 10, 21, 12, min)).toISOString();
const alert = (vehicleId, alertType, min, extra = {}) => ({
  id: `${vehicleId}-${alertType}-${min}`, vehicleId, alertType, generatedAt: at(min), instructions: '', ...extra,
});

describe('alertMeta', () => {
  it('treats cold-chain playbooks and temperature excursions as critical', () => {
    expect(alertMeta({ alertType: 'playbook' }).severity).toBe('critical');
    expect(alertMeta({ alertType: 'TemperatureExcursion' }).severity).toBe('critical');
  });

  it('treats a rising cargo temperature as a warning, not critical', () => {
    expect(alertMeta({ alertType: 'TemperatureTrend' }).severity).toBe('warning');
  });

  it('treats zone entry/exit as informational', () => {
    expect(alertMeta({ alertType: 'GeofenceBreach' }).severity).toBe('info');
  });

  it('falls back to a warning for unknown types', () => {
    expect(alertMeta({ alertType: 'Something' })).toMatchObject({ severity: 'warning', label: 'Something' });
  });
});

describe('alertSummary', () => {
  it('skips the response header, ticket and auto-generated note, and strips the severity tag', () => {
    const instructions = [
      'COLD-CHAIN INCIDENT RESPONSE ACTIVATED',
      'Ticket: CCM-Truck-001-20261121120000',
      '',
      '[Auto-generated from SOP — AI narrative unavailable]',
      '[CRITICAL] Insulin batch B-4471 outside safe range for 90s.',
    ].join('\n');
    expect(alertSummary({ instructions })).toBe('Insulin batch B-4471 outside safe range for 90s.');
  });

  it('returns an empty string when there is no body', () => {
    expect(alertSummary({})).toBe('');
  });
});

describe('groupAlerts', () => {
  it('collapses repeats of the same vehicle and type within the window, newest first', () => {
    const groups = groupAlerts([
      alert('Truck-001', 'GeofenceBreach', 9),
      alert('Truck-001', 'GeofenceBreach', 5),
      alert('Truck-001', 'GeofenceBreach', 1),
    ]);
    expect(groups).toHaveLength(1);
    expect(groups[0].count).toBe(3);
    expect(groups[0].alert.generatedAt).toBe(at(9));
  });

  it('keeps different vehicles and types apart', () => {
    const groups = groupAlerts([
      alert('Truck-001', 'GeofenceBreach', 3),
      alert('Truck-002', 'GeofenceBreach', 2),
      alert('Truck-001', 'playbook', 1),
    ]);
    expect(groups.map((g) => g.count)).toEqual([1, 1, 1]);
  });

  it('starts a new group once repeats are further apart than the window', () => {
    const groups = groupAlerts([alert('Truck-001', 'playbook', 30), alert('Truck-001', 'playbook', 0)], 10 * 60 * 1000);
    expect(groups).toHaveLength(2);
  });
});
