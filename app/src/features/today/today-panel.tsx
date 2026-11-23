// "Today": three numbers that matter (moving, needs you, to invoice), what needs someone now in plain words with
// one next step each, the trucks on the road, and the money waiting. New companies see a short setup checklist.
import { router } from 'expo-router';
import { useMemo, useState } from 'react';
import { Text, View } from 'react-native';
import type { Job } from '@/api/types';
import { Button, Divider, ErrorNote, TextAction } from '@/components/ui';
import { money } from '@/features/invoices/invoice-format';
import { groupJobsForToday, isPositionStale } from '@/features/jobs/job-format';
import { useBillingProfile, useInvoiceOverview, useJobOptions } from '@/features/office-queries';
import { useTheme } from '@/theme/theme-provider';
import { spacing, typography } from '@/theme/tokens';
import { AttentionRow, Count, JobRows, Section, SetupChecklist, todayStyles as s } from './today-parts';
import { attentionItems, movingJobs, setupSteps } from './today-rules';

interface Props {
  jobs: Job[];
  loading: boolean;
  error: Error | null;
  onRetry: () => void;
  selectedId: string | null;
  onSelect: (job: Job) => void;
}

export function TodayPanel({ jobs, loading, error, onRetry, selectedId, onSelect }: Props) {
  const { colors } = useTheme();
  const overview = useInvoiceOverview().data;
  const profile = useBillingProfile().data;
  const options = useJobOptions();
  const [showDelivered, setShowDelivered] = useState(false);

  const groups = useMemo(() => groupJobsForToday(jobs), [jobs]);
  const items = attentionItems(jobs, { freeWaitingMinutes: profile?.freeWaitingMinutes ?? 120 });
  const moving = movingJobs(jobs);
  const jobById = new Map(jobs.map((j) => [j.id, j]));
  const toInvoice = overview?.toInvoice.length ?? 0;
  const currency = overview?.currency ?? 'EUR';
  const steps = setupSteps({
    vehicles: options.data?.vehicles.length ?? 0,
    drivers: options.data?.drivers.length ?? 0,
    jobs,
    invoiceSettingsDone: !!profile?.isComplete,
  });
  const fresh = !loading && jobs.length === 0;
  const showChecklist = options.isSuccess && (fresh || (jobs.length < 3 && steps.some((step) => !step.done)));
  const silent = moving.filter((j) => isPositionStale(j)).length;
  const today = new Date().toLocaleDateString([], { weekday: 'long', day: 'numeric', month: 'long' });
  const newJob = () => router.navigate({ pathname: '/jobs', params: { create: '1' } });
  const toInvoices = (tab: string) => router.navigate({ pathname: '/invoices', params: { tab } });

  return (
    <View>
      <View style={s.header}>
        <Text style={[typography.title, { color: colors.text }]}>Today</Text>
        <Text style={[typography.caption, { color: colors.muted }]}>{today}</Text>
      </View>
      {error ? (
        <View style={s.pad}>
          <ErrorNote message={error.message} onRetry={onRetry} />
        </View>
      ) : null}

      <View style={s.counts}>
        <Count label="Moving" value={moving.length} tone={silent ? 'warning' : null} />
        <Count
          label="Need you"
          value={items.length}
          tone={
            items.some((i) => i.tone === 'critical')
              ? 'critical'
              : items.some((i) => i.tone === 'warning')
                ? 'warning'
                : null
          }
        />
        <Count label="To invoice" value={toInvoice} onPress={() => toInvoices('toInvoice')} />
      </View>
      <Divider />

      {showChecklist ? (
        <>
          <SetupChecklist steps={steps} />
          <Divider />
        </>
      ) : null}

      {items.length ? (
        <Section title="Needs you" count={items.length}>
          {items.map((item, i) => (
            <View key={item.key}>
              {i > 0 ? <Divider inset={spacing.lg} /> : null}
              <AttentionRow
                item={item}
                job={jobById.get(item.jobId)}
                onOpen={() => {
                  const job = jobById.get(item.jobId);
                  if (job) onSelect(job);
                }}
              />
            </View>
          ))}
        </Section>
      ) : !loading && jobs.length ? (
        <View style={s.pad}>
          <Text style={[typography.body, { color: colors.ok }]}>Nothing needs you right now.</Text>
        </View>
      ) : null}

      {moving.length ? (
        <Section title="On the road" count={moving.length}>
          <JobRows jobs={moving} selectedId={selectedId} onSelect={onSelect} />
        </Section>
      ) : null}

      {groups.planned.length ? (
        <Section title="Not started" count={groups.planned.length}>
          <JobRows jobs={groups.planned} selectedId={selectedId} onSelect={onSelect} />
        </Section>
      ) : null}

      {overview && (toInvoice > 0 || overview.overdueCount > 0 || overview.outstanding > 0) ? (
        <Section title="Money">
          {toInvoice > 0 ? (
            <View style={s.moneyRow}>
              <View style={s.flex}>
                <Text style={[typography.bodyStrong, { color: colors.text }]}>
                  {toInvoice} {toInvoice === 1 ? 'delivery' : 'deliveries'} ready to invoice
                </Text>
                <Text style={[typography.caption, { color: colors.muted }]}>
                  Paperwork attached. Invoice today, get paid sooner.
                </Text>
              </View>
              <Button label="Invoice" onPress={() => toInvoices('toInvoice')} />
            </View>
          ) : null}
          {overview.overdueCount > 0 ? (
            <View style={s.moneyRow}>
              <View style={s.flex}>
                <Text style={[typography.bodyStrong, { color: colors.warning }]}>
                  {money(overview.overdue, currency)} overdue
                </Text>
                <Text style={[typography.caption, { color: colors.muted }]}>
                  {overview.overdueCount} {overview.overdueCount === 1 ? 'invoice' : 'invoices'} past the due date
                </Text>
              </View>
              <Button label="Chase" variant="secondary" onPress={() => toInvoices('unpaid')} />
            </View>
          ) : null}
          {overview.outstanding > 0 ? (
            <Text style={[typography.caption, s.pad, { color: colors.muted }]}>
              {money(overview.outstanding, currency)} waiting for payment in total ·{' '}
              {money(overview.paidLast30Days, currency)} paid in the last 30 days
            </Text>
          ) : null}
        </Section>
      ) : null}

      {groups.delivered.length ? (
        <Section
          title="Delivered today"
          count={groups.delivered.length}
          right={<TextAction label={showDelivered ? 'Hide' : 'Show'} onPress={() => setShowDelivered((v) => !v)} />}
        >
          {showDelivered ? <JobRows jobs={groups.delivered} selectedId={selectedId} onSelect={onSelect} /> : null}
        </Section>
      ) : null}

      <View style={s.pad}>
        {fresh && !showChecklist ? (
          <Text style={[typography.bodyStrong, { color: colors.text }]}>No deliveries today</Text>
        ) : null}
        <Button label={fresh ? 'Create a job' : 'New job'} variant={fresh ? 'primary' : 'secondary'} onPress={newJob} />
      </View>
    </View>
  );
}
