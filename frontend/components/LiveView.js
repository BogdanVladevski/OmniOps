// Home screen: the live map with every shipment, and a panel that tells each shipment's temperature story.
// Laptop: floating panel on the left. Phone: bottom sheet that expands on tap.
import React, { useEffect, useMemo, useRef, useState } from 'react';
import { Animated, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import MapView, { Circle, Marker, Polygon, PROVIDER_DEFAULT } from 'react-native-maps';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useFleet } from '../contexts/FleetContext';
import { useTheme } from '../contexts/ThemeContext';
import { elevation, radius, spacing, typography } from '../theme/tokens';
import { alertSummary, groupAlerts } from '../utils/alerts';
import { telemetryApi } from '../utils/api';
import { getVehicleStatus, resolveShipmentIdForVehicle } from '../utils/fleetConfig';
import { useLayout } from '../utils/layout';
import IncidentReplay from './IncidentReplay';
import { BarChart, Button, Divider, TempRangeBar, timeAgo } from './ui/kit';

const INITIAL_REGION = { latitude: 41.9965, longitude: 21.4314, latitudeDelta: 0.08, longitudeDelta: 0.08 };
const money = (n) => `$${Math.round(n ?? 0).toLocaleString()}`;

function parsePolygon(json) {
  try {
    return JSON.parse(json ?? '[]').filter((p) => p.length >= 2).map((p) => ({ latitude: p[0], longitude: p[1] }));
  } catch {
    return [];
  }
}

/** 'out' | 'attention' | 'ok' | 'offline' */
function shipmentStatus(t) {
  if (!t) return 'offline';
  const s = t.shipment;
  if (s && (t.engineTemperature > s.maxSafeTempCelsius || t.engineTemperature < s.minSafeTempCelsius)) return 'out';
  return getVehicleStatus(t) === 'warning' ? 'attention' : 'ok';
}

function statusText(status, t) {
  if (status === 'out') return 'Out of safe range';
  if (status === 'attention') return 'Needs a look';
  if (status === 'offline') return 'No signal';
  return t ? `Updated ${timeAgo(t.timestamp)}` : '';
}

function ShipmentRow({ id, t, selected, onPress }) {
  const { colors } = useTheme();
  const status = shipmentStatus(t);
  const s = t?.shipment;
  const red = status === 'out';
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={`${s?.productName ?? id}, ${t ? `${t.engineTemperature} degrees` : 'no signal'}`}
      style={({ hovered, pressed }) => [styles.row, (selected || hovered || pressed) && { backgroundColor: colors.sunken }]}
    >
      <View style={styles.flex}>
        <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>{s?.productName ?? id}</Text>
        <Text style={[typography.caption, { color: colors.muted }]} numberOfLines={1}>
          {id}{s ? ` · safe ${s.minSafeTempCelsius}–${s.maxSafeTempCelsius} °C` : ''}
        </Text>
      </View>
      <View style={styles.rowRight}>
        <Text style={[typography.number, { color: red ? colors.critical : colors.text }]}>
          {t ? `${t.engineTemperature}°` : '—'}
        </Text>
        <Text style={[typography.caption, { color: red ? colors.critical : status === 'attention' ? colors.warning : colors.muted }]} numberOfLines={1}>
          {statusText(status, t)}
        </Text>
      </View>
    </Pressable>
  );
}

function ShipmentList({ ids, vehicles, selectedId, onSelect }) {
  const { colors } = useTheme();
  const summary = useMemo(() => {
    const live = ids.map((id) => vehicles[id]).filter(Boolean);
    const out = live.filter((t) => shipmentStatus(t) === 'out');
    return {
      live: live.length,
      out: out.length,
      total: live.reduce((n, t) => n + (t.shipment?.valueAtRiskUsd ?? 0), 0),
      atRisk: out.reduce((n, t) => n + (t.shipment?.valueAtRiskUsd ?? 0), 0),
    };
  }, [ids, vehicles]);

  // Problems first, then by name.
  const sorted = [...ids].sort((a, b) => {
    const rank = { out: 0, attention: 1, ok: 2, offline: 3 };
    return rank[shipmentStatus(vehicles[a])] - rank[shipmentStatus(vehicles[b])] || a.localeCompare(b);
  });

  return (
    <View>
      <View style={styles.panelHeader}>
        <Text style={[typography.title, { color: colors.text }]}>Shipments in transit</Text>
        <Text style={[typography.body, { color: colors.textSecondary }]}>
          {summary.live} on the road
          {summary.out > 0 ? <Text style={{ color: colors.critical }}>{` · ${summary.out} out of safe range`}</Text> : ' · all within safe range'}
        </Text>
        <Text style={[typography.caption, { color: colors.muted }]}>
          {money(summary.total)} of medicine{summary.atRisk > 0 ? ` · ${money(summary.atRisk)} at risk` : ''}
        </Text>
      </View>
      <Divider />
      {sorted.map((id, i) => (
        <View key={id}>
          {i > 0 ? <Divider inset={spacing.lg} /> : null}
          <ShipmentRow id={id} t={vehicles[id]} selected={id === selectedId} onPress={() => onSelect(id)} />
        </View>
      ))}
      <Divider />
      <Text style={[typography.caption, styles.explainer, { color: colors.muted }]}>
        Each truck reports its cargo temperature every few seconds. OmniOps compares every reading with the
        product's safe range and alerts the crew the moment it is crossed.
      </Text>
    </View>
  );
}

