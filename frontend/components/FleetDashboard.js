import React from 'react';
import { View, Text, StyleSheet, ScrollView, Pressable } from 'react-native';
import { useFleet } from '../contexts/FleetContext';
import { computeFleetSummary, getVehicleStatus, statusColor } from '../utils/fleetConfig';
import { useLayout } from '../utils/layout';
import Screen from './Screen';

function KpiCard({ label, value, accent, compact }) {
  return (
    <View style={[styles.kpiCard, compact && styles.kpiCardCompact]}>
      <Text style={styles.kpiLabel}>{label}</Text>
      <Text style={[styles.kpiValue, compact && styles.kpiValueCompact, accent ? { color: accent } : null]} numberOfLines={1}>
        {value}
      </Text>
    </View>
  );
}

export default function FleetDashboard() {
  const { vehicleIds, vehicles, connectionStatus, setSelectedVehicleId } = useFleet();
  const summary = computeFleetSummary(vehicles, vehicleIds);
  const { compact, horizontalPadding, titleSize, bodySize, cardPadding } = useLayout();

  return (
    <Screen>
      <ScrollView
        style={styles.container}
        contentContainerStyle={[styles.content, { paddingHorizontal: horizontalPadding, paddingBottom: 24 }]}
        showsVerticalScrollIndicator={false}
      >
        <Text style={[styles.heading, { fontSize: titleSize }]}>Fleet Overview</Text>
        <Text style={[styles.subheading, { fontSize: bodySize }]}>{connectionStatus}</Text>

        <View style={styles.kpiGrid}>
          <KpiCard compact={compact} label="ACTIVE" value={`${summary.activeCount}/${summary.configuredCount}`} />
          <KpiCard
            compact={compact}
            label="WARNINGS"
            value={String(summary.warningCount)}
            accent={summary.warningCount > 0 ? '#FF3B30' : undefined}
          />
          <KpiCard compact={compact} label="AVG FUEL" value={summary.averageFuel != null ? `${summary.averageFuel}%` : '—'} />
          <KpiCard compact={compact} label="AVG TEMP" value={summary.averageTemp != null ? `${summary.averageTemp}°C` : '—'} />
        </View>

        <Text style={styles.sectionTitle}>Vehicles</Text>
        {vehicleIds.map((vehicleId) => {
          const telemetry = vehicles[vehicleId];
          const status = getVehicleStatus(telemetry);

          return (
            <Pressable
              key={vehicleId}
              style={[styles.vehicleRow, { padding: cardPadding }]}
              onPress={() => setSelectedVehicleId(vehicleId)}
            >
              <View style={styles.vehicleHeader}>
                <Text style={styles.vehicleId} numberOfLines={1}>{vehicleId}</Text>
                <View style={[styles.statusBadge, { backgroundColor: statusColor(status) }]}>
                  <Text style={styles.statusBadgeText}>{status.toUpperCase()}</Text>
                </View>
              </View>
              {telemetry ? (
                <Text style={[styles.vehicleMeta, { fontSize: bodySize }]} numberOfLines={2}>
                  {telemetry.speed} km/h · Fuel {telemetry.fuelLevel}% · {telemetry.engineTemperature}°C
                </Text>
              ) : (
                <Text style={[styles.vehicleMeta, { fontSize: bodySize }]} numberOfLines={2}>
                  No live telemetry yet — run simulate for this vehicle.
                </Text>
              )}
            </Pressable>
          );
        })}
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1 },
  content: { paddingTop: 8 },
  heading: { fontWeight: '700', color: '#1C1C1E' },
  subheading: { color: '#8E8E93', marginBottom: 16, marginTop: 4 },
  kpiGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'space-between',
    marginBottom: 20,
  },
  kpiCard: {
    width: '48%',
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    padding: 16,
    marginBottom: 12,
    shadowColor: '#000',
    shadowOpacity: 0.05,
    shadowRadius: 8,
    elevation: 2,
  },
  kpiCardCompact: { padding: 12 },
  kpiLabel: { fontSize: 12, fontWeight: '600', color: '#8E8E93', marginBottom: 6 },
  kpiValue: { fontSize: 22, fontWeight: '700', color: '#1C1C1E' },
  kpiValueCompact: { fontSize: 20 },
  sectionTitle: { fontSize: 18, fontWeight: '700', color: '#1C1C1E', marginBottom: 10 },
  vehicleRow: {
    backgroundColor: '#FFFFFF',
    borderRadius: 14,
    marginBottom: 10,
  },
  vehicleHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: 6, gap: 8 },
  vehicleId: { fontSize: 16, fontWeight: '700', color: '#1C1C1E', flex: 1 },
  statusBadge: { borderRadius: 10, paddingHorizontal: 8, paddingVertical: 4 },
  statusBadgeText: { color: '#FFFFFF', fontSize: 11, fontWeight: '700' },
  vehicleMeta: { color: '#636366' },
});
