// One customer: their portal link (with WhatsApp, Viber and email ready to send), contact details, email updates,
// and their recent jobs and invoices.
import { router } from 'expo-router';
import { useState } from 'react';
import { Linking, Platform, StyleSheet, Text, View } from 'react-native';
import type { Customer, CustomerDetail } from '@/api/types';
import { Button, Card, Divider, Field, Sheet, Status, TextAction, Toggle } from '@/components/ui';
import { FormError } from '@/features/accounts/auth-frame';
import { invoiceStatusMeta, money } from '@/features/invoices/invoice-format';
import { jobStatusMeta, jobTitle } from '@/features/jobs/job-format';
import { useBillingProfile } from '@/features/office-queries';
import { shareLink } from '@/lib/share';
import { dayAndTime } from '@/lib/time';
import { useSession } from '@/lib/use-session';
import { useTheme } from '@/theme/theme-provider';
import { radius, spacing, typography } from '@/theme/tokens';
import { useCustomer, usePortalLink, useSaveCustomer, type CustomerForm } from './customer-queries';
import { mailtoUrl, portalMessage, viberUrl, whatsappUrl } from './customer-share';

const formOf = (c: Customer): CustomerForm => ({
  name: c.name,
  email: c.email ?? '',
  phone: c.phone ?? '',
  address: c.address ?? '',
  taxId: c.taxId ?? '',
  paymentTermsDays: c.paymentTermsDays?.toString() ?? '',
  emailUpdates: c.emailUpdates,
});

function PortalLink({ customer }: { customer: Customer }) {
  const { colors } = useTheme();
  const company = useSession()?.account.organizationName ?? '';
  const portal = usePortalLink(customer.id);
  const [notice, setNotice] = useState<string | null>(null);
  const [confirm, setConfirm] = useState<'replace' | 'off' | null>(null);
  const url = customer.portalUrl;
  const message = url ? portalMessage(customer.name, company, url) : '';

  if (!url) {
    return (
      <Card>
        <Text style={[typography.bodyStrong, { color: colors.text }]}>Customer portal</Text>
        <Text style={[typography.body, { color: colors.textSecondary }]}>
          One link for {customer.name}: every delivery live, signed proof as PDF, and their invoices. No account, no
          app. It stays the same, so they can bookmark it.
        </Text>
        <FormError message={portal.create.error?.message} />
        <View>
          <Button label="Create portal link" onPress={() => portal.create.mutate()} busy={portal.create.isPending} />
        </View>
      </Card>
    );
  }

  const act = (run: () => void) => {
    setConfirm(null);
    setNotice(null);
    run();
  };
  return (
    <Card>
      <View style={styles.row}>
        <Text style={[typography.bodyStrong, styles.flex, { color: colors.text }]}>Customer portal</Text>
        <Status label="On" tone="ok" />
      </View>
      <Text
        selectable
        numberOfLines={2}
        style={[typography.caption, styles.mono, { color: colors.text, backgroundColor: colors.sunken }]}
      >
        {url}
      </Text>
      <View style={styles.buttons}>
        <Button
          label="WhatsApp"
          variant="secondary"
          onPress={() => void Linking.openURL(whatsappUrl(customer.phone, message))}
          style={styles.flex}
        />
        <Button
          label="Viber"
          variant="secondary"
          onPress={() => void Linking.openURL(viberUrl(message))}
          style={styles.flex}
        />
        {customer.email ? (
          <Button
            label="Email"
            variant="secondary"
            onPress={() => void Linking.openURL(mailtoUrl(customer.email, `Your deliveries from ${company}`, message))}
            style={styles.flex}
          />
        ) : null}
        <Button
          label={Platform.OS === 'web' ? 'Copy link' : 'Share'}
          variant="secondary"
          onPress={() => void shareLink(url, message).then(setNotice)}
          style={styles.flex}
        />
      </View>
      {!customer.phone ? (
        <Text style={[typography.caption, { color: colors.muted }]}>
          Add their phone number below to open WhatsApp straight in their chat.
        </Text>
      ) : null}
      {notice ? <Text style={[typography.caption, { color: colors.ok }]}>{notice}</Text> : null}
      <View style={styles.actions}>
        <TextAction label="Open their page" onPress={() => void Linking.openURL(url)} />
        {confirm === 'replace' ? (
          <TextAction
            label="Tap again: the old link stops working"
            tone="critical"
            onPress={() => act(() => portal.create.mutate())}
          />
        ) : (
          <TextAction label="Replace link" onPress={() => setConfirm('replace')} />
        )}
        {confirm === 'off' ? (
          <TextAction
            label="Tap again to turn off"
            tone="critical"
            onPress={() => act(() => portal.disable.mutate())}
          />
        ) : (
          <TextAction label="Turn off" tone="critical" onPress={() => setConfirm('off')} />
        )}
      </View>
      <FormError message={portal.create.error?.message ?? portal.disable.error?.message} />
    </Card>
  );
}

