import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { View, Text, StyleSheet, ScrollView, Pressable } from 'react-native';
import { useFleet } from '../contexts/FleetContext';
import { useTheme } from '../contexts/ThemeContext';
import { analyticsApi, fleetApi, predictionApi, telemetryApi } from '../utils/api';
import { useLayout } from '../utils/layout';
import Screen from './Screen';
import RetryPanel from './ui/RetryPanel';
import { SkeletonCard } from './ui/SkeletonLoader';

function MiniBarChart({ points, valueKey, label, color }) {
  if (!points?.length) return <Text style={styles.emptyChart}>No data in window</Text>;
  const values = points.map((p) => p[valueKey] ?? 0);
  const max = Math.max(...values, 1);
  return (
    <View style={styles.chartBlock}>
      <Text style={styles.chartLabel}>{label}</Text>
      <View style={styles.barRow}>
        {points.slice(-12).map((p, i) => (
          <View key={`${p.bucketStartUtc}-${i}`} style={styles.barCol}>
            <View style={[styles.bar, { height: `${((p[valueKey] ?? 0) / max) * 100}%`, backgroundColor: color }]} />
          </View>
        ))}
      </View>
    </View>
  );
}

export default function AnalyticsPanel() {
  const { defaultFleetId, selectedVehicleId } = useFleet();
  const { colors } = useTheme();
  const { horizontalPadding, titleSize, cardPadding } = useLayout();
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [fleetAnalytics, setFleetAnalytics] = useState(null);
  const [operational, setOperational] = useState(null);
  const [drivers, setDrivers] = useState([]);
  const [aggregations, setAggregations] = useState([]);
  const [heatmap, setHeatmap] = useState([]);
  const [health, setHealth] = useState(null);
  const [maintenance, setMaintenance] = useState(null);

  const window = useMemo(() => {
    const to = new Date();
    const from = new Date(to.getTime() - 6 * 60 * 60 * 1000);
    return { from: from.toISOString(), to: to.toISOString() };
  }, []);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const vehicle = selectedVehicleId ?? 'Truck-001';
      const [fleet, ops, heat, driverStats, aggs, h, m] = await Promise.all([
        analyticsApi.fleet(defaultFleetId, window.from, window.to),
        analyticsApi.operational(defaultFleetId, window.from, window.to),
        fleetApi.heatmap(defaultFleetId, window.from, window.to, 0.01),
        analyticsApi.drivers(defaultFleetId, window.from, window.to),
        telemetryApi.aggregations(vehicle, window.from, window.to, 15),
        predictionApi.vehicleHealth(vehicle),
        predictionApi.maintenance(vehicle),
      ]);
      setFleetAnalytics(fleet);
      setOperational(ops);
      setHeatmap(heat ?? []);
      setDrivers(driverStats ?? []);
      setAggregations(aggs ?? []);
      setHealth(h);
      setMaintenance(m);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }, [defaultFleetId, selectedVehicleId, window.from, window.to]);

  useEffect(() => {
    load();
  }, [load]);

  return (
    <Screen>
      <ScrollView contentContainerStyle={[styles.content, { paddingHorizontal: horizontalPadding }]}>
        <View style={styles.headerRow}>
          <Text style={[styles.heading, { fontSize: titleSize, color: colors.text }]}>Analytics (6h)</Text>
          <Pressable onPress={load}>
            <Text style={{ color: colors.accent, fontWeight: '600' }}>Refresh</Text>
          </Pressable>
        </View>

        {error ? <RetryPanel message={error} onRetry={load} /> : null}

        {loading ? (
          <>
            <SkeletonCard />
            <SkeletonCard lines={4} />
          </>
        ) : (
          <>
            {fleetAnalytics && (
              <View style={[styles.card, { padding: cardPadding, backgroundColor: colors.card }]}>
                <Text style={[styles.cardTitle, { color: colors.text }]}>Fleet KPIs</Text>
                <Text style={[styles.kpi, { color: colors.textSecondary }]}>Distance: {fleetAnalytics.totalDistanceKm} km</Text>
                <Text style={[styles.kpi, { color: colors.textSecondary }]}>Avg speed: {fleetAnalytics.avgSpeedKmh} km/h</Text>
                <Text style={[styles.kpi, { color: colors.textSecondary }]}>Incidents: {fleetAnalytics.incidentCount}</Text>
              </View>
            )}

            {operational && (
              <View style={[styles.card, { padding: cardPadding, backgroundColor: colors.card }]}>
                <Text style={[styles.cardTitle, { color: colors.text }]}>Operations</Text>
                <Text style={[styles.kpi, { color: colors.textSecondary }]}>Active trips: {operational.activeTrips}</Text>
                <Text style={[styles.kpi, { color: colors.textSecondary }]}>Completed: {operational.completedTrips}</Text>
                <Text style={[styles.kpi, { color: colors.textSecondary }]}>Open incidents: {operational.openIncidents}</Text>
              </View>
            )}

            {health && maintenance && (
              <View style={[styles.card, { padding: cardPadding, backgroundColor: colors.card }]}>
                <Text style={[styles.cardTitle, { color: colors.text }]}>Predictions — {selectedVehicleId ?? 'Truck-001'}</Text>
                <Text style={[styles.kpi, { color: colors.textSecondary }]}>
                  Health: {Math.round(health.score * 100)}% — {health.summary}
                </Text>
                <Text style={[styles.kpi, { color: colors.textSecondary }]}>
                  Maintenance: {maintenance.daysUntilService} days — {maintenance.recommendation}
                </Text>
              </View>
            )}

            {drivers.length > 0 && (
              <View style={[styles.card, { padding: cardPadding, backgroundColor: colors.card }]}>
                <Text style={[styles.cardTitle, { color: colors.text }]}>Driver safety</Text>
                {drivers.slice(0, 5).map((d) => (
                  <Text key={d.driverId} style={[styles.kpi, { color: colors.textSecondary }]}>
                    {d.fullName}: score {d.safetyScore} · {d.tripCount} trips
                  </Text>
                ))}
              </View>
            )}

            <View style={[styles.card, { padding: cardPadding, backgroundColor: colors.card }]}>
              <Text style={[styles.cardTitle, { color: colors.text }]}>{selectedVehicleId ?? 'Vehicle'} — Speed trend</Text>
              <MiniBarChart points={aggregations} valueKey="avgSpeed" label="Avg speed per bucket" color={colors.accent} />
            </View>

            <View style={[styles.card, { padding: cardPadding, backgroundColor: colors.card }]}>
              <Text style={[styles.cardTitle, { color: colors.text }]}>{selectedVehicleId ?? 'Vehicle'} — Temperature trend</Text>
              <MiniBarChart points={aggregations} valueKey="avgTemperature" label="Avg cargo temp" color={colors.warning} />
            </View>

            <View style={[styles.card, { padding: cardPadding, backgroundColor: colors.card }]}>
              <Text style={[styles.cardTitle, { color: colors.text }]}>Speed heatmap cells</Text>
              {heatmap.length === 0 ? (
                <Text style={[styles.emptyChart, { color: colors.muted }]}>No heatmap data yet — simulate telemetry first</Text>
              ) : (
                heatmap.slice(0, 8).map((cell, i) => (
                  <Text key={i} style={[styles.kpi, { color: colors.textSecondary }]}>
                    ({cell.cellLatitude.toFixed(3)}, {cell.cellLongitude.toFixed(3)}) — {cell.avgSpeed.toFixed(0)} km/h · {cell.pointCount} pts
                  </Text>
                ))
              )}
            </View>
          </>
        )}
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  content: { paddingTop: 8, paddingBottom: 24 },
  headerRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 },
  heading: { fontWeight: '700' },
  card: { borderRadius: 14, marginBottom: 12 },
  cardTitle: { fontSize: 16, fontWeight: '700', marginBottom: 8 },
  kpi: { marginTop: 4 },
  chartBlock: { marginTop: 4 },
  chartLabel: { fontSize: 12, color: '#8E8E93', marginBottom: 6 },
  barRow: { flexDirection: 'row', alignItems: 'flex-end', height: 80, gap: 4 },
  barCol: { flex: 1, height: '100%', justifyContent: 'flex-end' },
  bar: { width: '100%', minHeight: 4, borderRadius: 3 },
  emptyChart: { fontStyle: 'italic' },
});
