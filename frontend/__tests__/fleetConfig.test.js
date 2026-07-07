import { computeFleetSummary, getVehicleStatus, parseFleetVehicles, statusColor } from '../utils/fleetConfig';
import { parseApiError, resolveApiBaseUrl } from '../utils/api';

jest.mock('expo-constants', () => ({
  isDevice: true,
  expoGoConfig: { debuggerHost: '192.168.1.173:8081' },
}));

describe('resolveApiBaseUrl', () => {
  test('rewrites localhost using Expo debugger host on device', () => {
    expect(resolveApiBaseUrl('http://localhost:5031')).toBe('http://192.168.1.173:5031');
  });
});

describe('fleetConfig', () => {
  test('parseFleetVehicles splits comma list', () => {
    process.env.EXPO_PUBLIC_FLEET_VEHICLES = 'Truck-001, Truck-002';
    expect(parseFleetVehicles()).toEqual(['Truck-001', 'Truck-002']);
  });

  test('getVehicleStatus detects cold-chain excursion', () => {
    const telemetry = {
      fuelLevel: 80,
      engineTemperature: 12,
      shipment: { minSafeTempCelsius: 2, maxSafeTempCelsius: 8 },
    };
    expect(getVehicleStatus(telemetry)).toBe('warning');
  });

  test('computeFleetSummary aggregates active vehicles', () => {
    const vehicles = {
      'Truck-001': { fuelLevel: 50, engineTemperature: 5 },
      'Truck-002': { fuelLevel: 70, engineTemperature: 6 },
    };
    const summary = computeFleetSummary(vehicles, ['Truck-001', 'Truck-002', 'Truck-003']);
    expect(summary.activeCount).toBe(2);
    expect(summary.configuredCount).toBe(3);
    expect(summary.averageFuel).toBe(60);
  });

  test('statusColor maps statuses', () => {
    expect(statusColor('warning')).toBe('#FF3B30');
    expect(statusColor('ok')).toBe('#34C759');
  });
});

describe('parseApiError', () => {
  test('parses problem+json style body', () => {
    expect(parseApiError(400, JSON.stringify({ title: 'Bad', detail: 'Invalid fleet' }))).toBe('Invalid fleet');
  });

  test('extracts aspnet exception from html', () => {
    const html = '<!DOCTYPE html>System.InvalidOperationException: Auth missing';
    expect(parseApiError(500, html)).toContain('Auth missing');
  });
});
