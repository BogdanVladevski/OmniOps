// Settings: your account, the company and team (owners), appearance, demo data, and the audit trail.
import { useMutation, useQuery } from '@tanstack/react-query';
import { StyleSheet, Text, View } from 'react-native';
import { api, unwrap } from '@/api/client';
import { keys, queryClient } from '@/api/query-client';
import { Button, Card, Divider, EmptyNote, Page, SectionTitle, Segmented } from '@/components/ui';
import { AccountCard, CompanyCard, TeamCard } from '@/features/accounts/account-cards';
import { describeAudit } from '@/features/audit/audit-format';
import { useDemoStatus } from '@/features/office-queries';
import { useLayout } from '@/hooks/use-layout';
import { useSession } from '@/lib/use-session';
import { timeAgo } from '@/lib/time';
import { useTheme, type ThemeMode } from '@/theme/theme-provider';
import { spacing, typography } from '@/theme/tokens';

/** Demo trucks only move when the demo is refreshed; this puts them where they would be now. Demo company only. */
function DemoDayCard() {
  const { colors } = useTheme();
  const status = useDemoStatus();
  const load = useMutation({
    mutationFn: () => unwrap(api.POST('/api/v1/demo/day')),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: keys.allJobs });
      void queryClient.invalidateQueries({ queryKey: keys.invoices });
    },
  });
  if (!status.data?.isDemoCompany) return null;
  return (
    <Card>
      <Text style={[typography.bodyStrong, { color: colors.text }]}>Demo data</Text>
      <Text style={[typography.body, { color: colors.textSecondary }]}>
        Loads a demo delivery day, or moves the demo trucks to where they would be by now.
      </Text>
      <View>
        <Button
          label="Load or refresh demo day"
          variant="secondary"
          onPress={() => load.mutate()}
          busy={load.isPending}
        />
      </View>
      {load.data || load.error ? (
        <Text style={[typography.caption, { color: colors.muted }]}>{load.data?.message ?? load.error?.message}</Text>
      ) : null}
    </Card>
  );
}

function AuditTrail() {
  const { colors } = useTheme();
  const audit = useQuery({
    queryKey: keys.audit,
    queryFn: () => unwrap(api.GET('/api/v1/audit', { params: { query: { limit: 50 } } })),
  });
  if (audit.data?.length === 0)
    return (
      <EmptyNote title="Nothing recorded yet" body="Jobs, deliveries, invoices and team changes are recorded here." />
    );
  return (
    <Card padded={false}>
      {(audit.data ?? []).map((entry, i) => {
        const text = describeAudit(entry);
        return (
          <View key={entry.id}>
            {i > 0 ? <Divider /> : null}
            <View style={styles.row}>
              <View style={styles.flex}>
                <Text style={[typography.bodyStrong, { color: colors.text }]}>{text.title}</Text>
                <Text style={[typography.caption, { color: colors.textSecondary }]} numberOfLines={2}>
                  {[entry.userName, text.detail].filter(Boolean).join(' · ')}
                </Text>
              </View>
              <Text style={[typography.caption, { color: colors.muted }]}>{timeAgo(entry.occurredAtUtc)}</Text>
            </View>
          </View>
        );
      })}
    </Card>
  );
}

export default function Settings() {
  const { mode, setMode } = useTheme();
  const { isWide } = useLayout();
  const owner = useSession()?.account.role === 'Owner';

  const left = (
    <View style={styles.column}>
      <SectionTitle title="Your account" />
      <AccountCard />
      {owner ? (
        <>
          <SectionTitle title="Company" />
          <CompanyCard />
          <SectionTitle title="Team" />
          <TeamCard />
        </>
      ) : null}
      <SectionTitle title="Appearance" />
      <Card>
        <Segmented<ThemeMode>
          options={[
            { key: 'system', label: 'Automatic' },
            { key: 'light', label: 'Light' },
            { key: 'dark', label: 'Dark' },
          ]}
          value={mode}
          onChange={setMode}
        />
      </Card>
      <DemoDayCard />
    </View>
  );
  const right = owner ? (
    <View style={styles.column}>
      <SectionTitle title="History" />
      <AuditTrail />
    </View>
  ) : null;

  return (
    <Page title="Settings">
      {isWide ? (
        <View style={styles.columns}>
          {left}
          {right}
        </View>
      ) : (
        <>
          {left}
          {right}
        </>
      )}
    </Page>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  columns: { flexDirection: 'row', gap: spacing.xl, alignItems: 'flex-start' },
  column: { flex: 1, gap: spacing.md },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
  },
});
