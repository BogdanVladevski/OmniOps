// Alert presentation rules shared by the Overview and Alerts screens.

const TYPES = {
  playbook: { label: 'Cold-chain response', severity: 'critical', icon: 'snow' },
  TemperatureExcursion: { label: 'Temperature excursion', severity: 'critical', icon: 'thermometer' },
  TemperatureTrend: { label: 'Temperature rising', severity: 'warning', icon: 'trending-up' },
  SensorFailure: { label: 'Sensor problem', severity: 'warning', icon: 'hardware-chip-outline' },
  HarshBraking: { label: 'Harsh braking', severity: 'warning', icon: 'warning-outline' },
  Overspeed: { label: 'Speeding', severity: 'warning', icon: 'speedometer-outline' },
  HarshAcceleration: { label: 'Harsh acceleration', severity: 'info', icon: 'trending-up' },
  LongIdle: { label: 'Long idle', severity: 'info', icon: 'pause-circle-outline' },
  GeofenceBreach: { label: 'Zone entry / exit', severity: 'info', icon: 'location-outline' },
  JobDelivered: { label: 'Delivered', severity: 'info', icon: 'checkmark-circle-outline' },
};

export function alertMeta(alert) {
  return TYPES[alert?.alertType] ?? { label: alert?.alertType ?? 'Alert', severity: 'warning', icon: 'alert-circle-outline' };
}

export const SEVERITY_LABEL = { critical: 'Critical', warning: 'Warning', info: 'Info' };

/** First meaningful line of an alert body, without ticket boilerplate. */
export function alertSummary(alert) {
  const lines = String(alert?.instructions ?? '')
    .split('\n')
    .map((l) => l.trim())
    .filter(Boolean);
  const useful = lines.find(
    (l) => !/^ticket:/i.test(l) && !/^cold-chain incident response activated/i.test(l) && !/^\[auto-generated/i.test(l),
  );
  return (useful ?? lines[0] ?? '').replace(/^\[(critical|warning)\]\s*/i, '');
}

/**
 * Collapses repeats of the same alert (same vehicle, type and title) arriving within `windowMs`
 * of each other into one entry with a count, so a flapping zone sensor cannot bury a critical alert.
 * Input is newest-first; output keeps that order.
 */
export function groupAlerts(alerts, windowMs = 10 * 60 * 1000) {
  const groups = [];
  const open = new Map();
  for (const alert of alerts ?? []) {
    const meta = alertMeta(alert);
    const key = `${alert.vehicleId}|${alert.alertType}|${meta.label}`;
    const t = new Date(alert.generatedAt).getTime();
    const g = open.get(key);
    if (g && Math.abs(g.oldestAt - t) <= windowMs) {
      g.count += 1;
      g.oldestAt = Math.min(g.oldestAt, t);
      continue;
    }
    const group = { key: `${key}|${alert.id ?? t}`, alert, meta, count: 1, oldestAt: t };
    open.set(key, group);
    groups.push(group);
  }
  return groups;
}
