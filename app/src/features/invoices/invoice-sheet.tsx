// One invoice: what it says, where it stands, reminders, and the next step (send, share, mark paid).
import { useMutation, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { Linking, Platform, StyleSheet, Text, View } from 'react-native';
import { API_URL, api, unwrap } from '@/api/client';
import { keys, queryClient } from '@/api/query-client';
import type { BillingProfile, InvoiceDetail } from '@/api/types';
import { Button, Card, Sheet, Status } from '@/components/ui';
import { FormError } from '@/features/accounts/auth-frame';
import { useTheme } from '@/theme/theme-provider';
import { spacing, typography } from '@/theme/tokens';
import { day, shareInvoiceLink } from './create-invoice-sheet';
import { invoiceStatusMeta, money, reminderText } from './invoice-format';

function Row({ label, value }: { label: string; value: string | null | undefined }) {
  const { colors } = useTheme();
  if (!value) return null;
  return (
    <View style={styles.detailRow}>
      <Text style={[typography.label, { color: colors.textSecondary }]}>{label}</Text>
      <Text style={[typography.label, styles.detailValue, { color: colors.text }]}>{value}</Text>
    </View>
  );
}

export function InvoiceSheet({
  invoiceId,
  profile,
  onClose,
}: {
  invoiceId: string | null;
  profile: BillingProfile | undefined;
  onClose: () => void;
}) {
  const { colors } = useTheme();
  const [notice, setNotice] = useState<string | null>(null);
  const detail = useQuery({
    queryKey: keys.invoice(invoiceId ?? ''),
    queryFn: () => unwrap(api.GET('/api/v1/invoices/{invoiceId}', { params: { path: { invoiceId: invoiceId! } } })),
    enabled: !!invoiceId,
  });

  const changed = (updated: InvoiceDetail, text: string | null) => {
    queryClient.setQueryData(keys.invoice(updated.summary.id), updated);
    void queryClient.invalidateQueries({ queryKey: keys.invoices });
    setNotice(text);
  };
  const send = useMutation({
    mutationFn: (email: boolean) =>
      unwrap(
        api.POST('/api/v1/invoices/{invoiceId}/send', { params: { path: { invoiceId: invoiceId! } }, body: { email } }),
      ),
  });
  const paid = useMutation({
    mutationFn: () =>
      unwrap(
        api.POST('/api/v1/invoices/{invoiceId}/paid', {
          params: { path: { invoiceId: invoiceId! } },
          body: { paidAt: null },
        }),
      ),
    onSuccess: (d) => changed(d, 'Marked as paid. Reminders stopped.'),
  });

  const d = detail.data;
  const s = d?.summary;
  const close = () => {
    setNotice(null);
    onClose();
  };

  const share = async () => {
    if (!s) return;
    const text = await shareInvoiceLink(s);
    // Sharing it yourself counts as sending: reminders start from here.
    if (s.status === 'Issued') changed(await send.mutateAsync(false), text);
    else setNotice(text);
  };
  const email = async () => {
    const updated = await send.mutateAsync(true);
    changed(
      updated,
      updated.emailed ? `Emailed to ${s?.customerEmail}` : 'The email could not be sent. Share the link instead.',
    );
  };

  return (
    <Sheet visible={!!invoiceId} onClose={close} width={640}>
      {!s || !d ? (
        <Text style={[typography.body, { color: detail.error ? colors.critical : colors.muted }]}>
          {detail.error?.message ?? 'Loading…'}
        </Text>
      ) : (
        <>
          <View style={styles.block}>
            <Status {...invoiceStatusMeta(s)} />
            <Text style={[typography.headline, { color: colors.text }]}>{money(s.total, s.currency)}</Text>
            <Text style={[typography.body, { color: colors.textSecondary }]}>
              Invoice {s.number} · {s.customerName}
            </Text>
          </View>
          <Card padded={false}>
            <Row label="Issued" value={day(s.issueDateUtc)} />
            <Row label="Due" value={day(s.dueDateUtc)} />
            <Row label="Sent" value={s.sentAtUtc ? day(s.sentAtUtc) : 'Not yet'} />
            <Row label="Paid" value={s.paidAtUtc ? day(s.paidAtUtc) : null} />
            <Row label="Customer email" value={s.customerEmail} />
            <Row label="Subtotal" value={money(d.subtotal, s.currency)} />
            <Row label="VAT" value={money(d.vatAmount, s.currency)} />
          </Card>
          {d.lines.map((l, i) => (
            <Text key={i} style={[typography.caption, { color: colors.muted }]}>
              {l.description}: {money(l.netAmount, s.currency)} + {l.vatRatePercent}% VAT
            </Text>
          ))}
          {d.reminders.length ? (
            <View style={styles.block}>
              <Text style={[typography.label, { color: colors.textSecondary }]}>Reminders</Text>
              {d.reminders.map((r) => (
                <Text
                  key={r.kind}
                  style={[typography.caption, { color: r.emailed ? colors.textSecondary : colors.warning }]}
                >
                  {reminderText(r)}
                </Text>
              ))}
            </View>
          ) : s.status === 'Sent' ? (
            <Text style={[typography.caption, { color: colors.muted }]}>
              Reminders go out 3 days before the due date, on the day, and 7 and 14 days after
              {s.customerEmail && profile?.emailSendingEnabled ? '.' : ' (listed here for you to send).'}
            </Text>
          ) : null}
          {notice ? <Text style={[typography.label, { color: colors.ok }]}>{notice}</Text> : null}
          <FormError message={send.error?.message ?? paid.error?.message} />
          <View style={styles.row}>
            <Button
              label="Open invoice"
              variant="secondary"
              onPress={() => void Linking.openURL(`${API_URL}${s.publicPath}`)}
              style={styles.flex}
            />
            <Button
              label={Platform.OS === 'web' ? 'Copy link' : 'Share link'}
              variant="secondary"
              onPress={() => void share()}
              style={styles.flex}
            />
          </View>
          {s.status !== 'Paid' ? (
            <View style={styles.row}>
              {profile?.emailSendingEnabled && s.customerEmail ? (
                <Button
                  label={s.status === 'Issued' ? 'Email to customer' : 'Email again'}
                  variant="secondary"
                  onPress={() => void email()}
                  busy={send.isPending}
                  style={styles.flex}
                />
              ) : null}
              <Button label="Mark as paid" onPress={() => paid.mutate()} busy={paid.isPending} style={styles.flex} />
            </View>
          ) : null}
        </>
      )}
    </Sheet>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  block: { gap: spacing.xs },
  row: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
  detailRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: spacing.lg,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.sm,
  },
  detailValue: { flex: 1, textAlign: 'right' },
});
