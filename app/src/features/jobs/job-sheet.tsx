// One job, in full: where it is, its story so far, waiting time, and every piece of paperwork the driver captured.
import { router } from 'expo-router';
import { useState } from 'react';
import { Linking, StyleSheet, Text, View } from 'react-native';
import { API_URL } from '@/api/client';
import type { Job, JobDocument, Wait } from '@/api/types';
import { AuthImage, Button, Card, Sheet, Status, TextAction } from '@/components/ui';
import { money } from '@/features/invoices/invoice-format';
import { shareTrackingLink, useJob } from '@/features/office-queries';
import { openAuthorizedFile } from '@/lib/open-file';
import { dayAndTime, timeAgo, clock } from '@/lib/time';
import { useTheme } from '@/theme/theme-provider';
import { radius, spacing, toneColors, typography } from '@/theme/tokens';
import {
  formatCelsius,
  formatRange,
  isOutOfRange,
  isPositionStale,
  isRefrigerated,
  jobStatusMeta,
  jobTitle,
  temperatureStatus,
} from './job-format';
import { evidence, formatDuration, stopName } from './waiting';

function progressText(job: Job): string {
  switch (job.status) {
    case 'InProgress':
      if (!job.lastPositionAtUtc) return 'Started · waiting for the first location';
      return isPositionStale(job)
        ? `Not reporting since ${timeAgo(job.lastPositionAtUtc)}`
        : `Last seen ${timeAgo(job.lastPositionAtUtc)}`;
    case 'Delivered':
      return `Delivered ${job.deliveredAtUtc ? clock(job.deliveredAtUtc) : ''}${job.recipientName ? ` · ${job.recipientName}` : ''}`;
    case 'Planned':
      return 'Not started';
    default:
      return '';
  }
}

const documentPath = (job: Job, doc: JobDocument) => `/api/v1/jobs/${job.id}/documents/${doc.id}`;

function Thumb({
  job,
  doc,
  size = 120,
  signature = false,
}: {
  job: Job;
  doc: JobDocument;
  size?: number;
  signature?: boolean;
}) {
  const { colors } = useTheme();
  return (
    <AuthImage
      path={documentPath(job, doc)}
      fit={signature ? 'contain' : 'cover'}
      label={`Photo taken ${dayAndTime(doc.capturedAtUtc)}`}
      style={[
        styles.photo,
        { borderColor: colors.border },
        signature
          ? { width: size, height: size * 0.4, backgroundColor: '#FFFFFF' }
          : { width: size, height: size, backgroundColor: colors.sunken },
      ]}
    />
  );
}

function Block({ title, tone, children }: { title: string; tone?: string; children: React.ReactNode }) {
  const { colors } = useTheme();
  return (
    <View style={styles.block}>
      <Text style={[typography.title, { color: tone ?? colors.text }]}>{title}</Text>
      {children}
    </View>
  );
}

function Photos({ job, docs, empty }: { job: Job; docs: JobDocument[]; empty: string }) {
  const { colors } = useTheme();
  if (!docs.length) return <Text style={[typography.body, { color: colors.muted }]}>{empty}</Text>;
  return (
    <View style={styles.photos}>
      {docs.map((d) => (
        <Thumb key={d.id} job={job} doc={d} />
      ))}
    </View>
  );
}

/** The job's story so far: each step with its time, the ones still to come in grey. */
function Timeline({ job }: { job: Job }) {
  const { colors } = useTheme();
  const delivered = job.status === 'Delivered';
  const steps = [
    { label: 'Job created', at: job.createdAtUtc as string | null, done: true },
    job.pickupArrivedAtUtc ? { label: 'Arrived at pickup', at: job.pickupArrivedAtUtc, done: true } : null,
    { label: 'Left for the customer', at: job.startedAtUtc, done: !!job.startedAtUtc },
    job.deliveryArrivedAtUtc || !delivered
      ? { label: 'Arrived at the customer', at: job.deliveryArrivedAtUtc, done: !!job.deliveryArrivedAtUtc }
      : null,
    {
      label: job.recipientName ? `Delivered, received by ${job.recipientName}` : 'Delivered',
      at: job.deliveredAtUtc,
      done: delivered,
    },
    { label: 'Invoiced', at: null, done: !!job.invoiceId },
  ].filter((s): s is { label: string; at: string | null; done: boolean } => !!s);
  return (
    <Block title="Timeline">
      <View>
        {steps.map((step, i) => (
          <View key={step.label} style={styles.step}>
            <View style={styles.rail}>
              <View
                style={[
                  styles.dot,
                  {
                    backgroundColor: step.done ? colors.text : 'transparent',
                    borderColor: step.done ? colors.text : colors.border,
                  },
                ]}
              />
              {i < steps.length - 1 ? <View style={[styles.line, { backgroundColor: colors.border }]} /> : null}
            </View>
            <Text style={[typography.label, styles.flex, { color: step.done ? colors.text : colors.muted }]}>
              {step.label}
            </Text>
            <Text style={[typography.caption, { color: colors.muted }]}>
              {step.at ? dayAndTime(step.at) : step.done ? '' : '—'}
            </Text>
          </View>
        ))}
      </View>
    </Block>
  );
}

