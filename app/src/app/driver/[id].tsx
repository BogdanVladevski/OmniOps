// One job on the driver's phone: arrive, loading note, temperature log, start, live GPS, problem reports and proof of
// delivery (photos or the receiver's signature). Every action is saved on the phone first, so no signal is fine.
import { router, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import type { DocumentKind } from '@/api/types';
import { Button, Card, Field, Status } from '@/components/ui';
import { useDriver, type DriverJob } from '@/features/driver/driver-context';
import {
  ArrivalCard,
  Chip,
  driverStyles as s,
  PhotoButtons,
  PhotoReview,
  pickPhoto,
  statusKey,
  Thumbs,
  TrackingLine,
} from '@/features/driver/driver-parts';
import { checkPhoto, type PhotoVerdict } from '@/features/driver/offline/photo-quality';
import { signatureDataUri, type StrokePoint } from '@/features/driver/offline/signature';
import { flushPoints } from '@/features/driver/offline/location-queue';
import { SignaturePad } from '@/features/driver/signature-pad';
import { currentCoords, useJobTracking } from '@/features/driver/use-job-tracking';
import {
  formatCelsius,
  formatRange,
  hasRecentReading,
  isOutOfRange,
  isRefrigerated,
  jobStatusMeta,
  jobTitle,
  parseTemperatureInput,
} from '@/features/jobs/job-format';
import { useNow } from '@/hooks/use-now';
import { translate, useLanguage, type StringKey } from '@/i18n';
import { useTheme } from '@/theme/theme-provider';
import { typography } from '@/theme/tokens';

// Paperwork someone has to read later; the readability check is for these.
const CHECKED: ReadonlySet<DocumentKind> = new Set(['PickupDocument', 'DeliveryPhoto', 'Exception']);
const PROBLEMS = ['damaged', 'short', 'wrong', 'refused', 'other'] as const;
type Problem = (typeof PROBLEMS)[number];
const TEMPERATURE_CHECK_MS = 2 * 60 * 60 * 1000;

type Asset = { uri: string };
interface Review {
  asset: Asset;
  verdict: Exclude<PhotoVerdict, 'ok'>;
  fromLibrary: boolean;
  kind: DocumentKind;
  use: (asset: Asset) => Promise<void>;
}

export default function DriverJobRoute() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { jobs, loading } = useDriver();
  const { colors } = useTheme();
  const job = jobs.find((j) => j.id === id);
  if (!job) {
    return (
      <View style={s.screen}>
        <Text style={[typography.body, { color: colors.muted }]}>{loading ? '…' : '—'}</Text>
      </View>
    );
  }
  return <JobScreen key={job.id} job={job} />;
}

