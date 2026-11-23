// What's printed on every invoice, plus the waiting-time terms. Owners only (the server enforces it).
import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { api, unwrap } from '@/api/client';
import { keys, queryClient } from '@/api/query-client';
import type { BillingProfile } from '@/api/types';
import { Button, Field, Sheet } from '@/components/ui';
import { FormError } from '@/features/accounts/auth-frame';
import { useTheme } from '@/theme/theme-provider';
import { spacing, typography } from '@/theme/tokens';
import { parseAmount } from './invoice-format';

const fromProfile = (p: BillingProfile) => ({
  legalName: p.legalName ?? '',
  address: p.address ?? '',
  taxId: p.taxId ?? '',
  iban: p.iban ?? '',
  bankName: p.bankName ?? '',
  email: p.email ?? '',
  phone: p.phone ?? '',
  currency: p.currency ?? 'EUR',
  vat: String(p.defaultVatRatePercent ?? 0),
  terms: String(p.defaultPaymentTermsDays ?? 30),
  prefix: p.invoicePrefix ?? 'INV',
  freeWait: String(p.freeWaitingMinutes ?? 120),
  waitRate: p.waitingRatePerHour != null ? String(p.waitingRatePerHour) : '',
});

type Form = ReturnType<typeof fromProfile>;

/** Mounted while open: the form starts from the saved profile each time. */
export function BillingSettingsSheet({
  profile,
  onClose,
  onSaved,
}: {
  profile: BillingProfile;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { colors } = useTheme();
  const [form, setForm] = useState<Form>(() => fromProfile(profile));
  const set = (key: keyof Form) => (value: string) => setForm((f) => ({ ...f, [key]: value }));

  const save = useMutation({
    mutationFn: (f: Form) =>
      unwrap(
        api.PUT('/api/v1/billing/profile', {
          body: {
            legalName: f.legalName.trim(),
            address: f.address.trim(),
            taxId: f.taxId.trim() || null,
            iban: f.iban.trim() || null,
            bankName: f.bankName.trim() || null,
            email: f.email.trim() || null,
            phone: f.phone.trim() || null,
            currency: f.currency.trim().toUpperCase(),
            defaultVatRatePercent: Number(f.vat.replace(',', '.')) || 0,
            defaultPaymentTermsDays: parseInt(f.terms, 10) || 0,
            invoicePrefix: f.prefix.trim(),
            freeWaitingMinutes: parseInt(f.freeWait, 10) || 0,
            waitingRatePerHour: parseAmount(f.waitRate),
            // Times on invoices (e.g. when a truck arrived) are written in the company's time zone.
            timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone ?? null,
          },
        }),
      ),
    onSuccess: (saved) => {
      queryClient.setQueryData(keys.billingProfile, saved);
      void queryClient.invalidateQueries({ queryKey: keys.invoices });
      onSaved();
    },
  });

  return (
    <Sheet visible onClose={onClose} width={640}>
      <Text style={[typography.title, { color: colors.text }]}>Invoice settings</Text>
      <Text style={[typography.body, { color: colors.textSecondary }]}>
        Printed on every invoice. Changes apply to new invoices only.
      </Text>
      <Field
        label="Company legal name"
        value={form.legalName}
        onChangeText={set('legalName')}
        placeholder="e.g. Vardar Transport DOOEL"
      />
      <Field
        label="Address"
        value={form.address}
        onChangeText={set('address')}
        placeholder="Street, postcode, town, country"
        multiline
      />
      <View style={styles.row}>
        <View style={styles.flex}>
          <Field label="Tax / VAT number" value={form.taxId} onChangeText={set('taxId')} />
        </View>
        <View style={styles.flex}>
          <Field
            label="Email for customers"
            value={form.email}
            onChangeText={set('email')}
            keyboardType="email-address"
            autoCapitalize="none"
          />
        </View>
      </View>
      <View style={styles.row}>
        <View style={styles.flex}>
          <Field label="Bank account (IBAN)" value={form.iban} onChangeText={set('iban')} autoCapitalize="characters" />
        </View>
        <View style={styles.flex}>
          <Field label="Bank" value={form.bankName} onChangeText={set('bankName')} />
        </View>
      </View>
      <View style={styles.row}>
        <View style={styles.flex}>
          <Field
            label="Currency"
            value={form.currency}
            onChangeText={set('currency')}
            autoCapitalize="characters"
            maxLength={3}
          />
        </View>
        <View style={styles.flex}>
          <Field label="VAT %" value={form.vat} onChangeText={set('vat')} keyboardType="decimal-pad" />
        </View>
        <View style={styles.flex}>
          <Field label="Pay within (days)" value={form.terms} onChangeText={set('terms')} keyboardType="number-pad" />
        </View>
        <View style={styles.flex}>
          <Field label="Number prefix" value={form.prefix} onChangeText={set('prefix')} autoCapitalize="characters" />
        </View>
      </View>
      <Text style={[typography.bodyStrong, { color: colors.text }]}>Waiting time</Text>
      <Text style={[typography.caption, { color: colors.muted }]}>
        The driver&apos;s phone records when the truck arrives at a pickup or delivery and when it is released. Waiting
        longer than the free time is added to the invoice, per started 15 minutes, with the times as evidence. Leave the
        rate empty to record waiting without charging for it.
      </Text>
      <View style={styles.row}>
        <View style={styles.flex}>
          <Field
            label="Free waiting (minutes)"
            value={form.freeWait}
            onChangeText={set('freeWait')}
            keyboardType="number-pad"
          />
        </View>
        <View style={styles.flex}>
          <Field
            label={`Rate per hour (${form.currency || 'EUR'})`}
            value={form.waitRate}
            onChangeText={set('waitRate')}
            placeholder="e.g. 30"
            keyboardType="decimal-pad"
          />
        </View>
      </View>
      {!profile.emailSendingEnabled ? (
        <Text style={[typography.caption, { color: colors.muted }]}>
          Email sending is not set up on the server, so you send invoice links yourself (WhatsApp, your own email).
          Reminders are listed for you to send.
        </Text>
      ) : null}
      <FormError message={save.error?.message} />
      <View style={styles.row}>
        <Button label="Cancel" variant="secondary" onPress={onClose} style={styles.flex} />
        <Button label="Save" onPress={() => save.mutate(form)} busy={save.isPending} style={styles.flex} />
      </View>
    </Sheet>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, minWidth: 120 },
  row: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
});
