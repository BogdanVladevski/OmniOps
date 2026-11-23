// "Today": three numbers that matter (moving, needs you, to invoice), what needs someone now in plain words with
// one next step each, the trucks on the road, and the money waiting. New companies see a short setup checklist.
import React, { useCallback, useEffect, useState } from 'react';
import { Linking, Pressable, StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTheme } from '../contexts/ThemeContext';
import { spacing, toneColors, typography } from '../theme/tokens';
import { billingApi, jobsApi } from '../utils/api';
import { onJobsChanged } from '../utils/events';
import { money } from '../utils/invoices';
import { isPositionStale, jobProgress, jobTitle, temperatureStatus } from '../utils/jobs';
import { attentionItems, movingJobs, setupSteps } from '../utils/today';
import { shareTrackingLink } from './JobsBoard';
import { Button, Divider } from './ui/kit';

const MONEY_POLL_MS = 60000;

function JobRow({ job, selected, onPress }) {
  const { colors } = useTheme();
  const progress = jobProgress(job);
  const temp = temperatureStatus(job);
  const tone = progress.tone === 'warning' ? colors.warning : progress.tone === 'ok' ? colors.ok : colors.text;
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityLabel={`${jobTitle(job)}, ${progress.primary} ${progress.secondary}`}
      style={({ hovered, pressed }) => [styles.row, (selected || hovered || pressed) && { backgroundColor: colors.sunken }]}
    >
      <View style={styles.flex}>
        <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>{jobTitle(job)}</Text>
        <Text style={[typography.caption, { color: colors.muted }]} numberOfLines={1}>
          {[job.vehicleLabel, job.destination].filter(Boolean).join(' · ')}
        </Text>
        {temp && temp.tone !== 'neutral' && temp.tone !== 'ok' ? (
          <Text style={[typography.caption, { color: toneColors(colors, temp.tone).fg }]} numberOfLines={1}>{temp.label}</Text>
        ) : null}
      </View>
      <View style={styles.rowRight}>
        <Text style={[typography.number, { color: tone }]} numberOfLines={1}>{progress.primary}</Text>
        {progress.secondary ? (
          <Text style={[typography.caption, { color: progress.tone === 'warning' ? colors.warning : colors.muted }]} numberOfLines={1}>
            {progress.secondary}
          </Text>
        ) : null}
      </View>
    </Pressable>
  );
}

function Section({ title, count, children, right }) {
  const { colors } = useTheme();
  return (
    <View>
      <View style={styles.sectionHead}>
        <Text style={[typography.label, styles.flex, { color: colors.textSecondary }]}>
          {title}{count != null ? ` · ${count}` : ''}
        </Text>
        {right}
      </View>
      {children}
    </View>
  );
}

function Rows({ jobs, selectedId, onSelect }) {
  return jobs.map((job, i) => (
    <View key={job.id}>
      {i > 0 ? <Divider inset={spacing.lg} /> : null}
      <JobRow job={job} selected={job.id === selectedId} onPress={() => onSelect(job)} />
    </View>
  ));
}

/** A big number with a label; colour only when it needs attention. */
function Count({ label, value, tone, onPress }) {
  const { colors } = useTheme();
  const color = tone === 'warning' ? colors.warning : tone === 'critical' ? colors.critical : colors.text;
  return (
    <Pressable onPress={onPress} accessibilityRole="button" accessibilityLabel={`${label}: ${value}`} style={styles.count}>
      <Text style={[styles.countValue, { color }]}>{value}</Text>
      <Text style={[typography.caption, { color: colors.muted }]}>{label}</Text>
    </Pressable>
  );
}

function AttentionRow({ item, job, onOpen }) {
  const { colors } = useTheme();
  const [notice, setNotice] = useState(null);
  const color = toneColors(colors, item.tone === 'neutral' ? 'neutral' : item.tone).fg;
  let action = { label: 'Open job', run: onOpen };
  if (item.action === 'call' && job?.driverPhone) {
    action = { label: `Call ${job.driverName?.split(' ')[0] ?? 'driver'}`, run: () => Linking.openURL(`tel:${job.driverPhone.replace(/[^+\d]/g, '')}`) };
  } else if (item.action === 'link' && job?.trackingPath) {
    action = { label: 'Send link', run: async () => setNotice(await shareTrackingLink(job)) };
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
          <Text style={[typography.caption, { color: colors.muted }]}>Add the driver's phone number under Fleet to call from here.</Text>
        ) : null}
        {notice ? <Text style={[typography.caption, { color: colors.ok }]}>{notice}</Text> : null}
      </View>
      <Pressable onPress={action.run} accessibilityRole="button" hitSlop={8} style={styles.attentionAction}>
        <Text style={[typography.label, { color: colors.accent }]}>{action.label}</Text>
      </Pressable>
    </View>
  );
}

