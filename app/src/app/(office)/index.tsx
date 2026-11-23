// Home: the map of today's deliveries, and next to it what needs someone now.
// Laptop: a floating panel on the left. Phone: a bottom sheet that expands on tap.
import { useEffect, useMemo, useState } from 'react';
import { Animated, Pressable, ScrollView, StyleSheet, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import type { Job } from '@/api/types';
import { Segmented } from '@/components/ui';
import { isPositionStale, jobTitle } from '@/features/jobs/job-format';
import { DeliveryMap } from '@/features/map/delivery-map';
import type { Coord, MapFocus, MapLine, MapPin } from '@/features/map/map-types';
import { useJobs } from '@/features/office-queries';
import { JobGlance } from '@/features/today/job-glance';
import { TodayPanel } from '@/features/today/today-panel';
import { useLayout } from '@/hooks/use-layout';
import { clock } from '@/lib/time';
import { useTheme } from '@/theme/theme-provider';
import { floating, radius, spacing } from '@/theme/tokens';

const position = (job: Job): Coord | null =>
  job.lastLatitude != null && job.lastLongitude != null
    ? { latitude: job.lastLatitude, longitude: job.lastLongitude }
    : null;
const destination = (job: Job): Coord | null =>
  job.destinationLatitude != null && job.destinationLongitude != null
    ? { latitude: job.destinationLatitude, longitude: job.destinationLongitude }
    : null;

const SHEET_COLLAPSED = 248;

export default function Today() {
  const { colors, isDark } = useTheme();
  const { isWide, height } = useLayout();
  const insets = useSafeAreaInsets();
  const jobs = useJobs('Active');
  const list = useMemo(() => jobs.data ?? [], [jobs.data]);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [satellite, setSatellite] = useState(false);
  const [expanded, setExpanded] = useState(false);
  const [picked, setPicked] = useState<MapFocus | null>(null);
  const selected = list.find((j) => j.id === selectedId) ?? null;
  const padding = { top: 80, left: isWide ? 420 : 40, right: 40, bottom: isWide ? 40 : 300 };

  // Until a job is picked, frame every moving truck and its destination. The key stays 'all', so the map frames
  // them once when they first arrive and doesn't jump as positions update.
  const overview = useMemo<MapFocus | null>(() => {
    const coords = list
      .filter((j) => j.status === 'InProgress')
      .flatMap((j) => [position(j), destination(j)])
      .filter((c): c is Coord => !!c);
    return coords.length
      ? { key: 'all', coords, padding: { top: 80, left: isWide ? 420 : 40, right: 40, bottom: isWide ? 40 : 300 } }
      : null;
  }, [list, isWide]);
  const focus = picked ?? overview;

  const select = (job: Job) => {
    setSelectedId(job.id);
    setExpanded(true);
    const coords = [position(job), job.status === 'InProgress' || !position(job) ? destination(job) : null].filter(
      (c): c is Coord => !!c,
    );
    if (coords.length) setPicked({ key: `${job.id}:${Date.now()}`, coords, padding });
  };

  const pins: MapPin[] = [];
  const lines: MapLine[] = [];
  const truckColor = (job: Job) =>
    isPositionStale(job) ? colors.warning : job.id === selectedId ? colors.accent : isDark ? '#D5D7DB' : '#16181D';
  for (const job of list) {
    const at = position(job);
    if (job.status !== 'InProgress' || !at) continue;
    pins.push({
      id: job.id,
      ...at,
      color: truckColor(job),
      title: `${job.vehicleName} → ${jobTitle(job)}`,
      description:
        isPositionStale(job) && job.lastPositionAtUtc
          ? `No signal since ${clock(job.lastPositionAtUtc)}`
          : job.etaUtc
            ? `Arrives around ${clock(job.etaUtc)}`
            : 'On the way',
      emphasis: !selectedId || job.id === selectedId,
      onPress: () => select(job),
    });
  }
  if (selected) {
    const to = destination(selected);
    const at = position(selected);
    if (to)
      pins.push({
        id: `dest-${selected.id}`,
        ...to,
        color: isDark ? '#ECEDEF' : '#16181D',
        title: jobTitle(selected),
        description: selected.destination ?? undefined,
      });
    if (to && at && selected.status === 'InProgress')
      lines.push({ id: 'route', points: [at, to], color: colors.accent, dashed: true });
  }

  const expandedHeight = Math.round(height * 0.72);
  const [sheetHeight] = useState(() => new Animated.Value(SHEET_COLLAPSED));
  useEffect(() => {
    Animated.timing(sheetHeight, {
      toValue: expanded ? expandedHeight : SHEET_COLLAPSED,
      duration: 220,
      useNativeDriver: false,
    }).start();
  }, [expanded, expandedHeight, sheetHeight]);

  const content = selected ? (
    <JobGlance
      job={selected}
      onBack={() => {
        setSelectedId(null);
        setExpanded(false);
      }}
    />
  ) : (
    <TodayPanel
      jobs={list}
      loading={jobs.isPending}
      error={jobs.error}
      onRetry={() => void jobs.refetch()}
      selectedId={selectedId}
      onSelect={select}
    />
  );

  return (
    <View style={[styles.flex, { backgroundColor: colors.background }]}>
      <DeliveryMap pins={pins} lines={lines} satellite={satellite} dark={isDark} focus={focus} />
      <View style={[styles.mapType, { top: (isWide ? 0 : insets.top) + spacing.md }]}>
        <View style={[styles.mapTypeBox, floating(isDark), { backgroundColor: colors.card }]}>
          <Segmented
            options={[
              { key: 'map', label: 'Map' },
              { key: 'satellite', label: 'Satellite' },
            ]}
            value={satellite ? 'satellite' : 'map'}
            onChange={(key) => setSatellite(key === 'satellite')}
          />
        </View>
      </View>

      {isWide ? (
        <View
          style={[
            styles.side,
            floating(isDark),
            { maxHeight: height - 56 - spacing.lg * 2, backgroundColor: colors.card },
          ]}
        >
          <ScrollView showsVerticalScrollIndicator={false}>{content}</ScrollView>
        </View>
      ) : (
        <Animated.View style={[styles.sheet, floating(isDark), { height: sheetHeight, backgroundColor: colors.card }]}>
          <Pressable
            onPress={() => setExpanded((v) => !v)}
            style={styles.handleArea}
            accessibilityRole="button"
            accessibilityLabel={expanded ? 'Collapse panel' : 'Expand panel'}
          >
            <View style={[styles.handle, { backgroundColor: colors.border }]} />
          </Pressable>
          <ScrollView showsVerticalScrollIndicator={false} contentContainerStyle={{ paddingBottom: spacing.xl }}>
            {content}
          </ScrollView>
        </Animated.View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  // zIndex keeps floating UI above the web map's layers (Leaflet panes use 400–700).
  side: {
    position: 'absolute',
    top: spacing.lg,
    left: spacing.lg,
    width: 380,
    borderRadius: radius.xl,
    overflow: 'hidden',
    zIndex: 1000,
  },
  sheet: {
    position: 'absolute',
    left: 0,
    right: 0,
    bottom: 0,
    borderTopLeftRadius: radius.xl,
    borderTopRightRadius: radius.xl,
    overflow: 'hidden',
    zIndex: 1000,
  },
  handleArea: { alignItems: 'center', paddingTop: 8, paddingBottom: 4 },
  handle: { width: 36, height: 5, borderRadius: 3 },
  mapType: { position: 'absolute', right: spacing.lg, zIndex: 1000 },
  mapTypeBox: { borderRadius: radius.md },
});
