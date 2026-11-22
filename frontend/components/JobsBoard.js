// The office's job board: create delivery jobs, see where each one is, read the proof, send the customer a link.
import React, { useCallback, useEffect, useMemo, useState } from 'react';
import { Image, Linking, Platform, Pressable, Share, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '../contexts/ThemeContext';
import { radius, spacing, typography } from '../theme/tokens';
import { apiUrl, authHeaders, geocode, jobsApi } from '../utils/api';
import { notifyJobsChanged } from '../utils/events';
import { isPositionStale, jobStatusMeta, jobTitle } from '../utils/jobs';
import { Button, Card, EmptyNote, Field, Page, Pill, Segmented, Sheet, timeAgo } from './ui/kit';

const SCOPES = [
  { key: 'active', label: 'Today' },
  { key: 'delivered', label: 'Delivered' },
  { key: 'all', label: 'All' },
];

const time = (iso) => new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

function progressText(job) {
  switch (job.status) {
    case 'InProgress':
      if (!job.lastPositionAtUtc) return 'Started · waiting for first location';
      return isPositionStale(job)
        ? `Not reporting since ${timeAgo(job.lastPositionAtUtc)}`
        : `Last seen ${timeAgo(job.lastPositionAtUtc)}`;
    case 'Completed':
      return `Delivered ${time(job.deliveredAtUtc)}${job.recipientName ? ` · ${job.recipientName}` : ''}`;
    case 'Planned':
      return 'Not started';
    default:
      return '';
  }
}

/** Shares (phone) or copies (web) a job's customer tracking link. Returns a notice to show, if any. */
export async function shareTrackingLink(job) {
  const url = apiUrl(job.trackingPath);
  const message = `Track your delivery${job.reference ? ` (order ${job.reference})` : ''}: ${url}`;
  if (Platform.OS === 'web') {
    await navigator.clipboard?.writeText(url);
    return 'Link copied';
  }
  await Share.share({ message });
  return null;
}

function Choice({ options, value, onChange, emptyText }) {
  const { colors } = useTheme();
  if (!options.length) return <Text style={[typography.caption, { color: colors.muted }]}>{emptyText}</Text>;
  return (
    <View style={styles.choices}>
      {options.map((o) => {
        const active = o.id === value;
        return (
          <Pressable
            key={o.id}
            onPress={() => onChange(active ? null : o.id)}
            accessibilityRole="button"
            accessibilityState={{ selected: active }}
            style={[styles.choice, { borderColor: active ? colors.text : colors.border, backgroundColor: active ? colors.sunken : colors.card }]}
          >
            <Text style={[typography.label, { color: colors.text, fontWeight: active ? '600' : '500' }]}>{o.label}</Text>
            {o.detail ? <Text style={[typography.caption, { color: colors.muted }]}>{o.detail}</Text> : null}
          </Pressable>
        );
      })}
    </View>
  );
}

function NewJobSheet({ visible, onClose, onCreated }) {
  const { colors } = useTheme();
  const [options, setOptions] = useState({ vehicles: [], drivers: [] });
  const [form, setForm] = useState({ vehicleId: null, driverId: null, customerName: '', reference: '', origin: '', destination: '' });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const set = (k) => (v) => setForm((f) => ({ ...f, [k]: v }));

  useEffect(() => {
    if (!visible) return;
    setError(null);
    jobsApi.assignable().then(setOptions).catch((e) => setError(e.message));
  }, [visible]);

  const valid = form.vehicleId && form.customerName.trim() && form.destination.trim();

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      // Coordinates let the customer page show a map pin and an arrival estimate; the job works without them.
      const place = await geocode(form.destination).catch(() => null);
      const job = await jobsApi.create({
        vehicleId: form.vehicleId,
        driverId: form.driverId,
        customerName: form.customerName.trim(),
        reference: form.reference.trim() || null,
        origin: form.origin.trim() || null,
        destination: form.destination.trim(),
        destinationLatitude: place?.latitude ?? null,
        destinationLongitude: place?.longitude ?? null,
      });
      setForm({ vehicleId: null, driverId: null, customerName: '', reference: '', origin: '', destination: '' });
      notifyJobsChanged();
      onCreated(job, !place);
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet visible={visible} onClose={onClose}>
      <Text style={[typography.title, { color: colors.text }]}>New delivery job</Text>
      <Field label="Customer" placeholder="Who is the delivery for?" value={form.customerName} onChangeText={set('customerName')} />
      <Field label="Deliver to" placeholder="Street, town" value={form.destination} onChangeText={set('destination')} />
      <View style={styles.formRow}>
        <View style={styles.flex}><Field label="From (optional)" placeholder="Depot or pickup" value={form.origin} onChangeText={set('origin')} /></View>
        <View style={styles.flex}><Field label="Order no. (optional)" placeholder="PO / reference" value={form.reference} onChangeText={set('reference')} /></View>
      </View>
      <View style={styles.gapSm}>
        <Text style={[typography.label, { color: colors.textSecondary }]}>Truck</Text>
        <Choice options={options.vehicles} value={form.vehicleId} onChange={set('vehicleId')} emptyText="No trucks yet — add one under Fleet." />
      </View>
      <View style={styles.gapSm}>
        <Text style={[typography.label, { color: colors.textSecondary }]}>Driver (optional)</Text>
        <Choice options={options.drivers} value={form.driverId} onChange={set('driverId')} emptyText="No drivers yet — add one under Fleet." />
      </View>
      {error ? <Text style={[typography.label, { color: colors.critical }]}>{error}</Text> : null}
      <View style={styles.formRow}>
        <Button label="Cancel" variant="secondary" onPress={onClose} style={styles.flex} />
        <Button label={busy ? 'Creating…' : 'Create job'} onPress={submit} disabled={!valid || busy} style={styles.flex} />
      </View>
    </Sheet>
  );
}

