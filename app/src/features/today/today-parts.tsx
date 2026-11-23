// Building blocks of the Today panel.
import { Ionicons } from '@expo/vector-icons';
import { router } from 'expo-router';
import { useState, type ReactNode } from 'react';
import { Linking, Pressable, StyleSheet, Text, View } from 'react-native';
import type { Job } from '@/api/types';
import { Divider, TextAction } from '@/components/ui';
import { isActive } from '@/components/ui/pressable-state';
import { jobProgress, jobTitle, temperatureStatus } from '@/features/jobs/job-format';
import { callNumber, shareTrackingLink } from '@/features/office-queries';
import { useTheme } from '@/theme/theme-provider';
import { spacing, toneColors, typography, type Tone } from '@/theme/tokens';
import type { AttentionItem, SetupStep } from './today-rules';

export function JobRow({ job, selected, onPress }: { job: Job; selected: boolean; onPress: () => void }) {
  const { colors } = useTheme();
  const progress = jobProgress(job);
  const temp = temperatureStatus(job);
  const tone = progress.tone === 'warning' ? colors.warning : progress.tone === 'ok' ? colors.ok : colors.text;
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={`${jobTitle(job)}, ${progress.primary} ${progress.secondary}`}
      style={(s) => [styles.row, (selected || isActive(s)) && { backgroundColor: colors.sunken }]}
    >
      <View style={styles.flex}>
        <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>
          {jobTitle(job)}
        </Text>
        <Text style={[typography.caption, { color: colors.muted }]} numberOfLines={1}>
          {[job.vehicleName, job.destination].filter(Boolean).join(' · ')}
        </Text>
        {temp && temp.tone !== 'neutral' && temp.tone !== 'ok' ? (
          <Text style={[typography.caption, { color: toneColors(colors, temp.tone).fg }]} numberOfLines={1}>
            {temp.label}
          </Text>
        ) : null}
      </View>
      <View style={styles.rowRight}>
        <Text style={[typography.number, { color: tone }]} numberOfLines={1}>
          {progress.primary}
        </Text>
        {progress.secondary ? (
          <Text
            style={[typography.caption, { color: progress.tone === 'warning' ? colors.warning : colors.muted }]}
            numberOfLines={1}
          >
            {progress.secondary}
          </Text>
        ) : null}
      </View>
    </Pressable>
  );
}

export function JobRows({
  jobs,
  selectedId,
  onSelect,
}: {
  jobs: Job[];
  selectedId: string | null;
  onSelect: (job: Job) => void;
}) {
  return jobs.map((job, i) => (
    <View key={job.id}>
      {i > 0 ? <Divider inset={spacing.lg} /> : null}
      <JobRow job={job} selected={job.id === selectedId} onPress={() => onSelect(job)} />
    </View>
  ));
}

export function Section({
  title,
  count,
  right,
  children,
}: {
  title: string;
  count?: number;
  right?: ReactNode;
  children?: ReactNode;
}) {
  const { colors } = useTheme();
  return (
    <View>
      <View style={styles.sectionHead}>
        <Text style={[typography.label, styles.flex, { color: colors.textSecondary }]}>
          {title}
          {count != null ? ` · ${count}` : ''}
        </Text>
        {right}
      </View>
      {children}
    </View>
  );
}

/** A big number with a label; colour only when it needs attention. */
export function Count({
  label,
  value,
  tone,
  onPress,
}: {
  label: string;
  value: number;
  tone?: Tone | null;
  onPress?: () => void;
}) {
  const { colors } = useTheme();
  const color = tone === 'warning' ? colors.warning : tone === 'critical' ? colors.critical : colors.text;
  return (
    <Pressable
      onPress={onPress}
      disabled={!onPress}
      accessibilityRole={onPress ? 'button' : 'text'}
      accessibilityLabel={`${label}: ${value}`}
      style={styles.count}
    >
      <Text style={[typography.metric, { color }]}>{value}</Text>
      <Text style={[typography.caption, { color: colors.muted }]}>{label}</Text>
    </Pressable>
  );
}

