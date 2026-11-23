// One job, at a glance, in the Today panel: where it is, when it arrives, and the next thing to do.
import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import { useState } from 'react';
import { Linking, Pressable, StyleSheet, Text, View } from 'react-native';
import type { Job } from '@/api/types';
import { Button, Divider, Status } from '@/components/ui';
import { formatCelsius, isPositionStale, jobProgress, jobTitle, temperatureStatus } from '@/features/jobs/job-format';
import { formatDuration, minutesSince } from '@/features/jobs/waiting';
import { callNumber, shareTrackingLink } from '@/features/office-queries';
import { clock, timeAgo } from '@/lib/time';
import { useTheme } from '@/theme/theme-provider';
import { spacing, toneColors, typography } from '@/theme/tokens';

const hasPosition = (job: Job) => job.lastLatitude != null && job.lastLongitude != null;

function headline(job: Job, stale: boolean): [string, string] {
  if (job.status === 'InProgress') {
    if (stale && job.lastPositionAtUtc)
      return [
        'No signal',
        `The phone has not reported since ${clock(job.lastPositionAtUtc)} (${timeAgo(job.lastPositionAtUtc)}).`,
      ];
    if (job.remainingKm === 0) return ['Arriving now', 'The truck is at the destination.'];
    if (job.etaUtc)
      return [
        `Arrives around ${clock(job.etaUtc)}`,
        `About ${Math.round(job.remainingKm ?? 0)} km to go. Estimate from the last position.`,
      ];
    return [
      'On the way',
      hasPosition(job)
        ? 'No map position for the destination, so no arrival estimate.'
        : 'Waiting for the first location from the phone.',
    ];
  }
  if (job.status === 'Planned') return ['Not started', 'The truck appears on the map when the driver starts the trip.'];
  return [
    `Delivered ${job.deliveredAtUtc ? clock(job.deliveredAtUtc) : ''}`.trim(),
    job.recipientName ? `Received by ${job.recipientName}.` : '',
  ];
}

function Fact({ label, value }: { label: string; value: string | null | undefined }) {
  const { colors } = useTheme();
  return (
    <View style={styles.fact}>
      <Text style={[typography.caption, { color: colors.muted }]}>{label}</Text>
      <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>
        {value || '—'}
      </Text>
    </View>
  );
}

export function JobGlance({ job, onBack }: { job: Job; onBack: () => void }) {
  const { colors } = useTheme();
  const [notice, setNotice] = useState<string | null>(null);
  const progress = jobProgress(job);
  const stale = isPositionStale(job);
  const [title, sub] = headline(job, stale);
  const temp = temperatureStatus(job);
  const delivered = job.status === 'Delivered';
  const status =
    progress.tone === 'warning'
      ? 'Needs a call'
      : delivered
        ? 'Delivered'
        : job.status === 'Planned'
          ? 'Planned'
          : 'On the way';

  return (
    <View>
      <Pressable
        onPress={onBack}
        style={styles.back}
        accessibilityRole="button"
        accessibilityLabel="Back to today's deliveries"
      >
        <Ionicons name="chevron-back" size={18} color={colors.accent} />
        <Text style={[typography.label, { color: colors.accent }]}>Today</Text>
      </Pressable>

      <View style={styles.head}>
        <Text style={[typography.headline, { color: colors.text }]}>{jobTitle(job)}</Text>
        <Text style={[typography.caption, { color: colors.muted }]}>{job.destination}</Text>
      </View>

      <View style={styles.reading}>
        <Status label={status} tone={progress.tone} />
        <Text style={[typography.headline, { color: stale ? colors.warning : colors.text, marginTop: spacing.sm }]}>
          {title}
        </Text>
        {sub ? <Text style={[typography.body, { color: colors.textSecondary }]}>{sub}</Text> : null}
      </View>

      <View style={[styles.facts, styles.pad]}>
        <Fact label="Truck" value={job.vehicleName} />
        <Fact label="Driver" value={job.driverName} />
        <Fact label="Order" value={job.reference} />
        <Fact
          label="Speed"
          value={
            job.status === 'InProgress' && job.lastSpeedKmh != null && !stale
              ? `${Math.round(job.lastSpeedKmh)} km/h`
              : null
          }
        />
        <Fact label="From" value={job.origin} />
        <Fact label="Last seen" value={job.lastPositionAtUtc ? timeAgo(job.lastPositionAtUtc) : null} />
        {job.minTempCelsius != null ? (
          <Fact
            label="Load temperature"
            value={
              job.lastTemperatureCelsius != null && job.lastTemperatureAtUtc
                ? `${formatCelsius(job.lastTemperatureCelsius)} · ${timeAgo(job.lastTemperatureAtUtc)}`
                : 'Not logged yet'
            }
          />
        ) : null}
      </View>
      {job.status === 'InProgress' && job.deliveryArrivedAtUtc ? (
        <Text style={[typography.label, styles.notice, { color: colors.textSecondary }]}>
          At the customer since {clock(job.deliveryArrivedAtUtc)} · waiting{' '}
          {formatDuration(minutesSince(job.deliveryArrivedAtUtc))}
        </Text>
      ) : null}
      {temp && temp.tone !== 'ok' && temp.tone !== 'neutral' ? (
        <Text style={[typography.label, styles.notice, { color: toneColors(colors, temp.tone).fg }]}>{temp.label}</Text>
      ) : null}

      <Divider />
      <View style={[styles.pad, styles.actions]}>
        {job.trackingPath && !delivered ? (
          <Button
            label="Send customer link"
            variant="secondary"
            onPress={() => void shareTrackingLink(job).then(setNotice)}
            style={styles.flex}
          />
        ) : null}
        {job.driverPhone && !delivered ? (
          <Button
            label={`Call ${job.driverName?.split(' ')[0] ?? 'driver'}`}
            variant="secondary"
            onPress={() => void Linking.openURL(callNumber(job.driverPhone!))}
            style={styles.flex}
          />
        ) : null}
        <Button
          label="Open job"
          onPress={() => router.navigate({ pathname: '/jobs', params: { open: job.id } })}
          style={styles.flex}
        />
      </View>
      {notice ? <Text style={[typography.caption, styles.notice, { color: colors.ok }]}>{notice}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  back: { flexDirection: 'row', alignItems: 'center', gap: 2, paddingHorizontal: spacing.md, paddingTop: spacing.md },
  head: { paddingHorizontal: spacing.lg, paddingTop: spacing.sm, gap: 2 },
  reading: { paddingHorizontal: spacing.lg, paddingTop: spacing.lg, gap: 2 },
  pad: { paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  facts: { flexDirection: 'row', flexWrap: 'wrap', rowGap: spacing.md },
  fact: { width: '50%', gap: 2 },
  actions: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
  notice: { paddingHorizontal: spacing.lg, paddingBottom: spacing.md },
});
