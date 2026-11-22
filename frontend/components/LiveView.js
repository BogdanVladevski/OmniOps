// Home screen ("Today"): the map of every delivery, and next to it what needs someone now.
// Laptop: floating panel on the left. Phone: bottom sheet that expands on tap.
import React, { useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { Animated, Linking, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import MapView, { Circle, Marker, Polygon, Polyline, PROVIDER_DEFAULT } from 'react-native-maps';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useFleet } from '../contexts/FleetContext';
import { useTheme } from '../contexts/ThemeContext';
import { elevation, radius, spacing, toneColors, typography } from '../theme/tokens';
import { jobsApi } from '../utils/api';
import { formatCelsius, groupJobsForToday, isPositionStale, jobProgress, jobTitle, temperatureStatus } from '../utils/jobs';
import { onJobsChanged } from '../utils/events';
import { formatDuration, minutesSince } from '../utils/waiting';
import { useLayout } from '../utils/layout';
import { shareTrackingLink } from './JobsBoard';
import TodayPanel from './TodayPanel';
import { Button, Divider, Pill, timeAgo } from './ui/kit';

const INITIAL_REGION = { latitude: 41.75, longitude: 21.45, latitudeDelta: 1.6, longitudeDelta: 1.6 };
const POLL_MS = 15000;
const clock = (iso) => new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

function parsePolygon(json) {
  try {
    return JSON.parse(json ?? '[]').filter((p) => p.length >= 2).map((p) => ({ latitude: p[0], longitude: p[1] }));
  } catch {
    return [];
  }
}

const hasPosition = (job) => job?.lastLatitude != null && job?.lastLongitude != null;
const hasDestination = (job) => job?.destinationLatitude != null && job?.destinationLongitude != null;

function Fact({ label, value }) {
  const { colors } = useTheme();
  return (
    <View style={styles.fact}>
      <Text style={[typography.caption, { color: colors.muted }]}>{label}</Text>
      <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>{value || '—'}</Text>
    </View>
  );
}

function JobDetail({ job, onBack, onOpenJob }) {
  const { colors } = useTheme();
  const [notice, setNotice] = useState(null);
  const progress = jobProgress(job);
  const stale = isPositionStale(job);

  let headline;
  let sub;
  if (job.status === 'InProgress') {
    if (stale) {
      headline = 'No signal';
      sub = `The phone has not reported since ${clock(job.lastPositionAtUtc)} (${timeAgo(job.lastPositionAtUtc)}).`;
    } else if (job.remainingKm === 0) {
      headline = 'Arriving now';
      sub = 'The truck is at the destination.';
    } else if (job.etaUtc) {
      headline = `Arrives around ${clock(job.etaUtc)}`;
      sub = `About ${Math.round(job.remainingKm)} km to go. Estimate from the last position.`;
    } else {
      headline = 'On the way';
      sub = hasPosition(job) ? 'No map position for the destination, so no arrival estimate.' : 'Waiting for the first location from the phone.';
    }
  } else if (job.status === 'Planned') {
    headline = 'Not started';
    sub = 'The truck appears on the map when the driver starts the trip.';
  } else {
    headline = `Delivered ${job.deliveredAtUtc ? clock(job.deliveredAtUtc) : ''}`.trim();
    sub = job.recipientName ? `Received by ${job.recipientName}.` : '';
  }

  return (
    <View>
      <Pressable onPress={onBack} style={styles.back} accessibilityRole="button" accessibilityLabel="Back to today's deliveries">
        <Ionicons name="chevron-back" size={18} color={colors.accent} />
        <Text style={[typography.label, { color: colors.accent }]}>Today</Text>
      </Pressable>

      <View style={styles.detailHead}>
        <Text style={[typography.headline, { color: colors.text }]}>{jobTitle(job)}</Text>
        <Text style={[typography.caption, { color: colors.muted }]}>{job.destination}</Text>
      </View>

      <View style={styles.reading}>
        <Pill label={progress.tone === 'warning' ? 'Needs a call' : job.status === 'Completed' ? 'Delivered' : job.status === 'Planned' ? 'Planned' : 'On the way'} tone={progress.tone} />
        <Text style={[typography.headline, { color: stale ? colors.warning : colors.text, marginTop: spacing.sm }]}>{headline}</Text>
        {sub ? <Text style={[typography.body, { color: colors.textSecondary }]}>{sub}</Text> : null}
      </View>

      <View style={[styles.facts, styles.pad]}>
        <Fact label="Truck" value={job.vehicleLabel} />
        <Fact label="Driver" value={job.driverName} />
        <Fact label="Order" value={job.reference} />
        <Fact label="Speed" value={job.status === 'InProgress' && job.lastSpeedKmh != null && !stale ? `${Math.round(job.lastSpeedKmh)} km/h` : null} />
        <Fact label="From" value={job.origin} />
        <Fact label="Last seen" value={job.lastPositionAtUtc ? timeAgo(job.lastPositionAtUtc) : null} />
        {job.minTempCelsius != null ? (
          <Fact
            label="Load temperature"
            value={job.lastTemperatureCelsius != null ? `${formatCelsius(job.lastTemperatureCelsius)} · ${timeAgo(job.lastTemperatureAtUtc)}` : 'Not logged yet'}
          />
        ) : null}
      </View>
      {job.status === 'InProgress' && job.deliveryArrivedAtUtc ? (
        <Text style={[typography.label, styles.notice, { color: colors.textSecondary }]}>
          At the customer since {clock(job.deliveryArrivedAtUtc)} · waiting {formatDuration(minutesSince(job.deliveryArrivedAtUtc))}
        </Text>
      ) : null}
      {temperatureStatus(job) && temperatureStatus(job).tone !== 'ok' && temperatureStatus(job).tone !== 'neutral' ? (
        <Text style={[typography.label, styles.notice, { color: toneColors(colors, temperatureStatus(job).tone).fg }]}>
          {temperatureStatus(job).label}
        </Text>
      ) : null}

      <Divider />
      <View style={[styles.pad, styles.actions]}>
        {job.trackingPath && job.status !== 'Completed' ? (
          <Button
            label="Send customer link"
            variant="secondary"
            onPress={async () => setNotice(await shareTrackingLink(job))}
            style={styles.flex}
          />
        ) : null}
        {job.driverPhone && job.status !== 'Completed' ? (
          <Button
            label={`Call ${job.driverName?.split(' ')[0] ?? 'driver'}`}
            variant="secondary"
            onPress={() => Linking.openURL(`tel:${job.driverPhone.replace(/[^+\d]/g, '')}`)}
            style={styles.flex}
          />
        ) : null}
        <Button label="Open job" onPress={onOpenJob} style={styles.flex} />
      </View>
      {notice ? <Text style={[typography.caption, styles.notice, { color: colors.ok }]}>{notice}</Text> : null}
    </View>
  );
}

export default function LiveView({ navigation }) {
  const { vehicles, geofences } = useFleet();
  const { colors, isDark } = useTheme();
  const { isWide, height } = useLayout();
  const insets = useSafeAreaInsets();
  const mapRef = useRef(null);
  const fitted = useRef(false);
  const [jobs, setJobs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [selectedId, setSelectedId] = useState(null);
  const [mapType, setMapType] = useState('standard');
  const [expanded, setExpanded] = useState(false);

  const load = useCallback(async () => {
    try {
      setJobs(await jobsApi.list('active'));
      setError(null);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
    const timer = setInterval(load, POLL_MS);
    const offChanged = onJobsChanged(load);
    const offFocus = navigation?.addListener?.('focus', load);
    return () => { clearInterval(timer); offChanged(); offFocus?.(); };
  }, [load, navigation]);

  const groups = useMemo(() => groupJobsForToday(jobs), [jobs]);
  const selected = jobs.find((j) => j.id === selectedId);

  // Trucks with telemetry but no running job: shown quietly so the office still knows where they are.
  const busyTrucks = new Set(groups.onTheWay.map((j) => j.vehicleLabel));
  const idleTrucks = Object.entries(vehicles ?? {})
    .filter(([id, t]) => !busyTrucks.has(id) && t?.latitude && t?.longitude);

  // Frame all moving trucks and their destinations once, when they first arrive.
  useEffect(() => {
    if (fitted.current || groups.onTheWay.length === 0) return;
    const coords = groups.onTheWay.flatMap((j) => [
      hasPosition(j) && { latitude: j.lastLatitude, longitude: j.lastLongitude },
      hasDestination(j) && { latitude: j.destinationLatitude, longitude: j.destinationLongitude },
    ]).filter(Boolean);
    if (coords.length) {
      fitted.current = true;
      setTimeout(() => mapRef.current?.fitToCoordinates(coords, { edgePadding: { top: 80, left: isWide ? 420 : 40, right: 40, bottom: isWide ? 40 : 280 } }), 300);
    }
  }, [groups.onTheWay, isWide]);

  const collapsedHeight = 248;
  const expandedHeight = Math.round(height * 0.72);
  const sheetHeight = useRef(new Animated.Value(collapsedHeight)).current;
  useEffect(() => {
    Animated.timing(sheetHeight, { toValue: expanded ? expandedHeight : collapsedHeight, duration: 220, useNativeDriver: false }).start();
  }, [expanded, expandedHeight, sheetHeight]);

  const select = (job) => {
    setSelectedId(job.id);
    setExpanded(true);
    const focus = hasPosition(job)
      ? { latitude: job.lastLatitude, longitude: job.lastLongitude }
      : hasDestination(job) ? { latitude: job.destinationLatitude, longitude: job.destinationLongitude } : null;
    if (focus && hasPosition(job) && hasDestination(job) && job.status === 'InProgress') {
      mapRef.current?.fitToCoordinates(
        [focus, { latitude: job.destinationLatitude, longitude: job.destinationLongitude }],
        { edgePadding: { top: 80, left: isWide ? 420 : 40, right: 40, bottom: isWide ? 40 : 320 } },
      );
    } else if (focus) {
      mapRef.current?.animateToRegion({ ...focus, latitudeDelta: 0.3, longitudeDelta: 0.3 }, 700);
    }
  };

  const zoneStroke = isDark ? 'rgba(122,155,255,0.8)' : 'rgba(46,91,219,0.7)';
  const zoneFill = isDark ? 'rgba(122,155,255,0.08)' : 'rgba(46,91,219,0.06)';
  const quiet = isDark ? '#8A8F99' : '#9CA0A8';
  const truckColor = (job) => {
    if (isPositionStale(job)) return toneColors(colors, 'warning').fg;
    return job.id === selectedId ? colors.accent : isDark ? '#D5D7DB' : '#16181D';
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

      {idleTrucks.map(([id, t]) => (
        <Marker
          key={`idle-${id}`}
          coordinate={{ latitude: t.latitude, longitude: t.longitude }}
          title={id}
          description={`No delivery running · seen ${timeAgo(t.timestamp)}`}
          pinColor={quiet}
          opacity={0.6}
        />
      ))}

      {selected && hasPosition(selected) && hasDestination(selected) && selected.status === 'InProgress' ? (
        <Polyline
          coordinates={[
            { latitude: selected.lastLatitude, longitude: selected.lastLongitude },
            { latitude: selected.destinationLatitude, longitude: selected.destinationLongitude },
          ]}
          strokeColor={colors.accent}
          strokeWidth={2}
          lineDashPattern={[6, 6]}
        />
      ) : null}
      {selected && hasDestination(selected) ? (
        <Marker
          key={`dest-${selected.id}`}
          coordinate={{ latitude: selected.destinationLatitude, longitude: selected.destinationLongitude }}
          title={jobTitle(selected)}
          description={selected.destination}
          pinColor={isDark ? '#ECEDEF' : '#16181D'}
          opacity={0.9}
        />
      ) : null}

      {groups.onTheWay.filter(hasPosition).map((job) => (
        <Marker
          key={job.id}
          coordinate={{ latitude: job.lastLatitude, longitude: job.lastLongitude }}
          title={`${job.vehicleLabel} → ${jobTitle(job)}`}
          description={jobProgress(job).primary === 'No signal' ? `No signal since ${clock(job.lastPositionAtUtc)}` : job.etaUtc ? `Arrives around ${clock(job.etaUtc)}` : 'On the way'}
          pinColor={truckColor(job)}
          onPress={() => select(job)}
        />
      ))}
    </MapView>
  );

  const content = selected ? (
    <JobDetail
      job={selected}
      onBack={() => { setSelectedId(null); setExpanded(false); }}
      onOpenJob={() => navigation.navigate('Jobs', { openJobId: selected.id })}
    />
  ) : (
    <TodayPanel
      jobs={jobs}
      groups={groups}
      loading={loading}
      error={error}
      selectedId={selectedId}
      onSelect={select}
      navigation={navigation}
    />
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
  sectionTitle: { paddingHorizontal: spacing.lg, paddingTop: spacing.md, paddingBottom: spacing.xs },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  rowRight: { alignItems: 'flex-end', maxWidth: 140 },
  empty: { padding: spacing.lg, gap: spacing.md },
  back: { flexDirection: 'row', alignItems: 'center', gap: 2, paddingHorizontal: spacing.md, paddingTop: spacing.md },
  detailHead: { paddingHorizontal: spacing.lg, paddingTop: spacing.sm, gap: 2 },
  reading: { paddingHorizontal: spacing.lg, paddingTop: spacing.lg, gap: 2 },
  pad: { paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  facts: { flexDirection: 'row', flexWrap: 'wrap', rowGap: spacing.md },
  fact: { width: '50%', gap: 2 },
  actions: { flexDirection: 'row', gap: spacing.sm },
  notice: { paddingHorizontal: spacing.lg, paddingBottom: spacing.md },
});
