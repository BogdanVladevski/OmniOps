// Invoices: turn delivered jobs into invoices with their paperwork, send them the same day, see who owes what.
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Linking, Platform, Pressable, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '../contexts/ThemeContext';
import { spacing, toneColors, typography } from '../theme/tokens';
import { apiUrl, billingApi } from '../utils/api';
import { onJobsChanged } from '../utils/events';
import { shareLink } from '../utils/share';
import { invoiceStatusMeta, lineTotals, money, parseAmount, reminderText, round2 } from '../utils/invoices';
import { jobTitle } from '../utils/jobs';
import { Button, Card, EmptyNote, Field, Grid, Page, Pill, Segmented, Sheet, StatTile } from './ui/kit';

const day = (iso) => (iso ? new Date(iso).toLocaleDateString([], { day: 'numeric', month: 'short', year: 'numeric' }) : '—');

function shareInvoiceLink(summary) {
  return shareLink(apiUrl(summary.publicPath), `Invoice ${summary.number}:`);
}

function Row({ label, value }) {
  const { colors } = useTheme();
  if (!value) return null;
  return (
    <View style={styles.detailRow}>
      <Text style={[typography.label, { color: colors.textSecondary }]}>{label}</Text>
      <Text style={[typography.label, styles.detailValue, { color: colors.text }]}>{value}</Text>
    </View>
  );
}

function SettingsSheet({ visible, profile, onClose, onSaved }) {
  const { colors } = useTheme();
  const [form, setForm] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const set = (k) => (v) => setForm((f) => ({ ...f, [k]: v }));

  useEffect(() => {
    if (!visible || !profile) return;
    setError(null);
    setForm({
      legalName: profile.legalName ?? '',
      address: profile.address ?? '',
      taxId: profile.taxId ?? '',
      iban: profile.iban ?? '',
      bankName: profile.bankName ?? '',
      email: profile.email ?? '',
      phone: profile.phone ?? '',
      currency: profile.currency ?? 'EUR',
      vat: String(profile.defaultVatRatePercent ?? 0),
      terms: String(profile.defaultPaymentTermsDays ?? 30),
      prefix: profile.invoicePrefix ?? 'INV',
      freeWait: String(profile.freeWaitingMinutes ?? 120),
      waitRate: profile.waitingRatePerHour != null ? String(profile.waitingRatePerHour) : '',
    });
  }, [visible, profile]);

  if (!form) return null;
  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      const saved = await billingApi.saveProfile({
        legalName: form.legalName, address: form.address, taxId: form.taxId || null, iban: form.iban,
        bankName: form.bankName || null, email: form.email || null, phone: form.phone || null,
        currency: form.currency.trim().toUpperCase(), defaultVatRatePercent: Number(form.vat.replace(',', '.')) || 0,
        defaultPaymentTermsDays: parseInt(form.terms, 10) || 0, invoicePrefix: form.prefix.trim(),
        freeWaitingMinutes: parseInt(form.freeWait, 10) || 0,
        waitingRatePerHour: parseAmount(form.waitRate),
        // Times on invoices (e.g. when a truck arrived) are written in the company's time zone.
        timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone ?? null,
      });
      onSaved(saved);
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet visible={visible} onClose={onClose} width={620}>
      <Text style={[typography.title, { color: colors.text }]}>Invoice settings</Text>
      <Text style={[typography.body, { color: colors.textSecondary }]}>Printed on every invoice. Changes apply to new invoices only.</Text>
      <Field label="Company legal name" value={form.legalName} onChangeText={set('legalName')} placeholder="e.g. Vardar Transport DOOEL" />
      <Field label="Address" value={form.address} onChangeText={set('address')} placeholder="Street, postcode, town, country" multiline />
      <View style={styles.formRow}>
        <View style={styles.flex}><Field label="Tax / VAT number" value={form.taxId} onChangeText={set('taxId')} /></View>
        <View style={styles.flex}><Field label="Email for customers" value={form.email} onChangeText={set('email')} keyboardType="email-address" autoCapitalize="none" /></View>
      </View>
      <View style={styles.formRow}>
        <View style={styles.flex}><Field label="Bank account (IBAN)" value={form.iban} onChangeText={set('iban')} autoCapitalize="characters" /></View>
        <View style={styles.flex}><Field label="Bank" value={form.bankName} onChangeText={set('bankName')} /></View>
      </View>
      <View style={styles.formRow}>
        <View style={styles.flex}><Field label="Currency" value={form.currency} onChangeText={set('currency')} autoCapitalize="characters" maxLength={3} /></View>
        <View style={styles.flex}><Field label="VAT %" value={form.vat} onChangeText={set('vat')} keyboardType="decimal-pad" /></View>
        <View style={styles.flex}><Field label="Pay within (days)" value={form.terms} onChangeText={set('terms')} keyboardType="number-pad" /></View>
        <View style={styles.flex}><Field label="Number prefix" value={form.prefix} onChangeText={set('prefix')} autoCapitalize="characters" /></View>
      </View>
      <Text style={[typography.bodyStrong, { color: colors.text }]}>Waiting time</Text>
      <Text style={[typography.caption, { color: colors.muted }]}>
        The driver's phone records when the truck arrives at a pickup or delivery and when it is released. Waiting longer than
        the free time is added to the invoice, per started 15 minutes, with the times as evidence. Leave the rate empty to
        record waiting without charging for it.
      </Text>
      <View style={styles.formRow}>
        <View style={styles.flex}><Field label="Free waiting (minutes)" value={form.freeWait} onChangeText={set('freeWait')} keyboardType="number-pad" /></View>
        <View style={styles.flex}><Field label={`Rate per hour (${form.currency || 'EUR'})`} value={form.waitRate} onChangeText={set('waitRate')} placeholder="e.g. 30" keyboardType="decimal-pad" /></View>
      </View>
      {!profile.emailSendingEnabled ? (
        <Text style={[typography.caption, { color: colors.muted }]}>
          Email sending is not set up on the server, so you send invoice links yourself (WhatsApp, your own email). Reminders are listed for you to send.
        </Text>
      ) : null}
      {error ? <Text style={[typography.label, { color: colors.critical }]}>{error}</Text> : null}
      <View style={styles.formRow}>
        <Button label="Cancel" variant="secondary" onPress={onClose} style={styles.flex} />
        <Button label={busy ? 'Saving…' : 'Save'} onPress={save} disabled={busy} style={styles.flex} />
      </View>
    </Sheet>
  );
}

