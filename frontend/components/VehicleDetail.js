import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet, ScrollView, Modal, Pressable, ActivityIndicator } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useFleet } from '../contexts/FleetContext';
import { useTheme } from '../contexts/ThemeContext';
import { predictionApi, telemetryApi } from '../utils/api';
import { getVehicleStatus, statusColor } from '../utils/fleetConfig';
import { useLayout } from '../utils/layout';
import EmptyState from './ui/EmptyState';
import RetryPanel from './ui/RetryPanel';
import { SkeletonCard } from './ui/SkeletonLoader';

export default function VehicleDetail({ visible, vehicleId, onClose }) {
  const { vehicles } = useFleet();
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const { bodySize, cardPadding } = useLayout();
  const telemetry = vehicleId ? vehicles[vehicleId] : null;
  const [health, setHealth] = useState(null);
  const [maintenance, setMaintenance] = useState(null);
  const [aggregations, setAggregations] = useState([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);

  const load = async () => {
    if (!vehicleId) return;
    setLoading(true);
    setError(null);
    try {
      const to = new Date();
      const from = new Date(to.getTime() - 3 * 60 * 60 * 1000);
      const [h, m, aggs] = await Promise.all([
        predictionApi.vehicleHealth(vehicleId),
        predictionApi.maintenance(vehicleId),
        telemetryApi.aggregations(vehicleId, from.toISOString(), to.toISOString(), 15),
      ]);
      setHealth(h);
      setMaintenance(m);
      setAggregations(aggs ?? []);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    if (visible && vehicleId) load();
  }, [visible, vehicleId]);

  if (!visible) return null;

  const status = getVehicleStatus(telemetry);

  return (
    <Modal visible animationType="slide" presentationStyle="pageSheet" onRequestClose={onClose}>
      <View style={[styles.container, { backgroundColor: colors.background, paddingTop: insets.top }]}>
        <View style={[styles.header, { borderBottomColor: colors.border }]}>
          <Text style={[styles.title, { color: colors.text }]}>{vehicleId}</Text>
          <Pressable onPress={onClose}>
            <Text style={[styles.close, { color: colors.accent }]}>Close</Text>
          </Pressable>
        </View>

        <ScrollView contentContainerStyle={styles.content}>
          {error ? <RetryPanel message={error} onRetry={load} /> : null}

          <View style={[styles.card, { backgroundColor: colors.card, padding: cardPadding }]}>
            <View style={styles.statusRow}>
              <Text style={[styles.section, { color: colors.text }]}>Live status</Text>
              <View style={[styles.badge, { backgroundColor: statusColor(status) }]}>
                <Text style={styles.badgeText}>{status.toUpperCase()}</Text>
              </View>
            </View>
            {telemetry ? (
              <Text style={[styles.meta, { color: colors.textSecondary, fontSize: bodySize }]}>
                {telemetry.speed} km/h · Fuel {telemetry.fuelLevel}% · Cargo {telemetry.engineTemperature}°C
              </Text>
            ) : (
              <EmptyState
                title="No live telemetry"
                body="Run simulate for this vehicle to populate real-time data."
                icon="🚛"
              />
            )}
          </View>

          {loading ? (
            <>
              <SkeletonCard lines={2} />
              <SkeletonCard lines={3} />
            </>
          ) : (
            <>
              {health && (
                <View style={[styles.card, { backgroundColor: colors.card, padding: cardPadding }]}>
                  <Text style={[styles.section, { color: colors.text }]}>Health score</Text>
                  <Text style={[styles.score, { color: colors.accent }]}>{Math.round(health.score * 100)}%</Text>
                  <Text style={[styles.meta, { color: colors.textSecondary }]}>{health.summary}</Text>
                </View>
              )}

              {maintenance && (
                <View style={[styles.card, { backgroundColor: colors.card, padding: cardPadding }]}>
                  <Text style={[styles.section, { color: colors.text }]}>Maintenance</Text>
                  <Text style={[styles.meta, { color: colors.textSecondary }]}>
                    Service in ~{maintenance.daysUntilService} days
                  </Text>
                  <Text style={[styles.meta, { color: colors.textSecondary }]}>{maintenance.recommendation}</Text>
                </View>
              )}

              <View style={[styles.card, { backgroundColor: colors.card, padding: cardPadding }]}>
                <Text style={[styles.section, { color: colors.text }]}>Recent trend (3h)</Text>
                {aggregations.length === 0 ? (
                  <Text style={{ color: colors.muted }}>No historical buckets yet</Text>
                ) : (
                  aggregations.slice(-5).map((bucket) => (
                    <Text key={bucket.bucketStartUtc} style={{ color: colors.textSecondary, marginTop: 4 }}>
                      {new Date(bucket.bucketStartUtc).toLocaleTimeString()} — avg {bucket.avgSpeed?.toFixed?.(0) ?? '—'} km/h
                    </Text>
                  ))
                )}
              </View>
            </>
          )}

          {loading && <ActivityIndicator color={colors.accent} style={{ marginTop: 8 }} />}
        </ScrollView>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  header: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingHorizontal: 20,
    paddingBottom: 12,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  title: { fontSize: 22, fontWeight: '700' },
  close: { fontSize: 16, fontWeight: '600' },
  content: { padding: 16, paddingBottom: 32 },
  card: { borderRadius: 14, marginBottom: 12 },
  section: { fontSize: 16, fontWeight: '700', marginBottom: 6 },
  statusRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  badge: { borderRadius: 8, paddingHorizontal: 8, paddingVertical: 4 },
  badgeText: { color: '#fff', fontSize: 11, fontWeight: '700' },
  score: { fontSize: 32, fontWeight: '800', marginVertical: 4 },
  meta: { lineHeight: 20 },
});
