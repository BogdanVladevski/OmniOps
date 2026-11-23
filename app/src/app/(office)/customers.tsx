// Customers: who the company delivers for, what's moving for each, what they owe, and their portal links.
import { router, useLocalSearchParams } from 'expo-router';
import { useMemo, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { Card, Divider, EmptyNote, ErrorNote, Field, Page, Status } from '@/components/ui';
import { useCustomers } from '@/features/customers/customer-queries';
import { CustomerSheet } from '@/features/customers/customer-sheet';
import { money } from '@/features/invoices/invoice-format';
import { timeAgo } from '@/lib/time';
import { useTheme } from '@/theme/theme-provider';
import { spacing, typography } from '@/theme/tokens';

export default function Customers() {
  const { colors } = useTheme();
  const params = useLocalSearchParams<{ open?: string }>();
  const openId = params.open ?? null;
  const customers = useCustomers();
  const [query, setQuery] = useState('');

  const list = useMemo(() => {
    const q = query.trim().toLowerCase();
    const all = customers.data ?? [];
    const shown = q ? all.filter((c) => [c.name, c.email, c.phone].some((v) => v?.toLowerCase().includes(q))) : all;
    // Most active first: what's moving, then who owes money, then the rest by name.
    return [...shown].sort(
      (a, b) => b.activeJobs - a.activeJobs || b.outstanding - a.outstanding || a.name.localeCompare(b.name),
    );
  }, [customers.data, query]);

  return (
    <Page title="Customers">
      {(customers.data?.length ?? 0) > 6 ? (
        <Field
          placeholder="Find a customer"
          value={query}
          onChangeText={setQuery}
          accessibilityLabel="Find a customer"
        />
      ) : null}
      {customers.error ? (
        <ErrorNote message={customers.error.message} onRetry={() => void customers.refetch()} />
      ) : null}
      {customers.isSuccess && customers.data.length === 0 ? (
        <EmptyNote
          title="No customers yet"
          body="Customers appear here when you create a job for them. Give each one a portal link: every delivery live, proof and invoices in one place."
        />
      ) : list.length ? (
        <Card padded={false}>
          {list.map((c, i) => (
            <View key={c.id}>
              {i > 0 ? <Divider /> : null}
              <Card
                padded={false}
                style={styles.flat}
                onPress={() => router.setParams({ open: c.id })}
                accessibilityLabel={`Open ${c.name}`}
              >
                <View style={styles.row}>
                  <View style={styles.flex}>
                    <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>
                      {c.name}
                    </Text>
                    <Text style={[typography.caption, { color: colors.muted }]} numberOfLines={1}>
                      {[
                        c.activeJobs ? `${c.activeJobs} active` : null,
                        `${c.deliveredLast30Days} delivered in 30 days`,
                        c.lastJobAtUtc ? `last job ${timeAgo(c.lastJobAtUtc)}` : null,
                      ]
                        .filter(Boolean)
                        .join(' · ')}
                    </Text>
                  </View>
                  <View style={styles.right}>
                    {c.overdue > 0 ? (
                      <Text style={[typography.number, { color: colors.critical }]}>
                        {money(c.overdue, c.currency)} overdue
                      </Text>
                    ) : c.outstanding > 0 ? (
                      <Text style={[typography.number, { color: colors.text }]}>
                        {money(c.outstanding, c.currency)}
                      </Text>
                    ) : null}
                    <Status
                      label={c.portalUrl ? 'Portal on' : 'No portal link'}
                      tone={c.portalUrl ? 'ok' : 'neutral'}
                    />
                  </View>
                </View>
              </Card>
            </View>
          ))}
        </Card>
      ) : customers.isSuccess ? (
        <Text style={[typography.body, { color: colors.muted }]}>No customer matches “{query}”.</Text>
      ) : null}
      <CustomerSheet customerId={openId} onClose={() => router.setParams({ open: undefined })} />
    </Page>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  flat: { borderWidth: 0, borderRadius: 0 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
  },
  right: { alignItems: 'flex-end', gap: 2 },
});