function Details({ customer }: { customer: Customer }) {
  const { colors } = useTheme();
  const profile = useBillingProfile().data;
  const save = useSaveCustomer(customer.id);
  const [form, setForm] = useState(() => formOf(customer));
  const set = (key: Exclude<keyof CustomerForm, 'emailUpdates'>) => (value: string) =>
    setForm((f) => ({ ...f, [key]: value }));
  const dirty = JSON.stringify(form) !== JSON.stringify(formOf(customer));

  return (
    <Card>
      <Text style={[typography.bodyStrong, { color: colors.text }]}>Details</Text>
      <Field label="Name" value={form.name} onChangeText={set('name')} />
      <View style={styles.formRow}>
        <View style={styles.flex}>
          <Field
            label="Email"
            value={form.email}
            onChangeText={set('email')}
            keyboardType="email-address"
            autoCapitalize="none"
          />
        </View>
        <View style={styles.flex}>
          <Field
            label="Phone (WhatsApp / Viber)"
            value={form.phone}
            onChangeText={set('phone')}
            keyboardType="phone-pad"
            placeholder="+389 70 123 456"
          />
        </View>
      </View>
      <Field label="Billing address" value={form.address} onChangeText={set('address')} />
      <View style={styles.formRow}>
        <View style={styles.flex}>
          <Field label="Tax number" value={form.taxId} onChangeText={set('taxId')} />
        </View>
        <View style={styles.flex}>
          <Field
            label="Pays within (days)"
            value={form.paymentTermsDays}
            onChangeText={set('paymentTermsDays')}
            keyboardType="number-pad"
          />
        </View>
      </View>
      <View style={styles.switchRow}>
        <View style={styles.flex}>
          <Text style={[typography.body, { color: colors.text }]}>Email updates</Text>
          <Text style={[typography.caption, { color: colors.muted }]}>
            Picked up, arriving in about 30 minutes, and delivered with the proof attached.
            {profile?.emailSendingEnabled === false
              ? ' Email sending is not set up on the server yet, so none go out until it is.'
              : ''}
          </Text>
        </View>
        <Toggle
          value={form.emailUpdates}
          onChange={(emailUpdates) => setForm((f) => ({ ...f, emailUpdates }))}
          label="Email updates"
        />
      </View>
      <FormError message={save.error?.message} />
      {save.isSuccess && !dirty ? <Text style={[typography.caption, { color: colors.ok }]}>Saved</Text> : null}
      <View>
        <Button
          label="Save details"
          onPress={() => save.mutate(form)}
          disabled={!dirty || !form.name.trim()}
          busy={save.isPending}
        />
      </View>
    </Card>
  );
}

