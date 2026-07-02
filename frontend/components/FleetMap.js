import React, { useRef } from 'react';
import { Text, View, StyleSheet, ActivityIndicator, Pressable, ScrollView } from 'react-native';
import MapView, { Marker, PROVIDER_DEFAULT } from 'react-native-maps';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useFleet } from '../contexts/FleetContext';
import { getVehicleStatus, statusColor } from '../utils/fleetConfig';
import { useLayout } from '../utils/layout';

export default function FleetMap() {
  const { vehicleIds, vehicles, selectedVehicleId, setSelectedVehicleId, connectionStatus } = useFleet();
  const mapRef = useRef(null);
  const insets = useSafeAreaInsets();
  const { compact, horizontalPadding, titleSize } = useLayout();
  const selectedTelemetry = selectedVehicleId ? vehicles[selectedVehicleId] : null;

  const focusVehicle = (vehicleId) => {
    setSelectedVehicleId(vehicleId);
    const telemetry = vehicles[vehicleId];
    if (mapRef.current && telemetry?.latitude && telemetry?.longitude) {
      mapRef.current.animateToRegion({
        latitude: telemetry.latitude,
        longitude: telemetry.longitude,
        latitudeDelta: 0.02,
        longitudeDelta: 0.02,
      }, 800);
    }
  };

  const initialRegion = {
    latitude: 41.9965,
    longitude: 21.4314,
    latitudeDelta: 0.08,
    longitudeDelta: 0.08,
  };

  return (
    <View style={styles.container}>
      <MapView
        ref={mapRef}
        style={styles.map}
        provider={PROVIDER_DEFAULT}
        initialRegion={initialRegion}
        showsUserLocation
      >
        {vehicleIds.map((vehicleId) => {
          const telemetry = vehicles[vehicleId];
          if (!telemetry?.latitude || !telemetry?.longitude) return null;

          const status = getVehicleStatus(telemetry);
          const isSelected = vehicleId === selectedVehicleId;

          return (
            <Marker
              key={vehicleId}
              coordinate={{ latitude: telemetry.latitude, longitude: telemetry.longitude }}
              title={vehicleId}
              description={`${telemetry.speed} km/h | Fuel ${telemetry.fuelLevel}%`}
              pinColor={statusColor(status)}
              onPress={() => focusVehicle(vehicleId)}
              opacity={isSelected ? 1 : 0.85}
            />
          );
        })}
      </MapView>

      <ScrollView
        horizontal
        showsHorizontalScrollIndicator={false}
        style={[styles.chipRow, { top: insets.top + 8 }]}
        contentContainerStyle={[styles.chipContent, { paddingHorizontal: horizontalPadding }]}
      >
        {vehicleIds.map((vehicleId) => {
          const status = getVehicleStatus(vehicles[vehicleId]);
          const isSelected = vehicleId === selectedVehicleId;
          return (
            <Pressable
              key={vehicleId}
              onPress={() => focusVehicle(vehicleId)}
              style={[styles.chip, isSelected && styles.chipSelected]}
            >
              <View style={[styles.chipDot, { backgroundColor: statusColor(status) }]} />
              <Text style={[styles.chipText, isSelected && styles.chipTextSelected]}>{vehicleId}</Text>
            </Pressable>
          );
        })}
      </ScrollView>

      <View
        style={[
          styles.overlayPanel,
          {
            left: horizontalPadding,
            right: horizontalPadding,
            bottom: 12,
            padding: compact ? 14 : 18,
          },
        ]}
      >
        <Text style={[styles.title, { fontSize: titleSize }]} numberOfLines={1}>
          Target: {selectedVehicleId ?? '—'}
        </Text>
        <Text style={styles.statusLabel} numberOfLines={1}>{connectionStatus}</Text>

        {selectedTelemetry ? (
          <View style={styles.statsGrid}>
            <View style={styles.statBox}>
              <Text style={styles.statLabel}>SPEED</Text>
              <Text style={styles.statValue} adjustsFontSizeToFit numberOfLines={1}>
                {selectedTelemetry.speed} km/h
              </Text>
            </View>
            <View style={styles.statBox}>
              <Text style={styles.statLabel}>FUEL</Text>
              <Text style={styles.statValue} adjustsFontSizeToFit numberOfLines={1}>
                {selectedTelemetry.fuelLevel}%
              </Text>
            </View>
            <View style={styles.statBox}>
              <Text style={styles.statLabel}>TEMP</Text>
              <Text style={styles.statValue} adjustsFontSizeToFit numberOfLines={1}>
                {selectedTelemetry.engineTemperature}°C
              </Text>
            </View>
          </View>
        ) : (
          <ActivityIndicator size="small" color="#007AFF" style={{ marginTop: 10 }} />
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: '#F2F2F7' },
  map: { ...StyleSheet.absoluteFillObject },
  chipRow: {
    position: 'absolute',
    left: 0,
    right: 0,
    maxHeight: 48,
  },
  chipContent: { alignItems: 'center' },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    backgroundColor: 'rgba(255,255,255,0.95)',
    borderRadius: 16,
    paddingHorizontal: 12,
    paddingVertical: 8,
    marginRight: 8,
  },
  chipSelected: { borderWidth: 2, borderColor: '#007AFF' },
  chipDot: { width: 8, height: 8, borderRadius: 4, marginRight: 6 },
  chipText: { fontSize: 13, fontWeight: '600', color: '#3A3A3C' },
  chipTextSelected: { color: '#007AFF' },
  overlayPanel: {
    position: 'absolute',
    backgroundColor: 'rgba(255, 255, 255, 0.95)',
    borderRadius: 20,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.15,
    shadowRadius: 10,
    elevation: 8,
  },
  title: { fontWeight: '700', color: '#1C1C1E' },
  statusLabel: { fontSize: 13, color: '#8E8E93', marginBottom: 10, marginTop: 4 },
  statsGrid: { flexDirection: 'row', justifyContent: 'space-between' },
  statBox: { alignItems: 'center', flex: 1, paddingHorizontal: 4 },
  statLabel: { fontSize: 11, fontWeight: '600', color: '#8E8E93', marginBottom: 2 },
  statValue: { fontSize: 15, fontWeight: '700', color: '#1C1C1E' },
});
