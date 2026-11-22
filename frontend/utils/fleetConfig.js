export function parseFleetVehicles() {
  const raw = process.env.EXPO_PUBLIC_FLEET_VEHICLES || 'Truck-001,Truck-002,Truck-003';
  return raw
    .split(',')
    .map((id) => id.trim())
    .filter(Boolean);
}

/** Seed shipment IDs from backend migration — fallback when fleet snapshot lacks shipment context. */
const SEED_SHIPMENT_IDS = {
  'Truck-001': 'a1000000-0000-0000-0000-000000000001',
  'Truck-002': 'a2000000-0000-0000-0000-000000000002',
  'Truck-003': 'a3000000-0000-0000-0000-000000000003',
};

export function resolveShipmentIdForVehicle(vehicleId, vehicles = {}) {
  return vehicles[vehicleId]?.shipment?.shipmentId ?? SEED_SHIPMENT_IDS[vehicleId] ?? null;
}

/**
 * True when a sensor actually reported a value. Phone GPS packets carry no temperature or fuel (null);
 * in JavaScript `null < 2` is true, so every comparison must check this first.
 */
export function hasReading(value) {
  return typeof value === 'number' && Number.isFinite(value);
}

/** "4.2 °C", or a dash when no cargo sensor reported. */
export function formatTemp(value, unit = ' °C') {
  return hasReading(value) ? `${value}${unit}` : '—';
}

function averageOf(values) {
  const known = values.filter(hasReading);
  return known.length ? known.reduce((sum, v) => sum + v, 0) / known.length : null;
}

export function getVehicleStatus(telemetry) {
  if (!telemetry) return 'offline';
  if (telemetry.shipment) {
    const temp = telemetry.engineTemperature;
    const outOfRange = hasReading(temp)
      && (temp > telemetry.shipment.maxSafeTempCelsius || temp < telemetry.shipment.minSafeTempCelsius);
    if (outOfRange || lowFuel(telemetry)) return 'warning';
    return 'ok';
  }
  if (lowFuel(telemetry) || (hasReading(telemetry.engineTemperature) && telemetry.engineTemperature > 100)) return 'warning';
  return 'ok';
}

function lowFuel(telemetry) {
  return hasReading(telemetry.fuelLevel) && telemetry.fuelLevel < 30;
}

export function statusColor(status) {
  if (status === 'warning') return '#FF3B30';
  if (status === 'ok') return '#34C759';
  return '#8E8E93';
}

export function computeFleetSummary(vehicles, vehicleIds) {
  const active = vehicleIds
    .map((id) => vehicles[id])
    .filter(Boolean);

  if (active.length === 0) {
    return {
      configuredCount: vehicleIds.length,
      activeCount: 0,
      warningCount: 0,
      averageFuel: null,
      averageTemp: null,
    };
  }

  const warningCount = active.filter((t) => getVehicleStatus(t) === 'warning').length;
  const averageFuel = averageOf(active.map((t) => t.fuelLevel));
  const averageTemp = averageOf(active.map((t) => t.engineTemperature));

  return {
    configuredCount: vehicleIds.length,
    activeCount: active.length,
    warningCount,
    averageFuel: Math.round(averageFuel * 10) / 10,
    averageTemp: Math.round(averageTemp * 10) / 10,
  };
}