function Waiting({ waits }: { waits: Wait[] }) {
  const { colors } = useTheme();
  if (!waits.length) return null;
  return (
    <Block title="Waiting time">
      <Card padded={false}>
        {waits.map((w, i) => {
          const over = w.chargedMinutes > 0;
          return (
            <View
              key={w.stop}
              style={[
                styles.waitRow,
                i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border },
              ]}
            >
              <View style={styles.inline}>
                <Text style={[typography.bodyStrong, styles.flex, { color: colors.text }]}>{stopName(w.stop)}</Text>
                <Text style={[typography.number, { color: over ? colors.warning : colors.text }]}>
                  {formatDuration(w.waitedMinutes)}
                </Text>
              </View>
              <Text style={[typography.caption, { color: colors.textSecondary }]}>
                Arrived {clock(w.arrivedAtUtc)}
                {w.releasedAtUtc ? ` · released ${clock(w.releasedAtUtc)}` : ' · still waiting'}
                {` · ${formatDuration(w.freeMinutes)} free`}
                {over ? ` · ${formatDuration(w.chargedMinutes)} chargeable` : ''}
                {w.amount != null ? ` · ${money(w.amount, w.currency)}` : ''}
              </Text>
              <Text style={[typography.caption, { color: w.verified ? colors.muted : colors.warning }]}>
                {evidence(w)}
              </Text>
              {over && w.ratePerHour == null ? (
                <Text style={[typography.caption, { color: colors.muted }]}>
                  Set a waiting rate in Settings → Invoices to charge for it.
                </Text>
              ) : null}
            </View>
          );
        })}
      </Card>
    </Block>
  );
}

export function JobSheet({
  jobId,
  onClose,
  onNotice,
}: {
  jobId: string | null;
  onClose: () => void;
  onNotice: (text: string) => void;
}) {
  const { colors } = useTheme();
  const detail = useJob(jobId);
  const job = detail.data?.job;
  const documents = detail.data?.documents ?? [];
  const docs = (kind: string) => documents.filter((d) => d.kind === kind);

  return (
    <Sheet visible={!!jobId} onClose={onClose} width={640}>
      {!job ? (
        <Text style={[typography.body, { color: detail.error ? colors.critical : colors.muted }]}>
          {detail.error?.message ?? 'Loading…'}
        </Text>
      ) : (
        <JobDetailBody job={job} docs={docs} waits={detail.data?.waits ?? []} onClose={onClose} onNotice={onNotice} />
      )}
    </Sheet>
  );
}

