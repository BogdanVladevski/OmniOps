// The job board: create delivery jobs, see where each one is, read the proof, send the customer a link.
import { router, useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { Button, Card, EmptyNote, ErrorNote, Page, Segmented, Status } from '@/components/ui';
import { isPositionStale, jobStatusMeta, jobTitle, temperatureStatus } from '@/features/jobs/job-format';
import { JobSheet } from '@/features/jobs/job-sheet';
import { NewJobSheet } from '@/features/jobs/new-job-sheet';
import { useJobs, type JobScope } from '@/features/office-queries';
import { timeAgo, clock } from '@/lib/time';
import { useTheme } from '@/theme/theme-provider';
import { spacing, toneColors, typography } from '@/theme/tokens';

const SCOPES: { key: JobScope; label: string }[] = [
  { key: 'Active', label: 'Today' },
  { key: 'Delivered', label: 'Delivered' },
  { key: 'All', label: 'All' },
];

export default function Jobs() {
  const { colors } = useTheme();
  // Other screens open a job (?open=id) or the new-job form (?create=1) by link.
  const params = useLocalSearchParams<{ open?: string; create?: string }>();
  const openId = params.open ?? null;
  const creating = params.create === '1';
  const setOpenId = (id: string | null) => router.setParams({ open: id ?? undefined, create: undefined });
  const setCreating = (on: boolean) => router.setParams({ create: on ? '1' : undefined });
  const [scope, setScope] = useState<JobScope>('Active');
  const [notice, setNotice] = useState<string | null>(null);
  const jobs = useJobs(scope);
  const list = jobs.data ?? [];

  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(() => setNotice(null), 5000);
    return () => clearTimeout(t);
  }, [notice]);

  const count = (status: string) => list.filter((j) => j.status === status).length;
  const summary =
    scope === 'Active'
      ? `${count('InProgress')} on the way · ${count('Planned')} not started · ${count('Delivered')} delivered today`
      : null;

  return (
    <Page title="Jobs" actions={<Button label="New job" onPress={() => setCreating(true)} />}>
      <View style={styles.toolbar}>
        <Segmented options={SCOPES} value={scope} onChange={setScope} />
        {summary ? <Text style={[typography.label, { color: colors.textSecondary }]}>{summary}</Text> : null}
      </View>
      {notice ? <Text style={[typography.label, { color: colors.ok }]}>{notice}</Text> : null}
      {jobs.error ? <ErrorNote message={jobs.error.message} onRetry={() => void jobs.refetch()} /> : null}

      {jobs.isSuccess && list.length === 0 ? (
        <EmptyNote
          title={scope === 'Delivered' ? 'Nothing delivered yet' : 'No jobs yet'}
          body="Create a job for each delivery. The driver sees it on their phone, and you can send the customer a live tracking link."
        />
      ) : list.length ? (
        <Card padded={false}>
          {list.map((job, i) => {
            const status = jobStatusMeta(job.status);
            const stale = isPositionStale(job);
            const temp = temperatureStatus(job);
            const progress =
              job.status === 'InProgress'
                ? job.lastPositionAtUtc
                  ? stale
                    ? `Not reporting since ${timeAgo(job.lastPositionAtUtc)}`
                    : `Last seen ${timeAgo(job.lastPositionAtUtc)}`
                  : 'Waiting for the first location'
                : job.status === 'Delivered' && job.deliveredAtUtc
                  ? `${clock(job.deliveredAtUtc)}${job.recipientName ? ` · ${job.recipientName}` : ''}`
                  : '';
            return (
              <View
                key={job.id}
                style={i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }}
              >
                <Card
                  padded={false}
                  style={styles.rowCard}
                  onPress={() => setOpenId(job.id)}
                  accessibilityLabel={`Open job for ${jobTitle(job)}`}
                >
                  <View style={styles.row}>
                    <View style={styles.flex}>
                      <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>
                        {jobTitle(job)}
                      </Text>
                      <Text style={[typography.caption, { color: colors.textSecondary }]} numberOfLines={1}>
                        {[job.destination, job.vehicleName, job.driverName, job.reference && `#${job.reference}`]
                          .filter(Boolean)
                          .join(' · ')}
                      </Text>
                    </View>
                    <View style={styles.right}>
                      <Status label={status.label} tone={stale ? 'warning' : status.tone} />
                      {temp && (temp.tone === 'critical' || temp.tone === 'warning') ? (
                        <Text
                          style={[typography.caption, { color: toneColors(colors, temp.tone).fg }]}
                          numberOfLines={1}
                        >
                          {temp.label}
                        </Text>
                      ) : null}
                      {progress ? (
                        <Text
                          style={[typography.caption, { color: stale ? colors.warning : colors.muted }]}
                          numberOfLines={1}
                        >
                          {progress}
                        </Text>
                      ) : null}
                    </View>
                  </View>
                </Card>
              </View>
            );
          })}
        </Card>
      ) : null}

      <NewJobSheet
        visible={creating}
        onClose={() => setCreating(false)}
        onCreated={(job, foundOnMap) => {
          setScope('Active');
          setNotice(
            foundOnMap
              ? 'Job created. Send the customer their tracking link from here.'
              : "Job created. The address wasn't found on the map, so the customer page shows no arrival estimate.",
          );
          setOpenId(job.id);
        }}
      />
      <JobSheet jobId={openId} onClose={() => setOpenId(null)} onNotice={setNotice} />
    </Page>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  toolbar: { gap: spacing.sm },
  rowCard: { borderWidth: 0, borderRadius: 0 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
  },
  right: { alignItems: 'flex-end', gap: 2, maxWidth: '45%' },
});
