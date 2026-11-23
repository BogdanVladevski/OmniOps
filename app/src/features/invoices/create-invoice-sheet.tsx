// A delivered job becomes an invoice: the proof of delivery is attached, waiting time is suggested as its own line.
import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { Platform, Pressable, StyleSheet, Text, View } from 'react-native';
import { API_URL, api, unwrap } from '@/api/client';
import { keys, queryClient } from '@/api/query-client';
import type { BillingProfile, InvoiceDetail, UninvoicedJob } from '@/api/types';
import { Button, Field, Sheet } from '@/components/ui';
import { FormError } from '@/features/accounts/auth-frame';
import { jobTitle } from '@/features/jobs/job-format';
import { shareLink } from '@/lib/share';
import { useTheme } from '@/theme/theme-provider';
import { radius, spacing, typography } from '@/theme/tokens';
import { lineTotals, money, parseAmount, round2 } from './invoice-format';

type Send = 'email' | 'manual' | null;

export const day = (iso: string | null | undefined) =>
  iso ? new Date(iso).toLocaleDateString([], { day: 'numeric', month: 'short', year: 'numeric' }) : '—';

export const shareInvoiceLink = (summary: { publicPath: string; number: string }) =>
  shareLink(`${API_URL}${summary.publicPath}`, `Invoice ${summary.number}:`);

const fromItem = (item: UninvoicedJob, profile: BillingProfile | undefined) => ({
  customerName: item.job.customerName ?? '',
  customerEmail: item.customerEmail ?? '',
  customerAddress: item.customerAddress ?? '',
  customerTaxId: item.customerTaxId ?? '',
  description: item.suggestedDescription,
  price: '',
  vat: String(profile?.defaultVatRatePercent ?? 0),
  terms: String(item.customerPaymentTermsDays ?? profile?.defaultPaymentTermsDays ?? 30),
  notes: '',
  // Waiting time and other priced extras, on by default; the office can untick any of them.
  extras: item.suggestedLines.map((l) => ({ ...l, include: true })),
});