function JobScreen({ job }: { job: DriverJob }) {
  const { colors } = useTheme();
  const { t, time, dateTime } = useLanguage();
  const { act } = useDriver();
  const now = useNow();
  const [busy, setBusy] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [review, setReview] = useState<Review | null>(null);
  // Photos taken on this screen, as thumbnails; counts come from the job (including ones not yet sent).
  const [shots, setShots] = useState<Record<DocumentKind, string[]>>({
    PickupDocument: [],
    DeliveryPhoto: [],
    TemperaturePhoto: [],
    Exception: [],
    Signature: [],
  });
  const [reading, setReading] = useState('');
  const [tempMessage, setTempMessage] = useState<{ bad: boolean; text: string } | null>(null);
  const [recipient, setRecipient] = useState(job.recipientName ?? '');
  const [note, setNote] = useState('');
  const [signing, setSigning] = useState(false);
  const [signedBy, setSignedBy] = useState<string | null>(null);
  const [problemOpen, setProblemOpen] = useState(false);
  const [problem, setProblem] = useState<{ reason: Problem | null; text: string }>({ reason: null, text: '' });
  const [problemDone, setProblemDone] = useState(false);

  const onTheWay = job.status === 'InProgress';
  const open = job.status === 'Planned' || onTheWay;
  const tracking = useJobTracking(job.id, onTheWay && !job.deliveryPending);
  const refrigerated = isRefrigerated(job);
  const parsedReading = parseTemperatureInput(reading);
  const lastReading = job.lastTemperatureAtUtc
    ? { value: job.lastTemperatureCelsius, at: job.lastTemperatureAtUtc }
    : null;

  const run = async (name: string, fn: () => Promise<void>) => {
    setBusy(name);
    setMessage(null);
    try {
      await fn();
    } catch (e) {
      setMessage(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(null);
    }
  };
  const remember = (kind: DocumentKind, uri: string) => setShots((x) => ({ ...x, [kind]: [...x[kind], uri] }));

  /** Takes a photo, checks it can be read, then hands it to `use`. */
  const photograph = (kind: DocumentKind, use: (asset: Asset) => Promise<void>) => (fromLibrary: boolean) =>
    run(kind, async () => {
      const asset = await pickPhoto(fromLibrary, t('cameraOff'));
      if (!asset) return;
      if (CHECKED.has(kind)) {
        const { verdict } = await checkPhoto(asset.uri);
        if (verdict !== 'ok') {
          setReview({ asset, verdict, fromLibrary, kind, use });
          return;
        }
      }
      await use(asset);
    });

  const document =
    (kind: DocumentKind, extra: { temperatureCelsius?: number; note?: string } = {}) =>
    async (asset: Asset) => {
      await act(job.id, {
        type: 'document',
        payload: { kind, coords: await currentCoords(), ...extra },
        photoUri: asset.uri,
      });
      remember(kind, asset.uri);
    };

  const logTemperature = (fromLibrary: boolean) => {
    if (parsedReading == null) {
      setMessage(t('temp.enterFirst'));
      return;
    }
    const value = parsedReading;
    void photograph('TemperaturePhoto', async (asset) => {
      await document('TemperaturePhoto', { temperatureCelsius: value })(asset);
      setReading('');
      setTempMessage(
        isOutOfRange(job, value)
          ? { bad: true, text: t('temp.outOfRange', { value: formatCelsius(value), range: formatRange(job) ?? '' }) }
          : { bad: false, text: t('temp.saved', { value: formatCelsius(value) }) },
      );
    })(fromLibrary);
  };

  const reportProblem = photograph('Exception', async (asset) => {
    // Stored in English so the office reads the same words whatever the driver's language.
    const reason = translate('en', `problem.${problem.reason}` as StringKey);
    const text = problem.text.trim();
    await document('Exception', { note: text ? `${reason}: ${text}` : reason })(asset);
    setProblem({ reason: null, text: '' });
    setProblemDone(true);
  });

  const saveSignature = (strokes: StrokePoint[][], width: number, height: number) =>
    run('signature', async () => {
      const uri = signatureDataUri(strokes, width, height);
      await act(job.id, { type: 'document', payload: { kind: 'Signature', note: recipient.trim() }, photoUri: uri });
      remember('Signature', uri);
      setSignedBy(recipient.trim());
      setSigning(false);
    });

  const arrive = (stop: 'pickup' | 'delivery') => () =>
    run(`arrive-${stop}`, async () => {
      const coords = await currentCoords();
      await act(job.id, {
        type: 'arrive',
        payload: { stop, latitude: coords?.latitude ?? null, longitude: coords?.longitude ?? null },
      });
    });

  const deliver = () =>
    run('deliver', async () => {
      // Best effort: the office gets the full route before the job closes (anything unsent follows later).
      await flushPoints(job.id).catch(() => {});
      const coords = await currentCoords();
      await act(job.id, {
        type: 'deliver',
        payload: {
          recipientName: recipient.trim(),
          note: note.trim() || null,
          latitude: coords?.latitude ?? null,
          longitude: coords?.longitude ?? null,
        },
      });
    });

  const proofCount = job.photoCount + job.signatureCount;
  const readingOk = !refrigerated || hasRecentReading(lastReading?.at, now);
  const canDeliver = proofCount > 0 && readingOk && recipient.trim().length > 0 && !busy;
  const dueAt = lastReading ? new Date(new Date(lastReading.at).getTime() + TEMPERATURE_CHECK_MS) : null;
  const overdue = onTheWay && refrigerated && (!dueAt || dueAt.getTime() <= now);
  const missing = !proofCount
    ? t('pod.needProof')
    : !readingOk
      ? t('pod.needReading')
      : !recipient.trim()
        ? t('pod.needName')
        : null;

  if (review) {
    return (
      <ScrollView contentContainerStyle={s.screen}>
        <PhotoReview
          uri={review.asset.uri}
          verdict={review.verdict}
          onKeep={() => {
            setReview(null);
            void run(review.kind, () => review.use(review.asset));
          }}
          onRetake={() => {
            setReview(null);
            void photograph(review.kind, review.use)(review.fromLibrary);
          }}
        />
      </ScrollView>
    );
  }

  return (
    <ScrollView contentContainerStyle={s.screen} keyboardShouldPersistTaps="handled">
      <Pressable
        onPress={() => (router.canGoBack() ? router.back() : router.replace('/driver'))}
        accessibilityRole="button"
        hitSlop={12}
      >
        <Text style={[typography.label, { color: colors.accent }]}>{t('allJobs')}</Text>
      </Pressable>

      <View style={s.gapSm}>
        <Status label={t(statusKey(job.status))} tone={jobStatusMeta(job.status).tone} />
        <Text style={[typography.headline, { color: colors.text }]}>{jobTitle(job)}</Text>
        <Text style={[typography.body, { color: colors.textSecondary }]}>
          {[job.origin, job.destination].filter(Boolean).join('  →  ')}
        </Text>
        <Text style={[typography.caption, { color: colors.muted }]}>
          {[
            job.reference && t('order', { ref: job.reference }),
            job.vehicleName,
            refrigerated && t('keepAt', { range: formatRange(job) ?? '' }),
          ]
            .filter(Boolean)
            .join(' · ')}
        </Text>
      </View>

      {job.status === 'Planned' ? (
        <ArrivalCard
          stop="pickup"
          arrivedAt={job.pickupArrivedAtUtc}
          busy={busy === 'arrive-pickup'}
          onArrive={arrive('pickup')}
        />
      ) : null}

      {open ? (
        <Card>
          <Text style={[typography.title, { color: colors.text }]}>{t('loading.title')}</Text>
          <Text style={[typography.body, { color: colors.textSecondary }]}>
            {job.pickupDocumentCount > 0 ? t('loading.count', { count: job.pickupDocumentCount }) : t('loading.hint')}
          </Text>
          <Thumbs uris={shots.PickupDocument} />
          <PhotoButtons
            label={job.pickupDocumentCount > 0 ? t('loading.addAnother') : t('loading.add')}
            busy={busy === 'PickupDocument'}
            disabled={!!busy}
            onPick={photograph('PickupDocument', document('PickupDocument'))}
          />
        </Card>
      ) : null}

      {open && refrigerated ? (
        <Card>
          <View style={s.row}>
            <Text style={[typography.title, s.flex, { color: colors.text }]}>{t('temp.title')}</Text>
            <Text style={[typography.label, { color: colors.textSecondary }]}>{formatRange(job)}</Text>
          </View>
          <Text
            style={[
              typography.body,
              {
                color:
                  lastReading && isOutOfRange(job, lastReading.value)
                    ? colors.critical
                    : overdue
                      ? colors.warning
                      : colors.textSecondary,
              },
            ]}
          >
            {lastReading
              ? `${t('temp.last', { value: formatCelsius(lastReading.value), time: time(lastReading.at) })} ` +
                (onTheWay
                  ? overdue || !dueAt
                    ? t('temp.dueNow')
                    : t('temp.dueBy', { time: time(dueAt.toISOString()) })
                  : '')
              : t('temp.hint')}
          </Text>
          <Thumbs uris={shots.TemperaturePhoto} />
          <Field
            label={t('temp.field')}
            placeholder={t('temp.placeholder')}
            value={reading}
            onChangeText={setReading}
            keyboardType="numbers-and-punctuation"
          />
          {reading && parsedReading == null ? (
            <Text style={[typography.caption, { color: colors.critical }]}>{t('temp.invalid')}</Text>
          ) : null}
          <PhotoButtons
            label={t('temp.save')}
            busy={busy === 'TemperaturePhoto'}
            disabled={!!busy || parsedReading == null}
            onPick={logTemperature}
          />
          {tempMessage ? (
            <Text style={[typography.label, { color: tempMessage.bad ? colors.critical : colors.ok }]}>
              {tempMessage.text}
            </Text>
          ) : null}
        </Card>
      ) : null}

      {job.status === 'Planned' ? (
        <Card>
          <Text style={[typography.body, { color: colors.text }]}>{t('start.hint')}</Text>
          <Button
            label={t('start.button')}
            onPress={() => void run('start', () => act(job.id, { type: 'start' }))}
            busy={busy === 'start'}
            disabled={!!busy}
            style={s.big}
          />
        </Card>
      ) : null}

      {onTheWay ? (
        <>
          <TrackingLine tracking={tracking} />
          <ArrivalCard
            stop="delivery"
            arrivedAt={job.deliveryArrivedAtUtc}
            busy={busy === 'arrive-delivery'}
            onArrive={arrive('delivery')}
          />
          <Card>
            <Text style={[typography.title, { color: colors.text }]}>{t('pod.title')}</Text>
            <Text style={[typography.body, { color: colors.textSecondary }]}>
              {t('pod.hint')}
              {refrigerated ? ` ${t('pod.hintCold')}` : ''}
            </Text>
            <Thumbs uris={shots.DeliveryPhoto} />
            {job.photoCount > 0 ? (
              <Text style={[typography.caption, { color: colors.muted }]}>
                {t('pod.photos', { count: job.photoCount })}
              </Text>
            ) : null}
            <PhotoButtons
              label={t('takePhoto')}
              busy={busy === 'DeliveryPhoto'}
              disabled={!!busy}
              onPick={photograph('DeliveryPhoto', document('DeliveryPhoto'))}
            />

            <Field
              label={t('pod.receivedBy')}
              placeholder={t('pod.receivedByPlaceholder')}
              value={recipient}
              onChangeText={setRecipient}
              autoCapitalize="words"
            />

            {signedBy || job.signatureCount > 0 ? (
              <View style={s.gapSm}>
                <Thumbs uris={shots.Signature} />
                <Text style={[typography.label, { color: colors.ok }]}>
                  {t('sign.done', { name: signedBy ?? job.recipientName ?? recipient })}
                </Text>
              </View>
            ) : signing ? (
              <SignaturePad
                hint={t('sign.hint', { name: recipient.trim() })}
                clearLabel={t('sign.clear')}
                saveLabel={busy === 'signature' ? t('pleaseWait') : t('sign.save')}
                onSave={(strokes, w, h) => void saveSignature(strokes, w, h)}
                busy={!!busy}
              />
            ) : (
              <View style={s.gapSm}>
                <Button
                  label={t('sign.open')}
                  variant="secondary"
                  onPress={() => setSigning(true)}
                  disabled={!recipient.trim() || !!busy}
                  style={s.big}
                />
                {!recipient.trim() ? (
                  <Text style={[typography.caption, { color: colors.muted }]}>{t('sign.needName')}</Text>
                ) : null}
              </View>
            )}

            <Field
              label={t('pod.note')}
              placeholder={t('pod.notePlaceholder')}
              value={note}
              onChangeText={setNote}
              multiline
            />
            {missing ? <Text style={[typography.caption, { color: colors.warning }]}>{missing}</Text> : null}
            <Button
              label={t('pod.confirm')}
              onPress={() => void deliver()}
              disabled={!canDeliver}
              busy={busy === 'deliver'}
              style={s.big}
            />
          </Card>
        </>
      ) : null}

      {open ? (
        <Card>
          <Pressable
            onPress={() => {
              setProblemOpen((o) => !o);
              setProblemDone(false);
            }}
            accessibilityRole="button"
            accessibilityState={{ expanded: problemOpen }}
            style={s.row}
          >
            <Text style={[typography.title, s.flex, { color: colors.text }]}>{t('problem.title')}</Text>
            <Text style={[typography.title, { color: colors.accent }]}>{problemOpen ? '−' : '+'}</Text>
          </Pressable>
          {job.exceptionCount > 0 ? (
            <Text style={[typography.caption, { color: colors.warning }]}>
              {t('problem.count', { count: job.exceptionCount })}
            </Text>
          ) : null}
          {problemOpen ? (
            <>
              <Text style={[typography.body, { color: colors.textSecondary }]}>{t('problem.hint')}</Text>
              <View style={s.chips} accessibilityRole="radiogroup">
                {PROBLEMS.map((p) => (
                  <Chip
                    key={p}
                    label={t(`problem.${p}`)}
                    selected={problem.reason === p}
                    onPress={() => setProblem((x) => ({ ...x, reason: p }))}
                  />
                ))}
              </View>
              <Field
                label={t('problem.describe')}
                placeholder={t('problem.placeholder')}
                value={problem.text}
                onChangeText={(text) => setProblem((x) => ({ ...x, text }))}
              />
              <Thumbs uris={shots.Exception} />
              <PhotoButtons
                label={t('problem.send')}
                busy={busy === 'Exception'}
                disabled={!!busy || !problem.reason}
                onPick={reportProblem}
              />
              {problemDone ? <Text style={[typography.label, { color: colors.ok }]}>{t('problem.done')}</Text> : null}
            </>
          ) : null}
        </Card>
      ) : null}

      {job.status === 'Delivered' ? (
        <Card>
          <Text style={[typography.title, { color: colors.text }]}>{t('done.title')}</Text>
          <Text style={[typography.body, { color: colors.textSecondary }]}>
            {[
              job.deliveredAtUtc && dateTime(job.deliveredAtUtc),
              job.recipientName && t('done.by', { name: job.recipientName }),
            ]
              .filter(Boolean)
              .join(' · ')}
          </Text>
          {job.deliveryPending ? (
            <Text style={[typography.label, { color: colors.warning }]}>{t('done.pending')}</Text>
          ) : null}
        </Card>
      ) : null}

      {message ? (
        <Text style={[typography.label, { color: colors.critical }]} accessibilityRole="alert">
          {message}
        </Text>
      ) : null}
    </ScrollView>
  );
}