function CreateSheet({ item, profile, onClose, onCreated }) {
  const { colors } = useTheme();
  const [form, setForm] = useState(null);
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);
  const set = (k) => (v) => setForm((f) => ({ ...f, [k]: v }));

  useEffect(() => {
    if (!item) { setForm(null); return; }
    setError(null);
    setForm({
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
      extras: (item.suggestedLines ?? []).map((l) => ({ ...l, include: true })),
    });
  }, [item, profile]);

  const price = parseAmount(form?.price);
  const vat = Number(String(form?.vat ?? '0').replace(',', '.'));
  const extras = (form?.extras ?? []).filter((x) => x.include);
  const totals = useMemo(
    () => lineTotals([
      { quantity: 1, unitPrice: price ?? 0, vatRatePercent: vat || 0 },
      ...extras.map((x) => ({ quantity: x.quantity, unitPrice: x.unitPrice, vatRatePercent: vat || 0 })),
    ]),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [price, vat, JSON.stringify(extras)],
  );
  if (!item || !form) return null;

  const currency = profile?.currency ?? 'EUR';
  const canEmail = profile?.emailSendingEnabled && form.customerEmail.trim();
  const valid = form.customerName.trim() && form.description.trim() && price != null && vat >= 0 && vat <= 50;

  const create = (send) => async () => {
    setBusy(send ?? 'issue');
    setError(null);
    try {
      const created = await billingApi.create({
        jobId: item.job.id,
        customerName: form.customerName.trim(),
        customerEmail: form.customerEmail.trim() || null,
        customerAddress: form.customerAddress.trim() || null,
        customerTaxId: form.customerTaxId.trim() || null,
        paymentTermsDays: parseInt(form.terms, 10) || 0,
        lines: [
          { description: form.description.trim(), quantity: 1, unitPrice: price, vatRatePercent: vat },
          ...extras.map((x) => ({ description: x.description, quantity: x.quantity, unitPrice: x.unitPrice, vatRatePercent: vat })),
        ],
        notes: form.notes.trim() || null,
        send,
      });
      const notice = send === 'manual' ? await shareInvoiceLink(created.summary) : send === 'email' ? `Emailed to ${form.customerEmail.trim()}` : null;
      onCreated(created, notice);
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(null);
    }
  };

  return (
    <Sheet visible={!!item} onClose={onClose} width={620}>
      <Text style={[typography.title, { color: colors.text }]}>Invoice for {jobTitle(item.job)}</Text>
      <Text style={[typography.caption, { color: colors.muted }]}>
        Delivered {day(item.job.deliveredAtUtc)}{item.job.recipientName ? ` · received by ${item.job.recipientName}` : ''} · {item.job.photoCount + item.job.pickupDocumentCount} photo(s)
        {item.job.temperatureReadingCount ? ` · ${item.job.temperatureReadingCount} temperature reading(s)` : ''} attached to the invoice
      </Text>
      <View style={styles.formRow}>
        <View style={styles.flex}><Field label="Customer" value={form.customerName} onChangeText={set('customerName')} /></View>
        <View style={styles.flex}><Field label="Customer email" value={form.customerEmail} onChangeText={set('customerEmail')} placeholder="For sending and reminders" keyboardType="email-address" autoCapitalize="none" /></View>
      </View>
      <View style={styles.formRow}>
        <View style={styles.flex}><Field label="Billing address (optional)" value={form.customerAddress} onChangeText={set('customerAddress')} /></View>
        <View style={styles.flex}><Field label="Customer tax number (optional)" value={form.customerTaxId} onChangeText={set('customerTaxId')} /></View>
      </View>
      <Field label="Description" value={form.description} onChangeText={set('description')} multiline />
      <View style={styles.formRow}>
        <View style={styles.flex}><Field label={`Price (${currency}, before VAT)`} value={form.price} onChangeText={set('price')} placeholder="e.g. 250,00" keyboardType="decimal-pad" /></View>
        <View style={styles.flex}><Field label="VAT %" value={form.vat} onChangeText={set('vat')} keyboardType="decimal-pad" /></View>
        <View style={styles.flex}><Field label="Pay within (days)" value={form.terms} onChangeText={set('terms')} keyboardType="number-pad" /></View>
      </View>
      {form.price && price == null ? <Text style={[typography.caption, { color: colors.critical }]}>Enter the price as a number, e.g. 250 or 1.250,50.</Text> : null}
      {form.extras.map((x, i) => (
        <Pressable
          key={x.description}
          onPress={() => setForm((f) => ({ ...f, extras: f.extras.map((e, j) => (j === i ? { ...e, include: !e.include } : e)) }))}
          accessibilityRole="checkbox"
          accessibilityState={{ checked: x.include }}
          style={[styles.extra, { borderColor: x.include ? colors.accent : colors.border }]}
        >
          <Text style={[typography.label, { color: x.include ? colors.accent : colors.muted }]}>{x.include ? '✓' : '○'}</Text>
          <View style={styles.flex}>
            <Text style={[typography.label, { color: x.include ? colors.text : colors.muted }]}>{x.description}</Text>
            <Text style={[typography.caption, { color: colors.muted }]}>
              {x.quantity} h × {money(x.unitPrice, currency)} = {money(round2(x.quantity * x.unitPrice), currency)} before VAT
            </Text>
          </View>
        </Pressable>
      ))}
      <View style={[styles.totals, { borderColor: colors.border }]}>
        <Text style={[typography.label, { color: colors.textSecondary }]}>Subtotal {money(totals.subtotal, currency)} · VAT {money(totals.vat, currency)}</Text>
        <Text style={[typography.title, { color: colors.text }]}>Total {money(totals.total, currency)}</Text>
      </View>
      {error ? <Text style={[typography.label, { color: colors.critical }]}>{error}</Text> : null}
      <View style={styles.formRow}>
        {canEmail ? (
          <Button label={busy === 'email' ? 'Sending…' : `Create and email`} onPress={create('email')} disabled={!valid || !!busy} style={styles.flex} />
        ) : null}
        <Button
          label={busy === 'manual' ? 'Creating…' : Platform.OS === 'web' ? 'Create and copy link' : 'Create and share link'}
          variant={canEmail ? 'secondary' : 'primary'}
          onPress={create('manual')}
          disabled={!valid || !!busy}
          style={styles.flex}
        />
      </View>
      <Button label={busy === 'issue' ? 'Saving…' : 'Create, send later'} variant="secondary" onPress={create(undefined)} disabled={!valid || !!busy} />
    </Sheet>
  );
}