/** Mounted while open (keyed by job): the form starts from the job's suggestions. */
export function CreateInvoiceSheet({
  item,
  profile,
  onClose,
  onCreated,
}: {
  item: UninvoicedJob;
  profile: BillingProfile | undefined;
  onClose: () => void;
  onCreated: (invoice: InvoiceDetail, notice: string | null) => void;
}) {
  const { colors } = useTheme();
  const [form, setForm] = useState(() => fromItem(item, profile));
  const set =
    (
      key:
        | 'customerName'
        | 'customerEmail'
        | 'customerAddress'
        | 'customerTaxId'
        | 'description'
        | 'price'
        | 'vat'
        | 'terms'
        | 'notes',
    ) =>
    (value: string) =>
      setForm((f) => ({ ...f, [key]: value }));

  const price = parseAmount(form.price);
  const vat = Number(form.vat.replace(',', '.'));
  const extras = form.extras.filter((x) => x.include);
  const totals = lineTotals([
    { quantity: 1, unitPrice: price ?? 0, vatRatePercent: vat || 0 },
    ...extras.map((x) => ({ quantity: x.quantity, unitPrice: x.unitPrice, vatRatePercent: vat || 0 })),
  ]);

  const create = useMutation({
    mutationFn: async (send: Send) => {
      const f = form;
      const invoice = await unwrap(
        api.POST('/api/v1/invoices', {
          body: {
            jobId: item.job.id,
            customerName: f.customerName.trim(),
            customerEmail: f.customerEmail.trim() || null,
            customerAddress: f.customerAddress.trim() || null,
            customerTaxId: f.customerTaxId.trim() || null,
            paymentTermsDays: parseInt(f.terms, 10) || 0,
            lines: [
              { description: f.description.trim(), quantity: 1, unitPrice: price!, vatRatePercent: vat },
              ...extras.map((x) => ({
                description: x.description,
                quantity: x.quantity,
                unitPrice: x.unitPrice,
                vatRatePercent: vat,
              })),
            ],
            notes: f.notes.trim() || null,
            send,
          },
        }),
      );
      // "Emailed" only when the server says it was.
      const notice =
        send === 'manual'
          ? await shareInvoiceLink(invoice.summary)
          : send === 'email'
            ? invoice.emailed
              ? `Emailed to ${f.customerEmail.trim()}`
              : 'Email could not be sent; share the link instead'
            : null;
      return { invoice, notice };
    },
    onSuccess: ({ invoice, notice }) => {
      void queryClient.invalidateQueries({ queryKey: keys.invoices });
      void queryClient.invalidateQueries({ queryKey: keys.allJobs });
      onCreated(invoice, notice);
    },
  });

  const currency = profile?.currency ?? 'EUR';
  const canEmail = !!profile?.emailSendingEnabled && form.customerEmail.trim() !== '';
  const valid =
    form.customerName.trim() !== '' &&
    form.description.trim() !== '' &&
    price != null &&
    vat >= 0 &&
    vat <= 50 &&
    !create.isPending;
  const photos = item.job.photoCount + item.job.pickupDocumentCount;

  return (
    <Sheet visible onClose={onClose} width={640}>
      <Text style={[typography.title, { color: colors.text }]}>Invoice for {jobTitle(item.job)}</Text>
      <Text style={[typography.caption, { color: colors.muted }]}>
        Delivered {day(item.job.deliveredAtUtc)}
        {item.job.recipientName ? ` · received by ${item.job.recipientName}` : ''} · {photos} photo
        {photos === 1 ? '' : 's'}
        {item.job.temperatureReadingCount ? ` · ${item.job.temperatureReadingCount} temperature readings` : ''} attached
        to the invoice
      </Text>
      <View style={styles.row}>
        <View style={styles.flex}>
          <Field label="Customer" value={form.customerName} onChangeText={set('customerName')} />
        </View>
        <View style={styles.flex}>
          <Field
            label="Customer email"
            value={form.customerEmail}
            onChangeText={set('customerEmail')}
            placeholder="For sending and reminders"
            keyboardType="email-address"
            autoCapitalize="none"
          />
        </View>
      </View>
      <View style={styles.row}>
        <View style={styles.flex}>
          <Field
            label="Billing address (optional)"
            value={form.customerAddress}
            onChangeText={set('customerAddress')}
          />
        </View>
        <View style={styles.flex}>
          <Field
            label="Customer tax number (optional)"
            value={form.customerTaxId}
            onChangeText={set('customerTaxId')}
          />
        </View>
      </View>
      <Field label="Description" value={form.description} onChangeText={set('description')} multiline />
      <View style={styles.row}>
        <View style={styles.flex}>
          <Field
            label={`Price (${currency}, before VAT)`}
            value={form.price}
            onChangeText={set('price')}
            placeholder="e.g. 250,00"
            keyboardType="decimal-pad"
          />
        </View>
        <View style={styles.flex}>
          <Field label="VAT %" value={form.vat} onChangeText={set('vat')} keyboardType="decimal-pad" />
        </View>
        <View style={styles.flex}>
          <Field label="Pay within (days)" value={form.terms} onChangeText={set('terms')} keyboardType="number-pad" />
        </View>
      </View>
      {form.price && price == null ? (
        <Text style={[typography.caption, { color: colors.critical }]}>
          Enter the price as a number, e.g. 250 or 1.250,50.
        </Text>
      ) : null}
      {form.extras.map((x, i) => (
        <Pressable
          key={x.description}
          onPress={() =>
            setForm((f) => ({ ...f, extras: f.extras.map((e, j) => (j === i ? { ...e, include: !e.include } : e)) }))
          }
          accessibilityRole="checkbox"
          accessibilityState={{ checked: x.include }}
          style={[styles.extra, { borderColor: x.include ? colors.accent : colors.border }]}
        >
          <Text style={[typography.label, { color: x.include ? colors.accent : colors.muted }]}>
            {x.include ? '✓' : '○'}
          </Text>
          <View style={styles.flex}>
            <Text style={[typography.label, { color: x.include ? colors.text : colors.muted }]}>{x.description}</Text>
            <Text style={[typography.caption, { color: colors.muted }]}>
              {x.quantity} h × {money(x.unitPrice, currency)} = {money(round2(x.quantity * x.unitPrice), currency)}{' '}
              before VAT
            </Text>
          </View>
        </Pressable>
      ))}
      <View style={[styles.totals, { borderColor: colors.border }]}>
        <Text style={[typography.label, { color: colors.textSecondary }]}>
          Subtotal {money(totals.subtotal, currency)} · VAT {money(totals.vat, currency)}
        </Text>
        <Text style={[typography.title, { color: colors.text }]}>Total {money(totals.total, currency)}</Text>
      </View>
      <FormError message={create.error?.message} />
      <View style={styles.row}>
        {canEmail ? (
          <Button
            label="Create and email"
            onPress={() => create.mutate('email')}
            disabled={!valid}
            style={styles.flex}
          />
        ) : null}
        <Button
          label={Platform.OS === 'web' ? 'Create and copy link' : 'Create and share link'}
          variant={canEmail ? 'secondary' : 'primary'}
          onPress={() => create.mutate('manual')}
          disabled={!valid}
          style={styles.flex}
        />
      </View>
      <Button
        label="Create, send later"
        variant="secondary"
        onPress={() => create.mutate(null)}
        disabled={!valid}
        busy={create.isPending}
      />
    </Sheet>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, minWidth: 140 },
  row: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
  extra: {
    flexDirection: 'row',
    gap: spacing.md,
    alignItems: 'flex-start',
    borderWidth: 1,
    borderRadius: radius.md,
    padding: spacing.md,
  },
  totals: { borderTopWidth: StyleSheet.hairlineWidth, paddingTop: spacing.md, gap: 2, alignItems: 'flex-end' },
});
