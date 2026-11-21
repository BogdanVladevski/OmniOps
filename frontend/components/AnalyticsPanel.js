import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useFleet } from '../contexts/FleetContext';
import { useTheme } from '../contexts/ThemeContext';
import { radius, spacing, typography } from '../theme/tokens';
import { analyticsApi, fleetApi, predictionApi, telemetryApi } from '../utils/api';
import { useLayout } from '../utils/layout';
import RetryPanel from './ui/RetryPanel';
import { SkeletonCard } from './ui/SkeletonLoader';
import { BarChart, Button, Card, Grid, Page, Pill, SectionTitle, Segmented, StatTile } from './ui/kit';

const WINDOW_HOURS = 6;

/** The health API returns 0–100 (older builds returned 0–1); normalise to a whole percentage. */
function healthPercent(score) {
  if (typeof score !== 'number') return null;
  return Math.round(score <= 1 ? score * 100 : score);
}

function healthTone(pct) {
  if (pct == null) return 'neutral';
  if (pct >= 80) return 'ok';
  if (pct >= 50) return 'warning';
  return 'critical';
}

export default function AnalyticsPanel() {
  const { defaultFleetId, selectedVehicleId, setSelectedVehicleId, vehicleIds } = useFleet();
  const { colors } = useTheme();
  const { isTablet, isWide } = useLayout();
  const vehicle = selectedVehicleId ?? vehicleIds[0] ?? 'Truck-001';
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [data, setData] = useState({});

  const range = useMemo(() => {
    const to = new Date();
    const from = new Date(to.getTime() - WINDOW_HOURS * 60 * 60 * 1000);
    return { from: from.toISOString(), to: to.toISOString() };
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const [fleet, ops, heat, drivers, aggs, health, maintenance] = await Promise.all([
        analyticsApi.fleet(defaultFleetId, range.from, range.to),
        analyticsApi.operational(defaultFleetId, range.from, range.to),
        fleetApi.heatmap(defaultFleetId, range.from, range.to, 0.01),
        analyticsApi.drivers(defaultFleetId, range.from, range.to),
        telemetryApi.aggregations(vehicle, range.from, range.to, 5),
        predictionApi.vehicleHealth(vehicle),
        predictionApi.maintenance(vehicle),
      ]);
      setData({ fleet, ops, heat: heat ?? [], drivers: drivers ?? [], aggs: aggs ?? [], health, maintenance });
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }, [defaultFleetId, vehicle, range.from, range.to]);

  useEffect(() => {
    load();
  }, [load]);

  const { fleet, ops, heat = [], drivers = [], aggs = [], health, maintenance } = data;
  const pct = healthPercent(health?.score);
  const busiest = [...heat].sort((a, b) => b.pointCount - a.pointCount).slice(0, 5);

  return (
    <Page
      title="Analytics"
      subtitle={`Last ${WINDOW_HOURS} hours`}
      actions={<Button label="Refresh" icon="refresh" variant="secondary" onPress={load} />}
    >
      {error ? <RetryPanel message={error} onRetry={load} /> : null}

      {loading && !fleet ? (
        <>
          <SkeletonCard />
          <SkeletonCard lines={4} />
        </>
      ) : (
        <>
          <Grid columns={isTablet ? 4 : 2}>
            <StatTile label="Distance driven" value={fleet ? `${Math.round(fleet.totalDistanceKm)} km` : '—'} icon="navigate-outline" tone="accent" />
            <StatTile label="Average speed" value={fleet ? `${Math.round(fleet.avgSpeedKmh)} km/h` : '—'} icon="speedometer-outline" />
            <StatTile label="Incidents" value={fleet ? String(fleet.incidentCount) : '—'} hint="Raised in this window" icon="alert-circle-outline" tone={fleet?.incidentCount ? 'warning' : 'ok'} />
            <StatTile label="Open incidents" value={ops ? String(ops.openIncidents) : '—'} hint={ops ? `${ops.activeTrips} active trips` : undefined} icon="folder-open-outline" tone={ops?.openIncidents ? 'warning' : 'ok'} />
          </Grid>

          <SectionTitle title="Vehicle detail" subtitle="Pick a truck to see its health and trends" />
          <Segmented options={vehicleIds.map((id) => ({ key: id, label: id }))} value={vehicle} onChange={setSelectedVehicleId} />
          <View style={{ height: spacing.md }} />

          <Grid columns={isWide ? 3 : 1}>
            <Card style={styles.block}>
              <Text style={[typography.overline, { color: colors.muted }]}>Predicted health</Text>
              <View style={styles.healthRow}>
                <Text style={[typography.display, { color: colors.text }]}>{pct != null ? `${pct}%` : '—'}</Text>
                <Pill label={pct == null ? 'Unknown' : pct >= 80 ? 'Good' : pct >= 50 ? 'Watch' : 'At risk'} tone={healthTone(pct)} />
              </View>
              <View style={[styles.meter, { backgroundColor: colors.sunken }]}>
                <View style={[styles.meterFill, { width: `${pct ?? 0}%`, backgroundColor: colors[healthTone(pct)] ?? colors.accent }]} />
              </View>
              <Text style={[typography.caption, { color: colors.textSecondary }]}>{health?.summary}</Text>
              {maintenance ? (
                <Text style={[typography.caption, { color: colors.textSecondary }]}>
                  Service: {maintenance.daysUntilService <= 0 ? 'due now' : `in ${maintenance.daysUntilService} days`} — {maintenance.recommendation}
                </Text>
              ) : null}
            </Card>
            <Card style={styles.block}>
              <Text style={[typography.overline, { color: colors.muted }]}>Cargo temperature (°C)</Text>
              <BarChart values={aggs.map((a) => a.avgTemperature)} />
            </Card>
            <Card style={styles.block}>
              <Text style={[typography.overline, { color: colors.muted }]}>Speed (km/h)</Text>
              <BarChart values={aggs.map((a) => a.avgSpeed)} />
            </Card>
          </Grid>

          <Grid columns={isWide ? 2 : 1}>
            <View>
              <SectionTitle title="Driver safety" subtitle="Higher score = safer driving" />
              <Card padded={false}>
                {drivers.length === 0 ? (
                  <Text style={[typography.caption, styles.emptyLine, { color: colors.muted }]}>No driver activity in this window.</Text>
                ) : (
                  drivers.slice(0, 5).map((d, i) => (
                    <View key={d.driverId} style={[styles.line, i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }]}>
                      <Text style={[typography.bodyStrong, styles.flex, { color: colors.text }]}>{d.fullName}</Text>
                      <Text style={[typography.caption, { color: colors.textSecondary }]}>{d.tripCount} trips</Text>
                      <Pill label={`${d.safetyScore}`} tone={d.safetyScore >= 80 ? 'ok' : d.safetyScore >= 50 ? 'warning' : 'critical'} />
                    </View>
                  ))
                )}
              </Card>
            </View>
            <View>
              <SectionTitle title="Busiest areas" subtitle="Where trucks spent the most time" />
              <Card padded={false}>
                {busiest.length === 0 ? (
                  <Text style={[typography.caption, styles.emptyLine, { color: colors.muted }]}>No location data in this window yet.</Text>
                ) : (
                  busiest.map((c, i) => (
                    <View key={i} style={[styles.line, i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }]}>
                      <Text style={[typography.body, styles.flex, { color: colors.text }]}>
                        {c.cellLatitude.toFixed(3)}, {c.cellLongitude.toFixed(3)}
                      </Text>
                      <Text style={[typography.caption, { color: colors.textSecondary }]}>{Math.round(c.avgSpeed)} km/h avg</Text>
                      <Text style={[typography.caption, { color: colors.muted }]}>{c.pointCount} readings</Text>
                    </View>
                  ))
                )}
              </Card>
            </View>
          </Grid>
        </>
      )}
    </Page>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  block: { gap: spacing.sm, minHeight: 190 },
  healthRow: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between' },
  meter: { height: 8, borderRadius: radius.pill, overflow: 'hidden' },
  meterFill: { height: 8, borderRadius: radius.pill },
  line: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  emptyLine: { padding: spacing.lg },
});