function JobSheet({ jobId, onClose, onNotice }) {
  const { colors } = useTheme();
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!jobId) { setDetail(null); return; }
    jobsApi.get(jobId).then(setDetail).catch((e) => setError(e.message));
  }, [jobId]);

  const job = detail?.job;
  const status = jobStatusMeta(job?.status);
  const rows = job ? [
    ['Deliver to', job.destination],
    ['From', job.origin],
    ['Order', job.reference],
    ['Truck', [job.vehicleLabel, job.registration].filter(Boolean).join(' · ')],
    ['Driver', job.driverName],
    ['Started', job.startedAtUtc && new Date(job.startedAtUtc).toLocaleString()],
    ['Delivered', job.deliveredAtUtc && new Date(job.deliveredAtUtc).toLocaleString()],
    ['Received by', job.recipientName],
    ['Note', job.deliveryNote],
  ].filter(([, v]) => v) : [];

  return (
    <Sheet visible={!!jobId} onClose={onClose} width={620}>
      {!job ? (
        <Text style={[typography.body, { color: error ? colors.critical : colors.muted }]}>{error ?? 'Loading…'}</Text>
      ) : (
        <>
          <View style={styles.gapSm}>
            <Pill label={status.label} tone={status.tone} />
            <Text style={[typography.headline, { color: colors.text }]}>{jobTitle(job)}</Text>
            <Text style={[typography.label, { color: isPositionStale(job) ? colors.warning : colors.textSecondary }]}>{progressText(job)}</Text>
          </View>
          <Card padded={false}>
            {rows.map(([k, v], i) => (
              <View key={k} style={[styles.detailRow, i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }]}>
                <Text style={[typography.label, { color: colors.textSecondary }]}>{k}</Text>
                <Text style={[typography.label, styles.detailValue, { color: colors.text }]}>{v}</Text>
              </View>
            ))}
          </Card>
          <View style={styles.gapSm}>
            <Text style={[typography.title, { color: colors.text }]}>Proof of delivery</Text>
            {detail.documents.length === 0 ? (
              <Text style={[typography.body, { color: colors.muted }]}>No photos yet.</Text>
            ) : (
              <View style={styles.photos}>
                {detail.documents.map((d) => (
                  <Pressable key={d.id} onPress={() => Platform.OS === 'web' && Linking.openURL(jobsApi.documentUrl(job.id, d.id))}>
                    <Image
                      source={{ uri: jobsApi.documentUrl(job.id, d.id), headers: authHeaders() }}
                      style={[styles.photo, { borderColor: colors.border, backgroundColor: colors.sunken }]}
                      accessibilityLabel={`Delivery photo taken ${new Date(d.capturedAtUtc).toLocaleString()}`}
                    />
                  </Pressable>
                ))}
              </View>
            )}
          </View>
          <View style={styles.formRow}>
            <Button label="Close" variant="secondary" onPress={onClose} style={styles.flex} />
            {job.trackingPath ? (
              <Button label="Open customer page" variant="secondary" onPress={() => Linking.openURL(apiUrl(job.trackingPath))} style={styles.flex} />
            ) : null}
            {job.trackingPath && job.status !== 'Completed' ? (
              <Button label="Send link" onPress={async () => onNotice(await shareTrackingLink(job))} style={styles.flex} />
            ) : null}
          </View>
        </>
      )}
    </Sheet>
  );
}

