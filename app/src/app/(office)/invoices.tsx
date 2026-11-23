// Invoices: turn delivered jobs into invoices with their paperwork, send them the same day, see who owes what.
import { router, useLocalSearchParams } from 'expo-router';
import { useEffect, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { Button, Card, EmptyNote, ErrorNote, Page, Segmented } from '@/components/ui';
import { BillingSettingsSheet } from '@/features/invoices/billing-settings-sheet';
import { CreateInvoiceSheet, day } from '@/features/invoices/create-invoice-sheet';
import { invoiceStatusMeta, money } from '@/features/invoices/invoice-format';
import { InvoiceSheet } from '@/features/invoices/invoice-sheet';
import { jobTitle } from '@/features/jobs/job-format';
import { useBillingProfile, useInvoiceOverview } from '@/features/office-queries';
import { useSession } from '@/lib/use-session';
import { useTheme } from '@/theme/theme-provider';
import { spacing, toneColors, typography } from '@/theme/tokens';

type Tab = 'toInvoice' | 'unpaid' | 'paid';

function Figure({ label, value, critical }: { label: string; value: string; critical?: boolean }) {
  const { colors } = useTheme();
  return (
    <View style={styles.figure}>
      <Text style={[typography.metric, { color: critical ? colors.critical : colors.text }]} numberOfLines={1}>
        {value}
      </Text>
      <Text style={[typography.caption, { color: colors.muted }]}>{label}</Text>
    </View>
  );
}

export default function Invoices() {
  const { colors } = useTheme();
  const isOwner = useSession()?.account.role === 'Owner';
  // Where you are lives in the URL, so other screens (and alerts) link straight here:
  // ?tab=unpaid, ?open=<invoice id>, ?job=<delivered job to invoice>.
  const params = useLocalSearchParams<{ tab?: Tab; open?: string; job?: string }>();
  const overview = useInvoiceOverview();
  const profile = useBillingProfile();
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [notice, setNotice] = useState<string | null>(null);
  const tab: Tab = params.tab ?? 'toInvoice';
  const openId = params.open ?? null;
  const setTab = (next: Tab) => router.setParams({ tab: next });
  const setOpenId = (id: string | null) => router.setParams({ open: id ?? undefined });
  const startInvoice = (jobId: string | null) => router.setParams({ job: jobId ?? undefined, tab: 'toInvoice' });
  const pending = params.job ? (overview.data?.toInvoice.find((t) => t.job.id === params.job) ?? null) : null;
  // Invoicing needs the company's details first; once they're saved, the invoice form opens by itself.
  const needsSettings = !!pending && profile.data?.isComplete === false;
  const creating = pending && profile.data?.isComplete ? pending : null;

  useEffect(() => {
    if (!notice) return;
    const t = setTimeout(() => setNotice(null), 6000);
    return () => clearTimeout(t);
  }, [notice]);

  const data = overview.data;
  const currency = data?.currency ?? 'EUR';
  const invoices = data?.invoices ?? [];
  const unpaid = invoices
    .filter((i) => i.status !== 'Paid')
    .sort(
      (a, b) =>
        Number(b.isOverdue) - Number(a.isOverdue) ||
        new Date(a.dueDateUtc).getTime() - new Date(b.dueDateUtc).getTime(),
    );
  const paid = invoices.filter((i) => i.status === 'Paid');
  const toInvoice = data?.toInvoice ?? [];
  const list = tab === 'unpaid' ? unpaid : paid;

  return (
    <Page
      title="Invoices"
      actions={
        isOwner ? <Button label="Invoice settings" variant="secondary" onPress={() => setSettingsOpen(true)} /> : null
      }
    >
      {profile.data && !profile.data.isComplete ? (
        <Card>
          <Text style={[typography.title, { color: colors.text }]}>Set up invoicing</Text>
          <Text style={[typography.body, { color: colors.textSecondary }]}>
            Add your company name, address and bank account once. Then every delivered job becomes an invoice in a
            minute, with the proof of delivery attached.
          </Text>
          {isOwner ? (
            <View>
              <Button label="Add company details" onPress={() => setSettingsOpen(true)} />
            </View>
          ) : (
            <Text style={[typography.caption, { color: colors.muted }]}>
              An owner of the company account can fill these in.
            </Text>
          )}
        </Card>
      ) : null}

      <View style={styles.figures}>
        <Figure label="Waiting for payment" value={money(data?.outstanding ?? 0, currency)} />
        <Figure
          label={`Overdue${data?.overdueCount ? ` (${data.overdueCount})` : ''}`}
          value={money(data?.overdue ?? 0, currency)}
          critical={!!data?.overdueCount}
        />
        <Figure label="Paid in the last 30 days" value={money(data?.paidLast30Days ?? 0, currency)} />
      </View>

      <Segmented
        options={[
          { key: 'toInvoice', label: 'To invoice', count: toInvoice.length },
          { key: 'unpaid', label: 'Unpaid', count: unpaid.length },
          { key: 'paid', label: 'Paid', count: paid.length },
        ]}
        value={tab}
        onChange={setTab}
      />
      {notice ? <Text style={[typography.label, { color: colors.ok }]}>{notice}</Text> : null}
      {overview.error ? <ErrorNote message={overview.error.message} onRetry={() => void overview.refetch()} /> : null}

      {!data ? null : tab === 'toInvoice' ? (
        toInvoice.length === 0 ? (
          <EmptyNote
            title="Nothing to invoice"
            body="Delivered jobs appear here, with their proof of delivery, ready to invoice."
          />
        ) : (
          <Card padded={false}>
            {toInvoice.map((t, i) => (
              <View
                key={t.job.id}
                style={[
                  styles.row,
                  i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border },
                ]}
              >
                <View style={styles.flex}>
                  <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>
                    {jobTitle(t.job)}
                  </Text>
                  <Text style={[typography.caption, { color: colors.muted }]} numberOfLines={1}>
                    Delivered {day(t.job.deliveredAtUtc)} ·{' '}
                    {[t.job.reference && `#${t.job.reference}`, t.job.vehicleName].filter(Boolean).join(' · ')}
                    {t.suggestedLines.length ? ' · waiting time to charge' : ''}
                  </Text>
                </View>
                <Button label="Create invoice" variant="secondary" onPress={() => startInvoice(t.job.id)} />
              </View>
            ))}
          </Card>
        )
      ) : list.length === 0 ? (
        <EmptyNote title={tab === 'paid' ? 'No paid invoices yet' : 'Nothing waiting for payment'} />
      ) : (
        <Card padded={false}>
          {list.map((inv, i) => {
            const st = invoiceStatusMeta(inv);
            return (
              <View
                key={inv.id}
                style={i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }}
              >
                <Card
                  padded={false}
                  style={styles.flat}
                  onPress={() => setOpenId(inv.id)}
                  accessibilityLabel={`Invoice ${inv.number} for ${inv.customerName}`}
                >
                  <View style={styles.row}>
                    <View style={styles.flex}>
                      <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>
                        {inv.customerName}
                      </Text>
                      <Text style={[typography.caption, { color: colors.muted }]} numberOfLines={1}>
                        {inv.number} · issued {day(inv.issueDateUtc)}
                        {inv.remindersSent
                          ? ` · ${inv.remindersSent} reminder${inv.remindersSent === 1 ? '' : 's'}`
                          : ''}
                      </Text>
                    </View>
                    <View style={styles.right}>
                      <Text style={[typography.number, { color: colors.text }]}>{money(inv.total, inv.currency)}</Text>
                      <Text
                        style={[
                          typography.caption,
                          { color: st.tone === 'neutral' ? colors.muted : toneColors(colors, st.tone).fg },
                        ]}
                      >
                        {st.label}
                      </Text>
                    </View>
                  </View>
                </Card>
              </View>
            );
          })}
        </Card>
      )}

      {(settingsOpen || needsSettings) && profile.data ? (
        <BillingSettingsSheet
          profile={profile.data}
          onClose={() => {
            setSettingsOpen(false);
            startInvoice(null);
          }}
          onSaved={() => {
            setSettingsOpen(false);
            setNotice('Invoice settings saved');
          }}
        />
      ) : null}
      {creating ? (
        <CreateInvoiceSheet
          key={creating.job.id}
          item={creating}
          profile={profile.data}
          onClose={() => startInvoice(null)}
          onCreated={(invoice, text) => {
            setNotice(`Invoice ${invoice.summary.number} created.${text ? ` ${text}.` : ''}`);
            router.setParams({ job: undefined, tab: 'unpaid' });
          }}
        />
      ) : null}
      <InvoiceSheet invoiceId={openId} profile={profile.data} onClose={() => setOpenId(null)} />
    </Page>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  figures: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xl },
  figure: { minWidth: 160, flex: 1 },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
  },
  right: { alignItems: 'flex-end', gap: 2 },
  flat: { borderWidth: 0, borderRadius: 0 },
});
