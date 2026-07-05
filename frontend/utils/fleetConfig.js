export function parseFleetVehicles() {
  const raw = process.env.EXPO_PUBLIC_FLEET_VEHICLES || 'Truck-001,Truck-002,Truck-003';
  return raw
    .split(',')
    .map((id) => id.trim())
    .filter(Boolean);
}

export function getVehicleStatus(telemetry) {
  if (!telemetry) return 'offline';
  if (telemetry.shipment) {
    const temp = telemetry.engineTemperature;
    const outOfRange =
      temp > telemetry.shipment.maxSafeTempCelsius ||
      temp < telemetry.shipment.minSafeTempCelsius;
    if (outOfRange || telemetry.fuelLevel < 30) return 'warning';
    return 'ok';
  }
  if (telemetry.fuelLevel < 30 || telemetry.engineTemperature > 100) return 'warning';
  return 'ok';
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
  const averageFuel = active.reduce((sum, t) => sum + t.fuelLevel, 0) / active.length;
  const averageTemp = active.reduce((sum, t) => sum + t.engineTemperature, 0) / active.length;

  return {
    configuredCount: vehicleIds.length,
    activeCount: active.length,
    warningCount,
    averageFuel: Math.round(averageFuel * 10) / 10,
    averageTemp: Math.round(averageTemp * 10) / 10,
  };
}
