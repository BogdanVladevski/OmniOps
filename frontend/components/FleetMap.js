import React, { useRef, useState } from 'react';
import { ActivityIndicator, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import MapView, { Circle, Marker, Polygon, PROVIDER_DEFAULT } from 'react-native-maps';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useFleet } from '../contexts/FleetContext';
import { useTheme } from '../contexts/ThemeContext';
import { elevation, radius, spacing, typography } from '../theme/tokens';
import { getVehicleStatus, statusColor } from '../utils/fleetConfig';
import { useLayout } from '../utils/layout';
import { Pill, TempRangeBar, timeAgo } from './ui/kit';

function parsePolygon(json) {
  try {
    const points = JSON.parse(json ?? '[]');
    return points.filter((p) => p.length >= 2).map((p) => ({ latitude: p[0], longitude: p[1] }));
  } catch {
    return [];
  }
}

const STATUS = {
  ok: { tone: 'ok', label: 'OK' },
  warning: { tone: 'critical', label: 'Attention' },
  offline: { tone: 'neutral', label: 'Offline' },
};

function Metric({ icon, label, value }) {
  const { colors } = useTheme();
  return (
    <View style={styles.metric}>
      <Ionicons name={icon} size={16} color={colors.muted} />
      <Text style={[typography.overline, { color: colors.muted }]}>{label}</Text>
      <Text style={[typography.title, { color: colors.text }]} numberOfLines={1}>{value}</Text>
    </View>
  );
}

function VehiclePanel({ vehicleId, telemetry }) {
  const { colors } = useTheme();
  const s = telemetry?.shipment;
  const status = STATUS[getVehicleStatus(telemetry)];
  if (!telemetry) {
    return (
      <View style={styles.panelBody}>
        <Text style={[typography.title, { color: colors.text }]}>{vehicleId ?? 'Select a truck'}</Text>
        <ActivityIndicator size="small" color={colors.accent} style={{ marginTop: spacing.sm }} />
      </View>
    );
  }
  const battery = telemetry.batteryLevel;
  return (
    <View style={styles.panelBody}>
      <View style={styles.panelHead}>
        <View style={styles.flex}>
          <Text style={[typography.title, { color: colors.text }]}>{vehicleId}</Text>
          <Text style={[typography.caption, { color: colors.muted }]}>
            {s ? `${s.productName} · batch ${s.batchNumber}` : 'No active shipment'} · updated {timeAgo(telemetry.timestamp)}
          </Text>
        </View>
        <Pill label={status.label} tone={status.tone} />
      </View>
      {s ? <TempRangeBar value={telemetry.engineTemperature} min={s.minSafeTempCelsius} max={s.maxSafeTempCelsius} /> : null}
      <View style={styles.metrics}>
        <Metric icon="speedometer-outline" label="Speed" value={`${telemetry.speed} km/h`} />
        <Metric icon="compass-outline" label="Heading" value={telemetry.heading != null ? `${Math.round(telemetry.heading)}°` : '—'} />
        <Metric icon="thermometer-outline" label="Cargo" value={`${telemetry.engineTemperature}°C`} />
        <Metric
          icon="battery-half-outline"
          label="Sensor battery"
          value={battery != null ? `${Math.round(battery)}%` : '—'}
        />
      </View>
    </View>
  );
}

function VehicleList({ vehicleIds, vehicles, selectedVehicleId, onSelect, horizontal }) {
  const { colors } = useTheme();
  const items = vehicleIds.map((id) => {
    const t = vehicles[id];
    const selected = id === selectedVehicleId;
    return (
      <Pressable
        key={id}
        onPress={() => onSelect(id)}
        accessibilityRole="button"
        accessibilityState={{ selected }}
        style={({ hovered }) => [
          styles.vehicleItem,
          horizontal && styles.vehicleChip,
          { backgroundColor: selected ? colors.accentMuted : hovered ? colors.sunken : colors.card, borderColor: selected ? colors.accent : colors.border },
        ]}
      >
        <View style={[styles.dot, { backgroundColor: statusColor(getVehicleStatus(t)) }]} />
        <View style={horizontal ? null : styles.flex}>
          <Text style={[typography.bodyStrong, { color: colors.text }]}>{id}</Text>
          {!horizontal ? (
            <Text style={[typography.caption, { color: colors.muted }]} numberOfLines={1}>
              {t ? `${t.speed} km/h · ${t.engineTemperature}°C` : 'No signal'}
            </Text>
          ) : null}
        </View>
      </Pressable>
    );
  });
  if (horizontal) {
    return (
      <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.chipRow}>
        {items}
      </ScrollView>
    );
  }
  return <View style={styles.vehicleList}>{items}</View>;
}