function Fact({ label, value }) {
  const { colors } = useTheme();
  return (
    <View style={styles.fact}>
      <Text style={[typography.caption, { color: colors.muted }]}>{label}</Text>
      <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>{value}</Text>
    </View>
  );
}

function ShipmentDetail({ id, t, alerts, onBack, onReplay, onOpenAlerts }) {
  const { colors } = useTheme();
  const [trend, setTrend] = useState(null);
  const s = t?.shipment;
  const status = shipmentStatus(t);
  const red = status === 'out';

  useEffect(() => {
    let cancelled = false;
    const to = new Date();
    const from = new Date(to.getTime() - 60 * 60 * 1000);
    telemetryApi
      .aggregations(id, from.toISOString(), to.toISOString(), 5)
      .then((rows) => !cancelled && setTrend((rows ?? []).map((r) => r.avgTemperature)))
      .catch(() => !cancelled && setTrend([]));
    return () => { cancelled = true; };
  }, [id, t?.timestamp]);

  const recent = groupAlerts(alerts.filter((a) => a.vehicleId === id)).slice(0, 3);

  return (
    <View>
      <Pressable onPress={onBack} style={styles.back} accessibilityRole="button" accessibilityLabel="Back to all shipments">
        <Ionicons name="chevron-back" size={18} color={colors.accent} />
        <Text style={[typography.label, { color: colors.accent }]}>All shipments</Text>
      </Pressable>

      <View style={styles.detailHead}>
        <Text style={[typography.headline, { color: colors.text }]}>{s?.productName ?? id}</Text>
        <Text style={[typography.caption, { color: colors.muted }]}>
          {s ? `Batch ${s.batchNumber} · ` : ''}{id}
        </Text>
      </View>

      <View style={styles.reading}>
        <Text style={[typography.reading, { color: red ? colors.critical : colors.text }]}>
          {t ? `${t.engineTemperature} °C` : '—'}
        </Text>
        <Text style={[typography.body, { color: red ? colors.critical : colors.textSecondary }]}>
          {red ? 'Cargo is outside its safe range' : s ? 'Cargo is within its safe range' : statusText(status, t)}
        </Text>
      </View>
      {s ? <View style={styles.pad}><TempRangeBar value={t.engineTemperature} min={s.minSafeTempCelsius} max={s.maxSafeTempCelsius} /></View> : null}

      <View style={styles.pad}>
        <Text style={[typography.label, { color: colors.textSecondary, marginBottom: spacing.sm }]}>Last hour</Text>
        <BarChart values={trend ?? []} color={red ? colors.critical : colors.text} height={64} emptyText={trend ? 'Not enough history yet' : 'Loading…'} />
      </View>

      <View style={[styles.facts, styles.pad]}>
        <Fact label="Value at risk" value={s ? money(s.valueAtRiskUsd) : '—'} />
        <Fact label="Speed" value={t ? `${t.speed} km/h` : '—'} />
        <Fact label="Sensor battery" value={t?.batteryLevel != null ? `${Math.round(t.batteryLevel)}%` : '—'} />
        <Fact label="Last reading" value={t ? timeAgo(t.timestamp) : '—'} />
      </View>

      <Divider />
      <View style={styles.pad}>
        <Text style={[typography.label, { color: colors.textSecondary }]}>Recent alerts</Text>
        {recent.length === 0 ? (
          <Text style={[typography.body, { color: colors.muted, marginTop: spacing.sm }]}>None for this truck.</Text>
        ) : (
          recent.map((g) => (
            <Pressable key={g.key} onPress={onOpenAlerts} style={styles.alertLine}>
              <View style={[styles.dot, { backgroundColor: g.meta.severity === 'critical' ? colors.critical : g.meta.severity === 'warning' ? colors.warning : colors.muted }]} />
              <View style={styles.flex}>
                <Text style={[typography.label, { color: colors.text }]} numberOfLines={1}>
                  {g.meta.label}{g.count > 1 ? ` ×${g.count}` : ''}
                </Text>
                <Text style={[typography.caption, { color: colors.muted }]} numberOfLines={1}>{alertSummary(g.alert) || timeAgo(g.alert.generatedAt)}</Text>
              </View>
              <Text style={[typography.caption, { color: colors.muted }]}>{timeAgo(g.alert.generatedAt)}</Text>
            </Pressable>
          ))
        )}
        <View style={styles.actions}>
          <Button label="Replay last hour" variant="secondary" onPress={onReplay} style={styles.flex} />
          <Button label="All alerts" onPress={onOpenAlerts} style={styles.flex} />
        </View>
      </View>
    </View>
  );
}