export default function JobsBoard({ route, navigation }) {
  const { colors } = useTheme();
  const [scope, setScope] = useState('active');
  const [jobs, setJobs] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [creating, setCreating] = useState(false);
  const [openId, setOpenId] = useState(null);
  const [notice, setNotice] = useState(null);

  const load = useCallback(async () => {
    try {
      setJobs(await jobsApi.list(scope));
      setError(null);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }, [scope]);

  useEffect(() => {
    setLoading(true);
    load();
    const timer = setInterval(load, 30000);
    return () => clearInterval(timer);
  }, [load]);

  // Other screens (the live map) can ask to open a job or the new-job form.
  const openJobParam = route?.params?.openJobId;
  const createParam = route?.params?.create;
  useEffect(() => {
    if (!openJobParam && !createParam) return;
    if (openJobParam) setOpenId(openJobParam);
    if (createParam) setCreating(true);
    navigation?.setParams({ openJobId: undefined, create: undefined });
  }, [openJobParam, createParam, navigation]);

  useEffect(() => {
    if (!notice) return undefined;
    const t = setTimeout(() => setNotice(null), 4000);
    return () => clearTimeout(t);
  }, [notice]);

  const counts = useMemo(() => ({
    onTheWay: jobs.filter((j) => j.status === 'InProgress').length,
    planned: jobs.filter((j) => j.status === 'Planned').length,
    delivered: jobs.filter((j) => j.status === 'Completed').length,
  }), [jobs]);

  const summary = scope === 'active'
    ? `${counts.onTheWay} on the way · ${counts.planned} not started · ${counts.delivered} delivered today`
    : null;

  return (
    <Page title="Jobs" actions={<Button label="New job" onPress={() => setCreating(true)} />}>
      <View style={styles.toolbar}>
        <Segmented options={SCOPES} value={scope} onChange={setScope} />
        {summary ? <Text style={[typography.label, { color: colors.textSecondary }]}>{summary}</Text> : null}
      </View>
      {notice ? <Text style={[typography.label, styles.notice, { color: colors.ok }]}>{notice}</Text> : null}
      {error ? <Text style={[typography.label, styles.notice, { color: colors.critical }]}>{error}</Text> : null}

      {!loading && jobs.length === 0 ? (
        <EmptyNote
          title={scope === 'delivered' ? 'Nothing delivered yet' : 'No jobs yet'}
          body="Create a job for each delivery. The driver sees it in driver mode, and you can send the customer a live tracking link."
        />
      ) : (
        <Card padded={false}>
          {jobs.map((job, i) => {
            const status = jobStatusMeta(job.status);
            const stale = isPositionStale(job);
            return (
              <Pressable
                key={job.id}
                onPress={() => setOpenId(job.id)}
                accessibilityRole="button"
                accessibilityLabel={`Open job for ${jobTitle(job)}`}
                style={({ hovered, pressed }) => [
                  styles.jobRow,
                  i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border },
                  (hovered || pressed) && { backgroundColor: colors.sunken },
                ]}
              >
                <View style={styles.flex}>
                  <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>{jobTitle(job)}</Text>
                  <Text style={[typography.caption, { color: colors.textSecondary }]} numberOfLines={1}>
                    {[job.destination, job.vehicleLabel, job.driverName, job.reference && `#${job.reference}`].filter(Boolean).join(' · ')}
                  </Text>
                </View>
                <View style={styles.jobRight}>
                  <Pill label={status.label} tone={stale ? 'warning' : status.tone} />
                  <Text style={[typography.caption, { color: stale ? colors.warning : colors.muted }]} numberOfLines={1}>{progressText(job)}</Text>
                </View>
              </Pressable>
            );
          })}
        </Card>
      )}

      <NewJobSheet
        visible={creating}
        onClose={() => setCreating(false)}
        onCreated={(job, noPin) => {
          setCreating(false);
          setNotice(noPin
            ? `Job created. The address wasn't found on the map, so the customer page will show no arrival estimate.`
            : 'Job created. Open it to send the customer their tracking link.');
          setScope('active');
          load();
          setOpenId(job.id);
        }}
      />
      <JobSheet jobId={openId} onClose={() => { setOpenId(null); load(); }} onNotice={(n) => n && setNotice(n)} />
    </Page>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  toolbar: { gap: spacing.sm, marginBottom: spacing.lg },
  notice: { marginBottom: spacing.md },
  jobRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  jobRight: { alignItems: 'flex-end', gap: 2, maxWidth: '45%' },
  formRow: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
  gapSm: { gap: spacing.xs },
  choices: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  choice: { borderWidth: 1, borderRadius: radius.md, paddingHorizontal: spacing.md, paddingVertical: spacing.sm, minWidth: 110 },
  detailRow: { flexDirection: 'row', justifyContent: 'space-between', gap: spacing.lg, paddingHorizontal: spacing.lg, paddingVertical: spacing.sm },
  detailValue: { flex: 1, textAlign: 'right' },
  photos: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  photo: { width: 120, height: 120, borderRadius: radius.md, borderWidth: StyleSheet.hairlineWidth },
});