export default function FleetMap() {
  const { vehicleIds, vehicles, geofences, selectedVehicleId, setSelectedVehicleId } = useFleet();
  const { colors, isDark } = useTheme();
  const mapRef = useRef(null);
  const insets = useSafeAreaInsets();
  const { isWide, horizontalPadding } = useLayout();
  const [mapType, setMapType] = useState('standard');
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

  const initialRegion = { latitude: 41.9965, longitude: 21.4314, latitudeDelta: 0.08, longitudeDelta: 0.08 };

  const map = (
    <MapView
      ref={mapRef}
      style={styles.map}
      provider={PROVIDER_DEFAULT}
      mapType={mapType}
      initialRegion={initialRegion}
      showsUserLocation
    >
      {geofences.map((g) => {
        if (g.shapeType === 'Radius' && g.centerLatitude && g.centerLongitude && g.radiusMeters) {
          return (
            <Circle
              key={g.id}
              center={{ latitude: g.centerLatitude, longitude: g.centerLongitude }}
              radius={g.radiusMeters}
              strokeColor="rgba(14,165,183,0.9)"
              fillColor="rgba(14,165,183,0.15)"
            />
          );
        }
        const coords = parsePolygon(g.polygonCoordinatesJson);
        if (coords.length >= 3) {
          return (
            <Polygon key={g.id} coordinates={coords} strokeColor="rgba(59,130,246,0.9)" fillColor="rgba(59,130,246,0.15)" />
          );
        }
        return null;
      })}

      {vehicleIds.map((vehicleId) => {
        const telemetry = vehicles[vehicleId];
        if (!telemetry?.latitude || !telemetry?.longitude) return null;
        const isSelected = vehicleId === selectedVehicleId;
        return (
          <Marker
            key={vehicleId}
            coordinate={{ latitude: telemetry.latitude, longitude: telemetry.longitude }}
            title={vehicleId}
            description={`${telemetry.speed} km/h · ${telemetry.engineTemperature}°C cargo`}
            pinColor={statusColor(getVehicleStatus(telemetry))}
            rotation={telemetry.heading ?? 0}
            onPress={() => focusVehicle(vehicleId)}
            opacity={isSelected ? 1 : 0.85}
          />
        );
      })}
    </MapView>
  );

  const mapTypes = (
    <View style={[styles.mapTypes, { backgroundColor: colors.card }, elevation(isDark)]}>
      {['standard', 'satellite', 'hybrid'].map((type) => (
        <Pressable
          key={type}
          onPress={() => setMapType(type)}
          style={[styles.mapType, mapType === type && { backgroundColor: colors.accentMuted }]}
        >
          <Text style={[typography.label, { color: mapType === type ? colors.accentStrong : colors.textSecondary }]}>
            {type[0].toUpperCase() + type.slice(1)}
          </Text>
        </Pressable>
      ))}
    </View>
  );

  if (isWide) {
    return (
      <View style={[styles.wide, { backgroundColor: colors.background }]}>
        <View style={styles.flex}>
          {map}
          <View style={[styles.mapTypesWrap, { top: spacing.lg, left: spacing.lg }]}>{mapTypes}</View>
        </View>
        <View style={[styles.sidePanel, { backgroundColor: colors.card, borderLeftColor: colors.border }]}>
          <Text style={[typography.headline, { color: colors.text }]}>Live map</Text>
          <Text style={[typography.caption, { color: colors.textSecondary, marginBottom: spacing.lg }]}>
            Where every truck is right now. Select one to see its cargo temperature.
          </Text>
          <VehicleList vehicleIds={vehicleIds} vehicles={vehicles} selectedVehicleId={selectedVehicleId} onSelect={focusVehicle} />
          <View style={[styles.divider, { backgroundColor: colors.border }]} />
          <VehiclePanel vehicleId={selectedVehicleId} telemetry={selectedTelemetry} />
        </View>
      </View>
    );
  }

  return (
    <View style={[styles.flex, { backgroundColor: colors.background }]}>
      {map}
      <View style={[styles.topOverlay, { top: insets.top + spacing.sm, paddingHorizontal: horizontalPadding }]}>
        {mapTypes}
        <VehicleList horizontal vehicleIds={vehicleIds} vehicles={vehicles} selectedVehicleId={selectedVehicleId} onSelect={focusVehicle} />
      </View>
      <View style={[styles.sheet, { backgroundColor: colors.card, left: horizontalPadding, right: horizontalPadding }, elevation(isDark, 2)]}>
        <VehiclePanel vehicleId={selectedVehicleId} telemetry={selectedTelemetry} />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  map: { ...StyleSheet.absoluteFillObject },
  wide: { flex: 1, flexDirection: 'row' },
  sidePanel: { width: 380, borderLeftWidth: StyleSheet.hairlineWidth, padding: spacing.xl },
  divider: { height: StyleSheet.hairlineWidth, marginVertical: spacing.lg },
  mapTypesWrap: { position: 'absolute' },
  mapTypes: { flexDirection: 'row', borderRadius: radius.pill, padding: 4, alignSelf: 'flex-start' },
  mapType: { paddingHorizontal: 12, paddingVertical: 6, borderRadius: radius.pill },
  topOverlay: { position: 'absolute', left: 0, right: 0, gap: spacing.sm },
  chipRow: { gap: spacing.sm },
  vehicleList: { gap: spacing.sm },
  vehicleItem: {
    flexDirection: 'row', alignItems: 'center', gap: spacing.md, borderWidth: 1,
    borderRadius: radius.md, paddingHorizontal: spacing.md, paddingVertical: 10,
  },
  vehicleChip: { borderRadius: radius.pill, paddingVertical: 7 },
  dot: { width: 10, height: 10, borderRadius: 5 },
  sheet: { position: 'absolute', bottom: spacing.md, borderRadius: radius.xl, padding: spacing.lg },
  panelBody: { gap: spacing.md },
  panelHead: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.md },
  metrics: { flexDirection: 'row', flexWrap: 'wrap', rowGap: spacing.md },
  metric: { width: '50%', gap: 2 },
});