function InvoiceSheet({ invoiceId, profile, onClose, onChanged }) {
  const { colors } = useTheme();
  const [detail, setDetail] = useState(null);
  const [busy, setBusy] = useState(null);
  const [notice, setNotice] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    setNotice(null);
    setError(null);
    if (!invoiceId) { setDetail(null); return; }
    billingApi.get(invoiceId).then(setDetail).catch((e) => setError(e.message));
  }, [invoiceId]);

  const act = (name, fn) => async () => {
    setBusy(name);
    setError(null);
    try {
      await fn();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(null);
    }
  };

  const s = detail?.summary;
  const status = invoiceStatusMeta(s);
  const update = (d, n) => { setDetail(d); setNotice(n); onChanged(); };

  return (
    <Sheet visible={!!invoiceId} onClose={onClose} width={620}>
      {!s ? (
        <Text style={[typography.body, { color: error ? colors.critical : colors.muted }]}>{error ?? 'Loading…'}</Text>
      ) : (
        <>
          <View style={styles.gapSm}>
            <Pill label={status.label} tone={status.tone} />
            <Text style={[typography.headline, { color: colors.text }]}>{money(s.total, s.currency)}</Text>
            <Text style={[typography.body, { color: colors.textSecondary }]}>Invoice {s.number} · {s.customerName}</Text>
          </View>
          <Card padded={false}>
            <Row label="Issued" value={day(s.issueDateUtc)} />
            <Row label="Due" value={day(s.dueDateUtc)} />
            <Row label="Sent" value={s.sentAtUtc ? day(s.sentAtUtc) : 'Not yet'} />
            <Row label="Paid" value={s.paidAtUtc ? day(s.paidAtUtc) : null} />
            <Row label="Customer email" value={s.customerEmail} />
            <Row label="Subtotal" value={money(detail.subtotal, s.currency)} />
            <Row label="VAT" value={money(detail.vatAmount, s.currency)} />
          </Card>
          {detail.lines.map((l, i) => (
            <Text key={i} style={[typography.caption, { color: colors.muted }]}>{l.description} — {money(l.netAmount, s.currency)} + {l.vatRatePercent}% VAT</Text>
          ))}
          {detail.reminders.length ? (
            <View style={styles.gapSm}>
              <Text style={[typography.label, { color: colors.textSecondary }]}>Reminders</Text>
              {detail.reminders.map((r) => (
                <Text key={`${r.kind}`} style={[typography.caption, { color: r.emailed ? colors.textSecondary : colors.warning }]}>{reminderText(r)}</Text>
              ))}
            </View>
          ) : s.status === 'Sent' ? (
            <Text style={[typography.caption, { color: colors.muted }]}>
              Reminders go out 3 days before the due date, on the day, and 7 and 14 days after{s.customerEmail && profile?.emailSendingEnabled ? '' : ' (listed here for you to send)'}.
            </Text>
          ) : null}
          {notice ? <Text style={[typography.label, { color: colors.ok }]}>{notice}</Text> : null}
          {error ? <Text style={[typography.label, { color: colors.critical }]}>{error}</Text> : null}
          <View style={styles.formRow}>
            <Button label="Open invoice" variant="secondary" onPress={() => Linking.openURL(apiUrl(s.publicPath))} style={styles.flex} />
            <Button
              label={Platform.OS === 'web' ? 'Copy link' : 'Share link'}
              variant="secondary"
              onPress={act('share', async () => {
                const n = await shareInvoiceLink(s);
                if (s.status === 'Issued') update(await billingApi.send(s.id, false), n);
                else setNotice(n);
              })}
              style={styles.flex}
            />
          </View>
          {s.status !== 'Paid' ? (
            <View style={styles.formRow}>
              {profile?.emailSendingEnabled && s.customerEmail ? (
                <Button
                  label={busy === 'email' ? 'Sending…' : s.status === 'Issued' ? 'Email to customer' : 'Email again'}
                  variant="secondary"
                  onPress={act('email', async () => update(await billingApi.send(s.id, true), `Emailed to ${s.customerEmail}`))}
                  style={styles.flex}
                />
              ) : null}
              <Button
                label={busy === 'paid' ? 'Saving…' : 'Mark as paid'}
                onPress={act('paid', async () => update(await billingApi.markPaid(s.id), 'Marked as paid. Reminders stopped.'))}
                style={styles.flex}
              />
            </View>
          ) : null}
        </>
      )}
    </Sheet>
  );
}