function JobDetailBody({
  job,
  docs,
  waits,
  onClose,
  onNotice,
}: {
  job: Job;
  docs: (kind: string) => JobDocument[];
  waits: Wait[];
  onClose: () => void;
  onNotice: (text: string) => void;
}) {
  const { colors } = useTheme();
  const status = jobStatusMeta(job.status);
  const temp = temperatureStatus(job);
  const delivered = job.status === 'Delivered';
  const readings = docs('TemperaturePhoto');
  const rows = [
    ['Deliver to', job.destination],
    ['From', job.origin],
    ['Order', job.reference],
    ['Truck', [job.vehicleName, job.registration].filter(Boolean).join(' · ')],
    ['Driver', job.driverName],
    ['Phone', job.driverPhone],
    ['Note', job.deliveryNote],
    ['Temperature', formatRange(job)],
  ].filter((r): r is [string, string] => !!r[1]);

  const openCustomer = (customerId: string) => {
    onClose();
    router.navigate({ pathname: '/customers', params: { open: customerId } });
  };
  const [pdfError, setPdfError] = useState<string | null>(null);
  const proofPack = () => {
    setPdfError(null);
    openAuthorizedFile(`/api/v1/jobs/${job.id}/proof.pdf`, `proof-${job.reference ?? job.id.slice(0, 8)}.pdf`).catch(
      (e: unknown) => setPdfError(e instanceof Error ? e.message : 'Could not open the proof pack.'),
    );
  };

  const invoice = () => {
    onClose();
    router.navigate({ pathname: '/invoices', params: job.invoiceId ? { open: job.invoiceId } : { job: job.id } });
  };

  return (
    <>
      <View style={styles.block}>
        <Status label={status.label} tone={isPositionStale(job) ? 'warning' : status.tone} />
        <Text style={[typography.headline, { color: colors.text }]}>{jobTitle(job)}</Text>
        <Text style={[typography.label, { color: isPositionStale(job) ? colors.warning : colors.textSecondary }]}>
          {progressText(job)}
        </Text>
        {job.customerId ? (
          <TextAction
            label={`${job.customerName ?? 'Customer'}: details and portal link`}
            onPress={() => openCustomer(job.customerId!)}
          />
        ) : null}
      </View>
      <Card padded={false}>
        {rows.map(([k, v], i) => (
          <View
            key={k}
            style={[
              styles.detailRow,
              i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border },
            ]}
          >
            <Text style={[typography.label, { color: colors.textSecondary }]}>{k}</Text>
            <Text style={[typography.label, styles.detailValue, { color: colors.text }]}>{v}</Text>
          </View>
        ))}
      </Card>
      {temp ? <Text style={[typography.label, { color: toneColors(colors, temp.tone).fg }]}>{temp.label}</Text> : null}
      <Timeline job={job} />
      <Waiting waits={waits} />
      <Block title="Loading note">
        <Photos job={job} docs={docs('PickupDocument')} empty="No loading note photographed." />
      </Block>
      {isRefrigerated(job) ? (
        <Block title="Temperature log">
          {readings.length === 0 ? (
            <Text style={[typography.body, { color: colors.muted }]}>No readings yet.</Text>
          ) : (
            <Card padded={false}>
              {readings.map((d, i) => (
                <View
                  key={d.id}
                  style={[
                    styles.listRow,
                    i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border },
                  ]}
                >
                  <Thumb job={job} doc={d} size={44} />
                  <Text style={[typography.label, styles.flex, { color: colors.textSecondary }]}>
                    {dayAndTime(d.capturedAtUtc)}
                  </Text>
                  <Text
                    style={[
                      typography.number,
                      { color: isOutOfRange(job, d.temperatureCelsius) ? colors.critical : colors.text },
                    ]}
                  >
                    {formatCelsius(d.temperatureCelsius)}
                  </Text>
                </View>
              ))}
            </Card>
          )}
        </Block>
      ) : null}
      <Block title="Proof of delivery">
        <Photos job={job} docs={docs('DeliveryPhoto')} empty="No photos yet." />
      </Block>
      {docs('Signature').length ? (
        <Block title="Receiver's signature">
          {docs('Signature').map((d) => (
            <View key={d.id} style={styles.block}>
              <Thumb job={job} doc={d} size={260} signature />
              <Text style={[typography.caption, { color: colors.muted }]}>
                {[d.note, dayAndTime(d.capturedAtUtc)].filter(Boolean).join(' · ')}
              </Text>
            </View>
          ))}
        </Block>
      ) : null}
      {docs('Exception').length ? (
        <Block title="Problems reported by the driver" tone={colors.warning}>
          <Card padded={false}>
            {docs('Exception').map((d, i) => (
              <View
                key={d.id}
                style={[
                  styles.listRow,
                  i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border },
                ]}
              >
                <Thumb job={job} doc={d} size={44} />
                <View style={styles.flex}>
                  <Text style={[typography.label, { color: colors.text }]}>{d.note}</Text>
                  <Text style={[typography.caption, { color: colors.muted }]}>{dayAndTime(d.capturedAtUtc)}</Text>
                </View>
              </View>
            ))}
          </Card>
        </Block>
      ) : null}
      {pdfError ? <Text style={[typography.label, { color: colors.critical }]}>{pdfError}</Text> : null}
      <View style={styles.actions}>
        <Button label="Close" variant="secondary" onPress={onClose} style={styles.flex} />
        <Button label="Proof pack (PDF)" variant="secondary" onPress={proofPack} style={styles.flex} />
        {job.trackingPath ? (
          <Button
            label="Open customer page"
            variant="secondary"
            onPress={() => void Linking.openURL(`${API_URL}${job.trackingPath}`)}
            style={styles.flex}
          />
        ) : null}
        {delivered ? (
          <Button label={job.invoiceId ? 'View invoice' : 'Create invoice'} onPress={invoice} style={styles.flex} />
        ) : null}
        {job.trackingPath && !delivered ? (
          <Button
            label="Send link"
            onPress={() => void shareTrackingLink(job).then((n) => n && onNotice(n))}
            style={styles.flex}
          />
        ) : null}
      </View>
    </>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  block: { gap: spacing.xs },
  inline: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  actions: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
  detailRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: spacing.lg,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.sm,
  },
  detailValue: { flex: 1, textAlign: 'right' },
  photos: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  photo: { borderRadius: radius.md, borderWidth: StyleSheet.hairlineWidth },
  step: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.md, minHeight: 28 },
  rail: { width: 12, alignItems: 'center', alignSelf: 'stretch' },
  dot: { width: 10, height: 10, borderRadius: 5, borderWidth: 1.5, marginTop: 4 },
  line: { width: 1, flex: 1, marginTop: 2 },
  waitRow: { paddingHorizontal: spacing.lg, paddingVertical: spacing.md, gap: 2 },
  listRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
  },
});
