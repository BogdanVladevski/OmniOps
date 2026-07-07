import React, { useEffect, useState } from 'react';
import { View, Text, StyleSheet, ScrollView, Pressable } from 'react-native';
import { useFleet } from '../contexts/FleetContext';
import { useTheme } from '../contexts/ThemeContext';
import { analyticsApi } from '../utils/api';
import { computeFleetSummary, getVehicleStatus, statusColor } from '../utils/fleetConfig';
import { useLayout } from '../utils/layout';
import Screen from './Screen';
import VehicleDetail from './VehicleDetail';
import EmptyState from './ui/EmptyState';
import RetryPanel from './ui/RetryPanel';
import { SkeletonKpiGrid } from './ui/SkeletonLoader';

function KpiCard({ label, value, accent, compact, colors }) {
  return (
    <View style={[styles.kpiCard, compact && styles.kpiCardCompact, { backgroundColor: colors.card }]}>
      <Text style={[styles.kpiLabel, { color: colors.muted }]}>{label}</Text>
      <Text
        style={[styles.kpiValue, compact && styles.kpiValueCompact, { color: accent ?? colors.text }]}
        numberOfLines={1}
      >
        {value}
      </Text>
    </View>
  );
}

function ShipmentBadge({ shipment, cargoTemp, colors }) {
  if (!shipment) return null;
  const inExcursion =
    cargoTemp > shipment.maxSafeTempCelsius || cargoTemp < shipment.minSafeTempCelsius;
  return (
    <View style={[styles.shipmentBadge, { backgroundColor: colors.border }, inExcursion && styles.shipmentBadgeExcursion]}>
      <Text style={[styles.shipmentBadgeText, { color: colors.text }]} numberOfLines={1}>
        {shipment.productName} · {shipment.batchNumber}
      </Text>
      {inExcursion && (
        <Text style={styles.shipmentExcursionText} numberOfLines={1}>
          Temp excursion — ${shipment.valueAtRiskUsd?.toLocaleString()} at risk
        </Text>
      )}
    </View>
  );
}