export default function InvoicesBoard({ route, navigation }) {
  const { colors } = useTheme();
  const [tab, setTab] = useState('toInvoice');
  const [data, setData] = useState(null);
  const [profile, setProfile] = useState(null);
  const [error, setError] = useState(null);
  const [settingsOpen, setSettingsOpen] = useState(false);
  const [creating, setCreating] = useState(null);
  const [openId, setOpenId] = useState(null);
  const [notice, setNotice] = useState(null);

  const load = useCallback(async () => {
    try {
      const [overview, p] = await Promise.all([billingApi.overview(), billingApi.profile()]);
      setData(overview);
      setProfile(p);
      setError(null);
    } catch (e) {
      setError(e.message);
    }
  }, []);

  useEffect(() => {
    load();
    const offChanged = onJobsChanged(load);
    const offFocus = navigation?.addListener?.('focus', load);
    return () => { offChanged(); offFocus?.(); };
  }, [load, navigation]);

  // The Jobs board can send a delivered job straight here.
  const invoiceJobParam = route?.params?.invoiceJobId;
  const openInvoiceParam = route?.params?.openInvoiceId;
  const tabParam = route?.params?.tab;
  useEffect(() => {
    if (tabParam) {
      setTab(tabParam);
      navigation?.setParams({ tab: undefined });
    }
  }, [tabParam, navigation]);
  useEffect(() => {
    if (openInvoiceParam) {
      setOpenId(openInvoiceParam);
      navigation?.setParams({ openInvoiceId: undefined });
    }
    if (!invoiceJobParam || !data) return;
    const item = data.toInvoice.find((t) => t.job.id === invoiceJobParam);
    if (item) {
      setTab('toInvoice');
      if (profile?.isComplete) setCreating(item); else setSettingsOpen(true);
    }
    navigation?.setParams({ invoiceJobId: undefined });
  }, [invoiceJobParam, openInvoiceParam, data, profile, navigation]);

  useEffect(() => {
    if (!notice) return undefined;
    const t = setTimeout(() => setNotice(null), 5000);
    return () => clearTimeout(t);
  }, [notice]);

  const currency = data?.currency ?? 'EUR';
  const unpaid = (data?.invoices ?? []).filter((i) => i.status !== 'Paid');
  const paid = (data?.invoices ?? []).filter((i) => i.status === 'Paid');
  const toInvoice = data?.toInvoice ?? [];
  const tabs = [
    { key: 'toInvoice', label: 'To invoice', count: toInvoice.length },
    { key: 'unpaid', label: 'Unpaid', count: unpaid.length },
    { key: 'paid', label: 'Paid', count: paid.length },
  ];
  const list = tab === 'unpaid'
    ? [...unpaid].sort((a, b) => Number(b.isOverdue) - Number(a.isOverdue) || new Date(a.dueDateUtc) - new Date(b.dueDateUtc))
    : paid;

  return (
    <Page title="Invoices" actions={<Button label="Invoice settings" variant="secondary" onPress={() => setSettingsOpen(true)} />}>
      {profile && !profile.isComplete ? (
        <Card style={styles.setup}>
          <Text style={[typography.title, { color: colors.text }]}>Set up invoicing</Text>
          <Text style={[typography.body, { color: colors.textSecondary }]}>
            Add your company name, address and bank account once. Then every delivered job becomes an invoice in a minute,
            with the proof of delivery attached.
          </Text>
          <View><Button label="Add company details" onPress={() => setSettingsOpen(true)} /></View>
        </Card>
      ) : null}

      <Grid columns={3}>
        <StatTile label="Waiting for payment" value={money(data?.outstanding ?? 0, currency)} />
        <StatTile label={`Overdue${data?.overdueCount ? ` (${data.overdueCount})` : ''}`} value={money(data?.overdue ?? 0, currency)} tone={data?.overdueCount ? 'critical' : 'neutral'} />
        <StatTile label="Paid in the last 30 days" value={money(data?.paidLast30Days ?? 0, currency)} />
      </Grid>

      <View style={styles.toolbar}>
        <Segmented options={tabs} value={tab} onChange={setTab} />
      </View>
      {notice ? <Text style={[typography.label, styles.notice, { color: colors.ok }]}>{notice}</Text> : null}
      {error ? <Text style={[typography.label, styles.notice, { color: colors.critical }]}>{error}</Text> : null}

      {tab === 'toInvoice' ? (
        toInvoice.length === 0 ? (
          <EmptyNote title="Nothing to invoice" body="Delivered jobs appear here, with their proof of delivery, ready to invoice." />
        ) : (
          <Card padded={false}>
            {toInvoice.map((t, i) => (
              <View key={t.job.id} style={[styles.row, i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }]}>
                <View style={styles.flex}>
                  <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>{jobTitle(t.job)}</Text>
                  <Text style={[typography.caption, { color: colors.muted }]} numberOfLines={1}>
                    Delivered {day(t.job.deliveredAtUtc)} · {[t.job.reference && `#${t.job.reference}`, t.job.vehicleLabel].filter(Boolean).join(' · ')}
                  </Text>
                </View>
                <Button
                  label="Create invoice"
                  variant="secondary"
                  onPress={() => (profile?.isComplete ? setCreating(t) : setSettingsOpen(true))}
                />
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
              <Pressable
                key={inv.id}
                onPress={() => setOpenId(inv.id)}
                accessibilityRole="button"
                accessibilityLabel={`Invoice ${inv.number} for ${inv.customerName}`}
                style={({ hovered, pressed }) => [
                  styles.row,
                  i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border },
                  (hovered || pressed) && { backgroundColor: colors.sunken },
                ]}
              >
                <View style={styles.flex}>
                  <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>{inv.customerName}</Text>
                  <Text style={[typography.caption, { color: colors.muted }]} numberOfLines={1}>
                    {inv.number} · issued {day(inv.issueDateUtc)}{inv.remindersSent ? ` · ${inv.remindersSent} reminder(s)` : ''}
                  </Text>
                </View>
                <View style={styles.right}>
                  <Text style={[typography.number, { color: colors.text }]}>{money(inv.total, inv.currency)}</Text>
                  <Text style={[typography.caption, { color: st.tone === 'neutral' ? colors.muted : toneColors(colors, st.tone).fg }]}>{st.label}</Text>
                </View>
              </Pressable>
            );
          })}
        </Card>
      )}

      <SettingsSheet
        visible={settingsOpen}
        profile={profile}
        onClose={() => setSettingsOpen(false)}
        onSaved={(p) => { setProfile(p); setSettingsOpen(false); setNotice('Invoice settings saved'); load(); }}
      />
      <CreateSheet
        item={creating}
        profile={profile}
        onClose={() => setCreating(null)}
        onCreated={(created, n) => {
          setCreating(null);
          setNotice(n ? `Invoice ${created.summary.number} created. ${n}.` : `Invoice ${created.summary.number} created.`);
          setTab('unpaid');
          load();
        }}
      />
      <InvoiceSheet invoiceId={openId} profile={profile} onClose={() => { setOpenId(null); load(); }} onChanged={load} />
    </Page>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  setup: { gap: spacing.md, marginBottom: spacing.lg },
  toolbar: { marginTop: spacing.xl, marginBottom: spacing.md },
  notice: { marginBottom: spacing.md },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  right: { alignItems: 'flex-end', gap: 2 },
  formRow: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
  extra: { flexDirection: 'row', gap: spacing.md, alignItems: 'flex-start', borderWidth: 1, borderRadius: 10, padding: spacing.md },
  gapSm: { gap: spacing.xs },
  totals: { borderTopWidth: StyleSheet.hairlineWidth, paddingTop: spacing.md, gap: 2, alignItems: 'flex-end' },
  detailRow: { flexDirection: 'row', justifyContent: 'space-between', gap: spacing.lg, paddingHorizontal: spacing.lg, paddingVertical: spacing.sm },
  detailValue: { flex: 1, textAlign: 'right' },
});