function History({ detail, onClose }: { detail: CustomerDetail; onClose: () => void }) {
  const { colors } = useTheme();
  const go = (pathname: '/jobs' | '/invoices', open: string) => {
    onClose();
    router.navigate({ pathname, params: { open } });
  };
  return (
    <>
      <Text style={[typography.title, { color: colors.text }]}>Recent jobs</Text>
      {detail.recentJobs.length === 0 ? (
        <Text style={[typography.body, { color: colors.muted }]}>No jobs yet.</Text>
      ) : (
        <Card padded={false}>
          {detail.recentJobs.map((job, i) => (
            <View key={job.id}>
              {i > 0 ? <Divider /> : null}
              <Card
                padded={false}
                style={styles.flat}
                onPress={() => go('/jobs', job.id)}
                accessibilityLabel={`Open job ${jobTitle(job)}`}
              >
                <View style={styles.listRow}>
                  <View style={styles.flex}>
                    <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>
                      {job.reference ? `Order ${job.reference}` : jobTitle(job)}
                    </Text>
                    <Text style={[typography.caption, { color: colors.muted }]} numberOfLines={1}>
                      {[job.destination, dayAndTime(job.deliveredAtUtc ?? job.createdAtUtc)]
                        .filter(Boolean)
                        .join(' · ')}
                    </Text>
                  </View>
                  <Status {...jobStatusMeta(job.status)} />
                </View>
              </Card>
            </View>
          ))}
        </Card>
      )}
      {detail.invoices.length ? (
        <>
          <Text style={[typography.title, { color: colors.text }]}>Invoices</Text>
          <Card padded={false}>
            {detail.invoices.map((inv, i) => (
              <View key={inv.id}>
                {i > 0 ? <Divider /> : null}
                <Card
                  padded={false}
                  style={styles.flat}
                  onPress={() => go('/invoices', inv.id)}
                  accessibilityLabel={`Open invoice ${inv.number}`}
                >
                  <View style={styles.listRow}>
                    <Text style={[typography.bodyStrong, styles.flex, { color: colors.text }]}>{inv.number}</Text>
                    <Text style={[typography.number, { color: colors.text }]}>{money(inv.total, inv.currency)}</Text>
                    <Status {...invoiceStatusMeta(inv)} />
                  </View>
                </Card>
              </View>
            ))}
          </Card>
        </>
      ) : null}
    </>
  );
}

export function CustomerSheet({ customerId, onClose }: { customerId: string | null; onClose: () => void }) {
  const { colors } = useTheme();
  const detail = useCustomer(customerId);
  const customer = detail.data?.customer;
  return (
    <Sheet visible={!!customerId} onClose={onClose} width={680}>
      {!customer || !detail.data ? (
        <Text style={[typography.body, { color: detail.error ? colors.critical : colors.muted }]}>
          {detail.error?.message ?? 'Loading…'}
        </Text>
      ) : (
        <>
          <View style={styles.block}>
            <Text style={[typography.headline, { color: colors.text }]}>{customer.name}</Text>
            <Text style={[typography.label, { color: colors.textSecondary }]}>
              {[
                `${customer.activeJobs} active`,
                `${customer.deliveredLast30Days} delivered in 30 days`,
                customer.outstanding > 0 ? `${money(customer.outstanding, customer.currency)} to pay` : null,
              ]
                .filter(Boolean)
                .join(' · ')}
            </Text>
            {customer.overdue > 0 ? (
              <Text style={[typography.label, { color: colors.critical }]}>
                {money(customer.overdue, customer.currency)} overdue
              </Text>
            ) : null}
          </View>
          <PortalLink customer={customer} />
          <Details key={customer.id} customer={customer} />
          <History detail={detail.data} onClose={onClose} />
          <Button label="Close" variant="secondary" onPress={onClose} />
        </>
      )}
    </Sheet>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1, minWidth: 170 }, // pairs of fields stack on phones instead of squeezing their labels
  block: { gap: spacing.xs },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  formRow: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
  buttons: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
  actions: { flexDirection: 'row', gap: spacing.lg, flexWrap: 'wrap' },
  switchRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  mono: {
    fontFamily: Platform.select({ ios: 'Menlo', android: 'monospace', default: 'ui-monospace, Menlo, monospace' }),
    padding: spacing.sm,
    borderRadius: radius.sm,
  },
  listRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
  },
  flat: { borderWidth: 0, borderRadius: 0 },
});