export default function FleetDashboard() {
  const { vehicleIds, vehicles, connectionStatus, fleetMeta, syncStatus, setSelectedVehicleId, selectedVehicleId, defaultFleetId } = useFleet();
  const { colors } = useTheme();
  const summary = computeFleetSummary(vehicles, vehicleIds);
  const { compact, horizontalPadding, titleSize, bodySize, cardPadding } = useLayout();
  const [detailVehicleId, setDetailVehicleId] = useState(null);
  const [operational, setOperational] = useState(null);
  const [opsError, setOpsError] = useState(null);
  const [opsLoading, setOpsLoading] = useState(true);

  const loadOperational = async () => {
    setOpsLoading(true);
    setOpsError(null);
    try {
      const to = new Date();
      const from = new Date(to.getTime() - 24 * 60 * 60 * 1000);
      const ops = await analyticsApi.operational(defaultFleetId, from.toISOString(), to.toISOString());
      setOperational(ops);
    } catch (e) {
      setOpsError(e.message);
    } finally {
      setOpsLoading(false);
    }
  };

  useEffect(() => {
    loadOperational();
  }, [defaultFleetId]);

  const openVehicle = (vehicleId) => {
    setSelectedVehicleId(vehicleId);
    setDetailVehicleId(vehicleId);
  };

  return (
    <Screen>
      <ScrollView
        style={styles.container}
        contentContainerStyle={[styles.content, { paddingHorizontal: horizontalPadding, paddingBottom: 24 }]}
        showsVerticalScrollIndicator={false}
      >
        <Text style={[styles.heading, { fontSize: titleSize, color: colors.text }]}>Executive Dashboard</Text>
        <Text style={[styles.subheading, { fontSize: bodySize, color: colors.muted }]}>
          {connectionStatus} · Sync: {syncStatus}
        </Text>

        {opsError ? <RetryPanel message={opsError} onRetry={loadOperational} /> : null}

        {opsLoading ? (
          <SkeletonKpiGrid />
        ) : (
          <View style={styles.kpiGrid}>
            <KpiCard compact={compact} colors={colors} label="ACTIVE" value={`${summary.activeCount}/${summary.configuredCount}`} />
            <KpiCard
              compact={compact}
              colors={colors}
              label="WARNINGS"
              value={String(summary.warningCount)}
              accent={summary.warningCount > 0 ? colors.danger : undefined}
            />
            <KpiCard
              compact={compact}
              colors={colors}
              label="OPEN INCIDENTS"
              value={String(operational?.openIncidents ?? fleetMeta?.openIncidentCount ?? '—')}
            />
            <KpiCard
              compact={compact}
              colors={colors}
              label="ACTIVE TRIPS"
              value={String(operational?.activeTrips ?? fleetMeta?.activeTripCount ?? '—')}
            />
            <KpiCard compact={compact} colors={colors} label="AVG FUEL" value={summary.averageFuel != null ? `${summary.averageFuel}%` : '—'} />
            <KpiCard compact={compact} colors={colors} label="AVG TEMP" value={summary.averageTemp != null ? `${summary.averageTemp}°C` : '—'} />
          </View>
        )}

        <Text style={[styles.sectionTitle, { color: colors.text }]}>Shipments in Transit</Text>
        {vehicleIds.length === 0 ? (
          <EmptyState
            title="No vehicles configured"
            body="Set EXPO_PUBLIC_FLEET_VEHICLES in frontend/.env to match your fleet."
          />
        ) : (
          vehicleIds.map((vehicleId) => {
            const telemetry = vehicles[vehicleId];
            const status = getVehicleStatus(telemetry);

            return (
              <Pressable
                key={vehicleId}
                style={[styles.vehicleRow, { padding: cardPadding, backgroundColor: colors.card }]}
                onPress={() => openVehicle(vehicleId)}
              >
                <View style={styles.vehicleHeader}>
                  <Text style={[styles.vehicleId, { color: colors.text }]} numberOfLines={1}>{vehicleId}</Text>
                  <View style={[styles.statusBadge, { backgroundColor: statusColor(status) }]}>
                    <Text style={styles.statusBadgeText}>{status.toUpperCase()}</Text>
                  </View>
                </View>

                {telemetry?.shipment && (
                  <ShipmentBadge shipment={telemetry.shipment} cargoTemp={telemetry.engineTemperature} colors={colors} />
                )}

                {telemetry ? (
                  <Text style={[styles.vehicleMeta, { fontSize: bodySize, color: colors.textSecondary }]} numberOfLines={2}>
                    {telemetry.speed} km/h · Fuel {telemetry.fuelLevel}% · Cargo {telemetry.engineTemperature}°C
                  </Text>
                ) : (
                  <Text style={[styles.vehicleMeta, { fontSize: bodySize, color: colors.muted }]} numberOfLines={2}>
                    No live telemetry — tap for health & maintenance details.
                  </Text>
                )}
              </Pressable>
            );
          })
        )}
      </ScrollView>

      <VehicleDetail
        visible={!!detailVehicleId}
        vehicleId={detailVehicleId ?? selectedVehicleId}
        onClose={() => setDetailVehicleId(null)}
      />
    </Screen>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  content: { paddingTop: 8 },
  heading: { fontWeight: '700' },
  subheading: { marginBottom: 16, marginTop: 4 },
  kpiGrid: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'space-between', marginBottom: 20 },
  kpiCard: { width: '48%', borderRadius: 16, padding: 16, marginBottom: 12 },
  kpiCardCompact: { padding: 12 },
  kpiLabel: { fontSize: 12, fontWeight: '600', marginBottom: 6 },
  kpiValue: { fontSize: 22, fontWeight: '700' },
  kpiValueCompact: { fontSize: 20 },
  sectionTitle: { fontSize: 18, fontWeight: '700', marginBottom: 10 },
  vehicleRow: { borderRadius: 14, marginBottom: 10 },
  vehicleHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6, gap: 8 },
  vehicleId: { fontSize: 16, fontWeight: '700', flex: 1 },
  statusBadge: { borderRadius: 10, paddingHorizontal: 8, paddingVertical: 4 },
  statusBadgeText: { color: '#FFFFFF', fontSize: 11, fontWeight: '700' },
  shipmentBadge: { borderRadius: 8, paddingHorizontal: 10, paddingVertical: 6, marginBottom: 6 },
  shipmentBadgeExcursion: { backgroundColor: '#FFF3CD', borderLeftWidth: 3, borderLeftColor: '#FF9500' },
  shipmentBadgeText: { fontSize: 13, fontWeight: '600' },
  shipmentExcursionText: { fontSize: 12, color: '#C05000', marginTop: 2 },
  vehicleMeta: {},
});