export default function LiveView({ navigation }) {
  const { vehicleIds, vehicles, geofences, alerts, setSelectedVehicleId } = useFleet();
  const { colors, isDark } = useTheme();
  const { isWide, height } = useLayout();
  const insets = useSafeAreaInsets();
  const mapRef = useRef(null);
  const [detailId, setDetailId] = useState(null);
  const [mapType, setMapType] = useState('standard');
  const [expanded, setExpanded] = useState(false);
  const [replay, setReplay] = useState(null);

  const collapsedHeight = 248;
  const expandedHeight = Math.round(height * 0.72);
  const sheetHeight = useRef(new Animated.Value(collapsedHeight)).current;
  useEffect(() => {
    Animated.timing(sheetHeight, { toValue: expanded ? expandedHeight : collapsedHeight, duration: 220, useNativeDriver: false }).start();
  }, [expanded, expandedHeight, sheetHeight]);

  const select = (id) => {
    setSelectedVehicleId(id);
    setDetailId(id);
    setExpanded(true);
    const t = vehicles[id];
    if (t?.latitude && t?.longitude) {
      mapRef.current?.animateToRegion({ latitude: t.latitude, longitude: t.longitude, latitudeDelta: 0.03, longitudeDelta: 0.03 }, 700);
    }
  };

  const openReplay = (id) => {
    const latest = alerts.find((a) => a.vehicleId === id);
    setReplay({ alert: latest ?? { vehicleId: id, generatedAt: new Date().toISOString() }, shipmentId: resolveShipmentIdForVehicle(id, vehicles) });
  };

  const zoneStroke = isDark ? 'rgba(122,155,255,0.8)' : 'rgba(46,91,219,0.7)';
  const zoneFill = isDark ? 'rgba(122,155,255,0.08)' : 'rgba(46,91,219,0.06)';
  const pinColor = (id) => {
    if (id === detailId) return colors.accent;
    const s = shipmentStatus(vehicles[id]);
    if (s === 'out') return colors.critical;
    if (s === 'attention') return colors.warning;
    return isDark ? '#D5D7DB' : '#16181D';
  };

  const map = (
    <MapView
      ref={mapRef}
      style={StyleSheet.absoluteFill}
      provider={PROVIDER_DEFAULT}
      mapType={mapType}
      userInterfaceStyle={isDark ? 'dark' : 'light'}
      initialRegion={INITIAL_REGION}
    >
      {geofences.map((g) => {
        if (g.shapeType === 'Radius' && g.centerLatitude && g.centerLongitude && g.radiusMeters) {
          return (
            <Circle key={g.id} center={{ latitude: g.centerLatitude, longitude: g.centerLongitude }} radius={g.radiusMeters} strokeColor={zoneStroke} fillColor={zoneFill} />
          );
        }
        const coords = parsePolygon(g.polygonCoordinatesJson);
        return coords.length >= 3 ? <Polygon key={g.id} coordinates={coords} strokeColor={zoneStroke} fillColor={zoneFill} /> : null;
      })}
      {vehicleIds.map((id) => {
        const t = vehicles[id];
        if (!t?.latitude || !t?.longitude) return null;
        return (
          <Marker
            key={id}
            coordinate={{ latitude: t.latitude, longitude: t.longitude }}
            title={t.shipment?.productName ?? id}
            description={`${id} · ${t.engineTemperature} °C`}
            pinColor={pinColor(id)}
            onPress={() => select(id)}
            opacity={detailId && id !== detailId ? 0.7 : 1}
          />
        );
      })}
    </MapView>
  );

  const content = detailId ? (
    <ShipmentDetail
      id={detailId}
      t={vehicles[detailId]}
      alerts={alerts}
      onBack={() => { setDetailId(null); setExpanded(false); }}
      onReplay={() => openReplay(detailId)}
      onOpenAlerts={() => navigation.navigate('Alerts')}
    />
  ) : (
    <ShipmentList ids={vehicleIds} vehicles={vehicles} selectedId={detailId} onSelect={select} />
  );

  const mapTypeToggle = (
    <View style={[styles.mapToggle, { backgroundColor: colors.card, borderColor: colors.border }]}>
      {[['standard', 'Map'], ['satellite', 'Satellite']].map(([key, label]) => (
        <Pressable key={key} onPress={() => setMapType(key)} style={[styles.mapToggleItem, mapType === key && { backgroundColor: colors.sunken }]}>
          <Text style={[typography.label, { color: mapType === key ? colors.text : colors.textSecondary }]}>{label}</Text>
        </Pressable>
      ))}
    </View>
  );

  return (
    <View style={[styles.flex, { backgroundColor: colors.background }]}>
      {map}
      <View style={[styles.mapToggleWrap, { top: insets.top + spacing.md }]}>{mapTypeToggle}</View>

      {isWide ? (
        <View style={[styles.sidePanel, { maxHeight: height - 56 - spacing.lg * 2, backgroundColor: colors.card, borderColor: colors.border }, elevation(isDark, 2)]}>
          <ScrollView showsVerticalScrollIndicator={false}>{content}</ScrollView>
        </View>
      ) : (
        <Animated.View style={[styles.sheet, { height: sheetHeight, backgroundColor: colors.card }, elevation(isDark, 2)]}>
          <Pressable
            onPress={() => setExpanded((v) => !v)}
            style={styles.handleArea}
            accessibilityRole="button"
            accessibilityLabel={expanded ? 'Collapse panel' : 'Expand panel'}
          >
            <View style={[styles.handle, { backgroundColor: colors.border }]} />
          </Pressable>
          <ScrollView showsVerticalScrollIndicator={false} contentContainerStyle={{ paddingBottom: spacing.xl }}>{content}</ScrollView>
        </Animated.View>
      )}

      <IncidentReplay
        visible={!!replay}
        alert={replay?.alert}
        shipmentId={replay?.shipmentId}
        apiBaseUrl={process.env.EXPO_PUBLIC_API_URL}
        apiToken={process.env.EXPO_PUBLIC_API_TOKEN}
        onClose={() => setReplay(null)}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  // zIndex keeps floating UI above the web map's layers (Leaflet panes use 400–700).
  sidePanel: { position: 'absolute', top: spacing.lg, left: spacing.lg, width: 380, borderRadius: radius.xl, borderWidth: StyleSheet.hairlineWidth, overflow: 'hidden', zIndex: 1000 },
  sheet: { position: 'absolute', left: 0, right: 0, bottom: 0, borderTopLeftRadius: radius.xl, borderTopRightRadius: radius.xl, overflow: 'hidden', zIndex: 1000 },
  handleArea: { alignItems: 'center', paddingTop: 8, paddingBottom: 4 },
  handle: { width: 36, height: 5, borderRadius: 3 },
  mapToggleWrap: { position: 'absolute', right: spacing.lg, zIndex: 1000 },
  mapToggle: { flexDirection: 'row', borderRadius: radius.md, borderWidth: StyleSheet.hairlineWidth, padding: 3 },
  mapToggleItem: { paddingHorizontal: 12, paddingVertical: 6, borderRadius: 8 },
  panelHeader: { paddingHorizontal: spacing.lg, paddingTop: spacing.lg, paddingBottom: spacing.md, gap: 2 },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  rowRight: { alignItems: 'flex-end', maxWidth: 140 },
  explainer: { paddingHorizontal: spacing.lg, paddingVertical: spacing.lg },
  back: { flexDirection: 'row', alignItems: 'center', gap: 2, paddingHorizontal: spacing.md, paddingTop: spacing.md },
  detailHead: { paddingHorizontal: spacing.lg, paddingTop: spacing.sm, gap: 2 },
  reading: { paddingHorizontal: spacing.lg, paddingTop: spacing.lg },
  pad: { paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  facts: { flexDirection: 'row', flexWrap: 'wrap', rowGap: spacing.md },
  fact: { width: '50%', gap: 2 },
  alertLine: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingVertical: spacing.sm },
  dot: { width: 7, height: 7, borderRadius: 4 },
  actions: { flexDirection: 'row', gap: spacing.sm, marginTop: spacing.md },
});
