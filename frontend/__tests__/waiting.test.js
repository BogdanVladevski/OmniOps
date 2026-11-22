import { evidence, formatDuration, minutesSince } from '../utils/waiting';

describe('waiting time wording', () => {
  it('formats durations the way people say them', () => {
    expect(formatDuration(45)).toBe('45 min');
    expect(formatDuration(120)).toBe('2 h');
    expect(formatDuration(135)).toBe('2 h 15 min');
    expect(formatDuration(65)).toBe('1 h 05 min');
    expect(formatDuration(65, { h: 'ч', min: 'мин' })).toBe('1 ч 05 мин');
    expect(formatDuration(null)).toBe('0 min');
  });

  it('counts a running wait up to now', () => {
    expect(minutesSince('2026-11-22T10:00:00Z', Date.parse('2026-11-22T11:30:59Z'))).toBe(90);
  });

  it('says how the arrival was recorded, and when a tap is too far to trust', () => {
    expect(evidence({ source: 'Gps' })).toMatch(/GPS/);
    expect(evidence({ source: 'Driver', distanceFromStopMeters: 80, verified: true })).toMatch(/80 m from the address$/);
    expect(evidence({ source: 'Driver', distanceFromStopMeters: 6400, verified: false })).toMatch(/6\.4 km .*too far/);
  });
});