export function AttentionRow({ item, job, onOpen }: { item: AttentionItem; job: Job | undefined; onOpen: () => void }) {
  const { colors } = useTheme();
  const [notice, setNotice] = useState<string | null>(null);
  const color = toneColors(colors, item.tone).fg;
  let action = { label: 'Open job', run: onOpen };
  if (item.action === 'call' && job?.driverPhone) {
    const phone = job.driverPhone;
    action = {
      label: `Call ${job.driverName?.split(' ')[0] ?? 'driver'}`,
      run: () => void Linking.openURL(callNumber(phone)),
    };
  } else if (item.action === 'link' && job?.trackingPath) {
    action = { label: 'Send link', run: () => void shareTrackingLink(job).then(setNotice) };
  }
  return (
    <View style={styles.attention}>
      <View style={[styles.bar, { backgroundColor: item.tone === 'neutral' ? colors.border : color }]} />
      <View style={[styles.flex, styles.attentionBody]}>
        <Pressable onPress={onOpen} accessibilityRole="button">
          <Text style={[typography.bodyStrong, { color: colors.text }]}>{item.title}</Text>
          <Text style={[typography.caption, { color: colors.textSecondary }]}>{item.detail}</Text>
        </Pressable>
        {item.action === 'call' && !job?.driverPhone ? (
          <Text style={[typography.caption, { color: colors.muted }]}>
            Add the driver&apos;s phone number under Fleet to call from here.
          </Text>
        ) : null}
        {notice ? <Text style={[typography.caption, { color: colors.ok }]}>{notice}</Text> : null}
      </View>
      <TextAction label={action.label} onPress={action.run} />
    </View>
  );
}

export function SetupChecklist({ steps }: { steps: SetupStep[] }) {
  const { colors } = useTheme();
  const done = steps.filter((s) => s.done).length;
  return (
    <View style={styles.pad}>
      <Text style={[typography.bodyStrong, { color: colors.text }]}>
        Get set up · {done} of {steps.length}
      </Text>
      <Text style={[typography.caption, { color: colors.muted, marginBottom: spacing.sm }]}>
        About ten minutes. After this, deliveries, proof and invoices run from here.
      </Text>
      {steps.map((s) => (
        <Pressable key={s.key} onPress={() => router.navigate(s.href)} accessibilityRole="button" style={styles.step}>
          <Ionicons
            name={s.done ? 'checkmark-circle' : 'ellipse-outline'}
            size={18}
            color={s.done ? colors.ok : colors.muted}
          />
          <Text
            style={[
              typography.body,
              styles.flex,
              { color: s.done ? colors.muted : colors.text, textDecorationLine: s.done ? 'line-through' : 'none' },
            ]}
          >
            {s.title}
          </Text>
          {!s.done ? <Ionicons name="chevron-forward" size={16} color={colors.muted} /> : null}
        </Pressable>
      ))}
    </View>
  );
}

export const todayStyles = StyleSheet.create({
  pad: { paddingHorizontal: spacing.lg, paddingVertical: spacing.md, gap: spacing.sm },
  moneyRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.sm,
  },
  counts: { flexDirection: 'row', paddingHorizontal: spacing.lg, paddingVertical: spacing.md, gap: spacing.md },
  header: { paddingHorizontal: spacing.lg, paddingTop: spacing.lg, gap: 2 },
  flex: { flex: 1 },
});

const styles = StyleSheet.create({
  flex: { flex: 1 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
  },
  rowRight: { alignItems: 'flex-end', maxWidth: 140 },
  sectionHead: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: spacing.lg,
    paddingTop: spacing.lg,
    paddingBottom: spacing.xs,
  },
  count: { flex: 1 },
  attention: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: spacing.md,
    paddingRight: spacing.lg,
    paddingVertical: spacing.md,
  },
  bar: { width: 3, alignSelf: 'stretch', borderRadius: 2, marginLeft: spacing.sm },
  attentionBody: { gap: 2 },
  pad: { paddingHorizontal: spacing.lg, paddingVertical: spacing.md, gap: spacing.sm },
  step: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, paddingVertical: 6 },
});