function Checklist({ steps, navigation }) {
  const { colors } = useTheme();
  const done = steps.filter((s) => s.done).length;
  return (
    <View style={styles.pad}>
      <Text style={[typography.bodyStrong, { color: colors.text }]}>Get set up · {done} of {steps.length}</Text>
      <Text style={[typography.caption, { color: colors.muted, marginBottom: spacing.sm }]}>
        About ten minutes. After this, deliveries, proof and invoices run from here.
      </Text>
      {steps.map((s) => (
        <Pressable key={s.key} onPress={() => navigation.navigate(s.screen)} accessibilityRole="button" style={styles.step}>
          <Ionicons name={s.done ? 'checkmark-circle' : 'ellipse-outline'} size={18} color={s.done ? colors.ok : colors.muted} />
          <Text style={[typography.body, styles.flex, { color: s.done ? colors.muted : colors.text, textDecorationLine: s.done ? 'line-through' : 'none' }]}>
            {s.title}
          </Text>
          {!s.done ? <Ionicons name="chevron-forward" size={16} color={colors.muted} /> : null}
        </Pressable>
      ))}
    </View>
  );
}

export default function TodayPanel({ jobs, groups, loading, error, selectedId, onSelect, navigation }) {
  const { colors } = useTheme();
  const [money_, setMoney] = useState(null);
  const [profile, setProfile] = useState(null);
  const [counts, setCounts] = useState(null);
  const [showDelivered, setShowDelivered] = useState(false);

  const loadMoney = useCallback(async () => {
    const [overview, p] = await Promise.all([billingApi.overview().catch(() => null), billingApi.profile().catch(() => null)]);
    setMoney(overview);
    setProfile(p);
  }, []);

  useEffect(() => {
    loadMoney();
    const timer = setInterval(loadMoney, MONEY_POLL_MS);
    const off = onJobsChanged(loadMoney);
    return () => { clearInterval(timer); off(); };
  }, [loadMoney]);

  // The setup checklist needs to know whether trucks and drivers exist; only asked while there are no jobs.
  const fresh = !loading && jobs.length === 0;
  useEffect(() => {
    if (!fresh) return;
    jobsApi.assignable().then((a) => setCounts({ vehicles: a.vehicles.length, drivers: a.drivers.length })).catch(() => {});
  }, [fresh]);

  const items = attentionItems(jobs, { freeWaitingMinutes: profile?.freeWaitingMinutes ?? 120 });
  const moving = movingJobs(jobs);
  const jobById = Object.fromEntries(jobs.map((j) => [j.id, j]));
  const toInvoice = money_?.toInvoice?.length ?? 0;
  const currency = money_?.currency ?? 'EUR';
  const steps = setupSteps({ ...(counts ?? {}), jobs, invoiceSettingsDone: !!profile?.isComplete });
  const showChecklist = fresh || (jobs.length > 0 && jobs.length < 3 && steps.some((s) => !s.done));
  const silent = moving.filter((j) => isPositionStale(j)).length;
  const today = new Date().toLocaleDateString([], { weekday: 'long', day: 'numeric', month: 'long' });

  return (
    <View>
      <View style={styles.header}>
        <Text style={[typography.title, { color: colors.text }]}>Today</Text>
        <Text style={[typography.caption, { color: colors.muted }]}>{today}</Text>
        {error ? <Text style={[typography.caption, { color: colors.critical }]}>{error}</Text> : null}
      </View>

      <View style={styles.counts}>
        <Count label="Moving" value={moving.length} tone={silent ? 'warning' : null} />
        <Count label="Need you" value={items.length} tone={items.some((i) => i.tone === 'critical') ? 'critical' : items.some((i) => i.tone === 'warning') ? 'warning' : null} />
        <Count label="To invoice" value={toInvoice} onPress={() => navigation.navigate('Invoices', { tab: 'toInvoice' })} />
      </View>
      <Divider />

      {showChecklist ? (
        <>
          <Checklist steps={steps} navigation={navigation} />
          <Divider />
        </>
      ) : null}

      {items.length ? (
        <Section title="Needs you" count={items.length}>
          {items.map((item, i) => (
            <View key={item.key}>
              {i > 0 ? <Divider inset={spacing.lg} /> : null}
              <AttentionRow item={item} job={jobById[item.jobId]} onOpen={() => onSelect(jobById[item.jobId])} />
            </View>
          ))}
        </Section>
      ) : !loading && jobs.length ? (
        <View style={styles.pad}>
          <Text style={[typography.body, { color: colors.ok }]}>Nothing needs you right now.</Text>
        </View>
      ) : null}

      {moving.length ? (
        <Section title="On the road" count={moving.length}>
          <Rows jobs={moving} selectedId={selectedId} onSelect={onSelect} />
        </Section>
      ) : null}

      {groups.planned.length ? (
        <Section title="Not started" count={groups.planned.length}>
          <Rows jobs={groups.planned} selectedId={selectedId} onSelect={onSelect} />
        </Section>
      ) : null}

      {money_ && (toInvoice > 0 || money_.overdueCount > 0 || money_.outstanding > 0) ? (
        <Section title="Money">
          {toInvoice > 0 ? (
            <View style={styles.moneyRow}>
              <View style={styles.flex}>
                <Text style={[typography.bodyStrong, { color: colors.text }]}>
                  {toInvoice} {toInvoice === 1 ? 'delivery' : 'deliveries'} ready to invoice
                </Text>
                <Text style={[typography.caption, { color: colors.muted }]}>Paperwork attached. Invoice today, get paid sooner.</Text>
              </View>
              <Button label="Invoice" onPress={() => navigation.navigate('Invoices', { tab: 'toInvoice' })} />
            </View>
          ) : null}
          {money_.overdueCount > 0 ? (
            <View style={styles.moneyRow}>
              <View style={styles.flex}>
                <Text style={[typography.bodyStrong, { color: colors.warning }]}>{money(money_.overdue, currency)} overdue</Text>
                <Text style={[typography.caption, { color: colors.muted }]}>
                  {money_.overdueCount} {money_.overdueCount === 1 ? 'invoice' : 'invoices'} past the due date
                </Text>
              </View>
              <Button label="Chase" variant="secondary" onPress={() => navigation.navigate('Invoices', { tab: 'unpaid' })} />
            </View>
          ) : null}
          {money_.outstanding > 0 ? (
            <Text style={[typography.caption, styles.pad, { color: colors.muted }]}>
              {money(money_.outstanding, currency)} waiting for payment in total · {money(money_.paidLast30Days, currency)} paid in the last 30 days
            </Text>
          ) : null}
        </Section>
      ) : null}

      {groups.delivered.length ? (
        <Section
          title="Delivered today"
          count={groups.delivered.length}
          right={(
            <Pressable onPress={() => setShowDelivered((v) => !v)} accessibilityRole="button" hitSlop={8}>
              <Text style={[typography.label, { color: colors.accent }]}>{showDelivered ? 'Hide' : 'Show'}</Text>
            </Pressable>
          )}
        >
          {showDelivered ? <Rows jobs={groups.delivered} selectedId={selectedId} onSelect={onSelect} /> : null}
        </Section>
      ) : null}

      {!loading && jobs.length === 0 && !showChecklist ? (
        <View style={styles.pad}>
          <Text style={[typography.bodyStrong, { color: colors.text }]}>No deliveries today</Text>
          <Button label="Create a job" onPress={() => navigation.navigate('Jobs', { create: true })} />
        </View>
      ) : null}
      {jobs.length > 0 || showChecklist ? (
        <View style={styles.pad}>
          <Button label="New job" variant="secondary" onPress={() => navigation.navigate('Jobs', { create: true })} />
        </View>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  header: { paddingHorizontal: spacing.lg, paddingTop: spacing.lg, gap: 2 },
  counts: { flexDirection: 'row', paddingHorizontal: spacing.lg, paddingVertical: spacing.md, gap: spacing.md },
  count: { flex: 1, gap: 0 },
  countValue: { fontSize: 28, fontWeight: '700', letterSpacing: -0.5, fontVariant: ['tabular-nums'] },
  sectionHead: { flexDirection: 'row', alignItems: 'center', paddingHorizontal: spacing.lg, paddingTop: spacing.lg, paddingBottom: spacing.xs },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  rowRight: { alignItems: 'flex-end', maxWidth: 140 },
  attention: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.md, paddingRight: spacing.lg, paddingVertical: spacing.md },
  bar: { width: 3, alignSelf: 'stretch', borderRadius: 2, marginLeft: spacing.sm },
  attentionBody: { gap: 2 },
  attentionAction: { paddingTop: 2 },
  moneyRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.sm },
  pad: { paddingHorizontal: spacing.lg, paddingVertical: spacing.md, gap: spacing.sm },
  step: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, paddingVertical: 6 },
});
