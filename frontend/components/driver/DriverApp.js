// The driver's phone: today's jobs, loading note, temperature log, live GPS during the trip, problem reports,
// and proof of delivery (photo or the receiver's signature). Works without signal: every action is saved on
// the phone first (utils/outbox) and sent in order when there is a connection. Shown in the driver's language.
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Image, Platform, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import * as Location from 'expo-location';
import * as ImagePicker from 'expo-image-picker';
import { useSession } from '../../contexts/SessionContext';
import { useTheme } from '../../contexts/ThemeContext';
import { LANGUAGES, LanguageProvider, translate, useLanguage } from '../../i18n';
import { radius, spacing, typography } from '../../theme/tokens';
import { accountApi, driverApi } from '../../utils/api';
import {
  formatCelsius, formatRange, hasRecentReading, isOutOfRange, isRefrigerated, jobStatusMeta, jobTitle,
  parseTemperatureInput, toLocationPoint,
} from '../../utils/jobs';
import { enqueuePoints, flushPoints, pendingPoints } from '../../utils/locationQueue';
import { applyPending, outbox } from '../../utils/outbox';
import { checkPhoto } from '../../utils/photoQuality';
import { signatureDataUri } from '../../utils/signature';
import Screen from '../Screen';
import { Button, Card, EmptyNote, Field, Pill } from '../ui/kit';
import SignaturePad from './SignaturePad';

const FLUSH_EVERY_MS = 20000;
const JOBS_KEY = 'omniops:driver-jobs';
// Paperwork someone has to read later; the readability check is for these.
const CHECKED_KINDS = new Set(['PickupDocument', 'DeliveryPhoto', 'Exception']);
const PROBLEMS = ['damaged', 'short', 'wrong', 'refused', 'other'];

/** Outbox contents, kept current, plus a way to send now. */
function useOutbox() {
  const [items, setItems] = useState([]);
  const [sending, setSending] = useState(false);
  const flush = useCallback(async () => {
    setSending(true);
    try {
      return await outbox.flush();
    } finally {
      setSending(false);
    }
  }, []);
  useEffect(() => {
    const unsubscribe = outbox.subscribe(setItems);
    flush();
    const timer = setInterval(flush, FLUSH_EVERY_MS);
    return () => { unsubscribe(); clearInterval(timer); };
  }, [flush]);
  return { items, sending, flush };
}

/** Shares the phone's position for a running job. Foreground only for now. */
function useJobTracking(jobId, active) {
  const [state, setState] = useState({ status: 'off', lastSentAt: null, pending: 0, error: null });

  useEffect(() => {
    if (!active || !jobId) {
      setState((s) => ({ ...s, status: 'off' }));
      return undefined;
    }

    let subscription;
    let cancelled = false;
    const flush = async () => {
      try {
        // A "Start trip" made offline must reach the server before the route does.
        const queued = await outbox.flush();
        if (queued.offline) throw new Error('offline');
        const waiting = (await pendingPoints(jobId)).length;
        await flushPoints(jobId);
        // Only claim "sent" when points actually reached the server.
        if (!cancelled) {
          setState((s) => ({ ...s, lastSentAt: waiting ? new Date().toISOString() : s.lastSentAt, pending: 0, error: null }));
        }
      } catch (e) {
        const pending = (await pendingPoints(jobId)).length;
        if (!cancelled) setState((s) => ({ ...s, pending, error: e.message }));
      }
    };

    (async () => {
      setState((s) => ({ ...s, status: 'starting' }));
      const { status } = await Location.requestForegroundPermissionsAsync();
      if (cancelled) return;
      if (status !== 'granted') {
        setState((s) => ({ ...s, status: 'denied' }));
        return;
      }
      setState((s) => ({ ...s, status: 'sharing' }));
      subscription = await Location.watchPositionAsync(
        { accuracy: Location.Accuracy.Balanced, timeInterval: 15000, distanceInterval: 50 },
        async (location) => {
          const pending = await enqueuePoints(jobId, [toLocationPoint(location)]);
          if (!cancelled) setState((s) => ({ ...s, pending }));
          if (pending >= 5) flush();
        },
      );
      flush();
    })().catch((e) => !cancelled && setState((s) => ({ ...s, status: 'error', error: e.message })));

    const timer = setInterval(flush, FLUSH_EVERY_MS);
    return () => {
      cancelled = true;
      clearInterval(timer);
      try {
        subscription?.remove();
      } catch {
        // expo-location 19 on web throws here ("removeSubscription is not a function") even though the
        // watch is gone; stopping must never take down the screen right after a delivery.
      }
      flush();
    };
  }, [jobId, active]);

  return state;
}

async function currentCoords() {
  try {
    const last = await Location.getLastKnownPositionAsync();
    const fix = last ?? (await Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced }));
    return fix ? { latitude: fix.coords.latitude, longitude: fix.coords.longitude } : null;
  } catch {
    return null;
  }
}

function TrackingLine({ tracking }) {
  const { colors } = useTheme();
  const { t, time } = useLanguage();
  let text;
  let tone = colors.textSecondary;
  switch (tracking.status) {
    case 'sharing':
      if (tracking.error) {
        text = t('gps.offline', { count: tracking.pending });
        tone = colors.warning;
      } else {
        text = tracking.lastSentAt ? t('gps.sentAt', { time: time(tracking.lastSentAt) }) : t('gps.sharing');
      }
      break;
    case 'denied':
      text = t('gps.denied');
      tone = colors.critical;
      break;
    case 'starting':
      text = t('gps.starting');
      break;
    case 'error':
      text = t('gps.error', { message: tracking.error });
      tone = colors.critical;
      break;
    default:
      return null;
  }
  return <Text style={[typography.label, { color: tone }]}>{text}</Text>;
}

/** Opens the camera (or gallery) and returns the chosen image asset, or null if cancelled. */
async function pickPhoto(fromLibrary, cameraOffMessage) {
  if (!fromLibrary && Platform.OS !== 'web') {
    const permission = await ImagePicker.requestCameraPermissionsAsync();
    if (!permission.granted) throw new Error(cameraOffMessage);
  }
  const options = { mediaTypes: ['images'], quality: 0.6, exif: false };
  const result = fromLibrary ? await ImagePicker.launchImageLibraryAsync(options) : await ImagePicker.launchCameraAsync(options);
  return result.canceled || !result.assets?.length ? null : result.assets[0];
}

function PhotoButtons({ label, busy, disabled, onPick }) {
  const { t } = useLanguage();
  return (
    <View style={styles.row}>
      <Button label={busy ? t('pleaseWait') : label} onPress={() => onPick(false)} disabled={disabled} style={styles.flex} />
      {Platform.OS !== 'web' ? (
        <Button label={t('fromGallery')} variant="secondary" onPress={() => onPick(true)} disabled={disabled} style={styles.flex} />
      ) : null}
    </View>
  );
}

function Thumbs({ uris }) {
  const { colors } = useTheme();
  if (!uris.length) return null;
  return (
    <View style={styles.thumbs}>
      {uris.map((uri) => <Image key={uri} source={{ uri }} style={[styles.thumb, { borderColor: colors.border }]} />)}
    </View>
  );
}

/** "This photo looks blurry" — the driver retakes it or keeps it. */
function PhotoReview({ review, onRetake, onKeep }) {
  const { colors } = useTheme();
  const { t } = useLanguage();
  return (
    <View style={[styles.review, { borderColor: colors.warning }]}>
      <Image source={{ uri: review.asset.uri }} style={styles.reviewImage} resizeMode="cover" />
      <Text style={[typography.body, { color: colors.text }]}>{t(`quality.${review.verdict}`)}</Text>
      <View style={styles.row}>
        <Button label={t('retake')} onPress={onRetake} style={styles.flex} />
        <Button label={t('keepPhoto')} variant="secondary" onPress={onKeep} style={styles.flex} />
      </View>
    </View>
  );
}

function Chip({ label, selected, onPress }) {
  const { colors } = useTheme();
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      accessibilityState={{ selected }}
      style={[styles.chip, { borderColor: selected ? colors.accent : colors.border }]}
    >
      <Text style={[typography.label, { color: selected ? colors.accent : colors.textSecondary }]}>{label}</Text>
    </Pressable>
  );
}

function JobScreen({ job, onBack, act }) {
  const { colors } = useTheme();
  const { t, time, dateTime } = useLanguage();
  const [busy, setBusy] = useState(null);
  const [message, setMessage] = useState(null);
  const [review, setReview] = useState(null); // { kind, asset, verdict, accept }
  // Photos taken on this screen, shown as thumbnails (counts come from the job, including unsent ones).
  const [shots, setShots] = useState({ PickupDocument: [], DeliveryPhoto: [], TemperaturePhoto: [], Exception: [], Signature: [] });
  const [reading, setReading] = useState('');
  const [tempMessage, setTempMessage] = useState(null);
  const [recipient, setRecipient] = useState(job.recipientName ?? '');
  const [note, setNote] = useState('');
  const [signing, setSigning] = useState(false);
  const [signedBy, setSignedBy] = useState(null);
  const [problemOpen, setProblemOpen] = useState(false);
  const [problem, setProblem] = useState({ reason: null, text: '' });
  const [problemDone, setProblemDone] = useState(false);

  const onTheWay = job.status === 'InProgress';
  const open = job.status === 'Planned' || onTheWay;
  const tracking = useJobTracking(job.id, onTheWay && !job.deliveryPending);
  const refrigerated = isRefrigerated(job);
  const parsedReading = parseTemperatureInput(reading);
  const lastReading = job.lastTemperatureAtUtc ? { value: job.lastTemperatureCelsius, at: job.lastTemperatureAtUtc } : null;

  const run = async (name, fn) => {
    setBusy(name);
    setMessage(null);
    try {
      await fn();
    } catch (e) {
      setMessage(e.message);
    } finally {
      setBusy(null);
    }
  };

  const remember = (kind, uri) => setShots((s) => ({ ...s, [kind]: [...s[kind], uri] }));

  /** Takes a photo, checks it can be read, then hands it to `use`. */
  const photograph = (kind, use) => (fromLibrary) => run(kind, async () => {
    const asset = await pickPhoto(fromLibrary, t('cameraOff'));
    if (!asset) return;
    if (CHECKED_KINDS.has(kind)) {
      const { verdict } = await checkPhoto(asset.uri);
      if (verdict !== 'ok') {
        setReview({ kind, asset, verdict, fromLibrary, use });
        return;
      }
    }
    await use(asset);
  });

  const savePhoto = (kind) => async (asset) => {
    await act('document', job.id, { kind, coords: await currentCoords() }, asset.uri);
    remember(kind, asset.uri);
  };

  const logTemperature = (fromLibrary) => {
    if (parsedReading == null) {
      setMessage(t('temp.enterFirst'));
      return undefined;
    }
    return photograph('TemperaturePhoto', async (asset) => {
      const value = parsedReading;
      await act('document', job.id, { kind: 'TemperaturePhoto', temperatureCelsius: value, coords: await currentCoords() }, asset.uri);
      remember('TemperaturePhoto', asset.uri);
      setReading('');
      setTempMessage(isOutOfRange(job, value)
        ? { bad: true, text: t('temp.outOfRange', { value: formatCelsius(value), range: formatRange(job) }) }
        : { bad: false, text: t('temp.saved', { value: formatCelsius(value) }) });
    })(fromLibrary);
  };

  const reportProblem = photograph('Exception', async (asset) => {
    // Stored in English so the office reads the same words whatever the driver's language.
    const reason = translate('en', `problem.${problem.reason}`);
    const text = problem.text.trim();
    await act('document', job.id, { kind: 'Exception', note: text ? `${reason}: ${text}` : reason, coords: await currentCoords() }, asset.uri);
    remember('Exception', asset.uri);
    setProblem({ reason: null, text: '' });
    setProblemDone(true);
  });

  const saveSignature = (strokes, width, height) => run('signature', async () => {
    const uri = signatureDataUri(strokes, width, height);
    await act('document', job.id, { kind: 'Signature', note: recipient.trim() }, uri);
    remember('Signature', uri);
    setSignedBy(recipient.trim());
    setSigning(false);
  });

  const start = () => run('start', () => act('start', job.id));

  const deliver = () => run('deliver', async () => {
    // Best effort: the office gets the full route before the job closes (anything unsent follows later).
    await flushPoints(job.id).catch(() => {});
    const coords = await currentCoords();
    await act('deliver', job.id, {
      recipientName: recipient.trim(),
      note: note.trim() || null,
      latitude: coords?.latitude ?? null,
      longitude: coords?.longitude ?? null,
    });
  });

  const status = jobStatusMeta(job.status);
  const proofCount = (job.photoCount ?? 0) + (job.signatureCount ?? 0);
  const readingOk = !refrigerated || hasRecentReading(lastReading?.at);
  const canDeliver = proofCount > 0 && readingOk && recipient.trim().length > 0 && !busy;
  const dueAt = lastReading?.at ? new Date(new Date(lastReading.at).getTime() + 2 * 60 * 60 * 1000) : null;
  const overdue = onTheWay && refrigerated && (!lastReading || dueAt.getTime() <= Date.now());
  const missing = !proofCount ? t('pod.needProof') : !readingOk ? t('pod.needReading') : !recipient.trim() ? t('pod.needName') : null;

  if (review) {
    return (
      <ScrollView contentContainerStyle={styles.screen}>
        <PhotoReview
          review={review}
          onKeep={() => { const r = review; setReview(null); run(r.kind, () => r.use(r.asset)); }}
          onRetake={() => { const r = review; setReview(null); photograph(r.kind, r.use)(r.fromLibrary); }}
        />
      </ScrollView>
    );
  }

  return (
    <ScrollView contentContainerStyle={styles.screen} keyboardShouldPersistTaps="handled">
      <Pressable onPress={onBack} accessibilityRole="button" hitSlop={12}>
        <Text style={[typography.label, { color: colors.accent }]}>{t('allJobs')}</Text>
      </Pressable>

      <View style={styles.gapSm}>
        <Pill label={t(`status.${job.status}`)} tone={status.tone} />
        <Text style={[typography.headline, { color: colors.text }]}>{jobTitle(job)}</Text>
        <Text style={[typography.body, { color: colors.textSecondary }]}>
          {[job.origin, job.destination].filter(Boolean).join('  →  ')}
        </Text>
        <Text style={[typography.caption, { color: colors.muted }]}>
          {[job.reference && t('order', { ref: job.reference }), job.vehicleLabel, refrigerated && t('keepAt', { range: formatRange(job) })]
            .filter(Boolean).join(' · ')}
        </Text>
      </View>

      {open ? (
        <Card style={styles.gapMd}>
          <Text style={[typography.title, { color: colors.text }]}>{t('loading.title')}</Text>
          <Text style={[typography.body, { color: colors.textSecondary }]}>
            {job.pickupDocumentCount > 0 ? t('loading.count', { count: job.pickupDocumentCount }) : t('loading.hint')}
          </Text>
          <Thumbs uris={shots.PickupDocument} />
          <PhotoButtons
            label={job.pickupDocumentCount > 0 ? t('loading.addAnother') : t('loading.add')}
            busy={busy === 'PickupDocument'}
            disabled={!!busy}
            onPick={photograph('PickupDocument', savePhoto('PickupDocument'))}
          />
        </Card>
      ) : null}

      {open && refrigerated ? (
        <Card style={styles.gapMd}>
          <View style={styles.row}>
            <Text style={[typography.title, styles.flex, { color: colors.text }]}>{t('temp.title')}</Text>
            <Text style={[typography.label, { color: colors.textSecondary }]}>{formatRange(job)}</Text>
          </View>
          <Text style={[typography.body, { color: lastReading && isOutOfRange(job, lastReading.value) ? colors.critical : overdue ? colors.warning : colors.textSecondary }]}>
            {lastReading
              ? `${t('temp.last', { value: formatCelsius(lastReading.value), time: time(lastReading.at) })} `
                + (onTheWay ? (overdue ? t('temp.dueNow') : t('temp.dueBy', { time: time(dueAt.toISOString()) })) : '')
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
          <PhotoButtons label={t('temp.save')} busy={busy === 'TemperaturePhoto'} disabled={!!busy || parsedReading == null} onPick={logTemperature} />
          {tempMessage ? (
            <Text style={[typography.label, { color: tempMessage.bad ? colors.critical : colors.ok }]}>{tempMessage.text}</Text>
          ) : null}
        </Card>
      ) : null}

      {job.status === 'Planned' ? (
        <Card style={styles.gapMd}>
          <Text style={[typography.body, { color: colors.text }]}>{t('start.hint')}</Text>
          <Button label={busy === 'start' ? t('pleaseWait') : t('start.button')} onPress={start} disabled={!!busy} style={styles.big} />
        </Card>
      ) : null}

      {onTheWay ? (
        <>
          <TrackingLine tracking={tracking} />
          <Card style={styles.gapMd}>
            <Text style={[typography.title, { color: colors.text }]}>{t('pod.title')}</Text>
            <Text style={[typography.body, { color: colors.textSecondary }]}>
              {t('pod.hint')}{refrigerated ? ` ${t('pod.hintCold')}` : ''}
            </Text>
            <Thumbs uris={shots.DeliveryPhoto} />
            {job.photoCount > 0 ? (
              <Text style={[typography.caption, { color: colors.muted }]}>{t('pod.photos', { count: job.photoCount })}</Text>
            ) : null}
            <PhotoButtons
              label={t('takePhoto')}
              busy={busy === 'DeliveryPhoto'}
              disabled={!!busy}
              onPick={photograph('DeliveryPhoto', savePhoto('DeliveryPhoto'))}
            />

            <Field
              label={t('pod.receivedBy')}
              placeholder={t('pod.receivedByPlaceholder')}
              value={recipient}
              onChangeText={setRecipient}
              autoCapitalize="words"
            />

            {signedBy || job.signatureCount > 0 ? (
              <View style={styles.gapSm}>
                <Thumbs uris={shots.Signature} />
                <Text style={[typography.label, { color: colors.ok }]}>{t('sign.done', { name: signedBy ?? job.recipientName ?? recipient })}</Text>
              </View>
            ) : signing ? (
              <SignaturePad
                hint={t('sign.hint', { name: recipient.trim() })}
                clearLabel={t('sign.clear')}
                saveLabel={busy === 'signature' ? t('pleaseWait') : t('sign.save')}
                onSave={saveSignature}
                busy={!!busy}
              />
            ) : (
              <View style={styles.gapSm}>
                <Button label={t('sign.open')} variant="secondary" onPress={() => setSigning(true)} disabled={!recipient.trim() || !!busy} />
                {!recipient.trim() ? <Text style={[typography.caption, { color: colors.muted }]}>{t('sign.needName')}</Text> : null}
              </View>
            )}

            <Field label={t('pod.note')} placeholder={t('pod.notePlaceholder')} value={note} onChangeText={setNote} multiline />
            {missing ? <Text style={[typography.caption, { color: colors.warning }]}>{missing}</Text> : null}
            <Button label={busy === 'deliver' ? t('pleaseWait') : t('pod.confirm')} onPress={deliver} disabled={!canDeliver} style={styles.big} />
          </Card>
        </>
      ) : null}

      {open ? (
        <Card style={styles.gapMd}>
          <Pressable onPress={() => { setProblemOpen((o) => !o); setProblemDone(false); }} accessibilityRole="button" style={styles.row}>
            <Text style={[typography.title, styles.flex, { color: colors.text }]}>{t('problem.title')}</Text>
            <Text style={[typography.label, { color: colors.accent }]}>{problemOpen ? '−' : '+'}</Text>
          </Pressable>
          {job.exceptionCount > 0 ? (
            <Text style={[typography.caption, { color: colors.warning }]}>{t('problem.count', { count: job.exceptionCount })}</Text>
          ) : null}
          {problemOpen ? (
            <>
              <Text style={[typography.body, { color: colors.textSecondary }]}>{t('problem.hint')}</Text>
              <View style={styles.chips}>
                {PROBLEMS.map((p) => (
                  <Chip key={p} label={t(`problem.${p}`)} selected={problem.reason === p} onPress={() => setProblem((x) => ({ ...x, reason: p }))} />
                ))}
              </View>
              <Field
                label={t('problem.describe')}
                placeholder={t('problem.placeholder')}
                value={problem.text}
                onChangeText={(text) => setProblem((x) => ({ ...x, text }))}
              />
              <Thumbs uris={shots.Exception} />
              <PhotoButtons label={t('problem.send')} busy={busy === 'Exception'} disabled={!!busy || !problem.reason} onPick={reportProblem} />
              {problemDone ? <Text style={[typography.label, { color: colors.ok }]}>{t('problem.done')}</Text> : null}
            </>
          ) : null}
        </Card>
      ) : null}

      {job.status === 'Completed' ? (
        <Card style={styles.gapSm}>
          <Text style={[typography.title, { color: colors.text }]}>{t('done.title')}</Text>
          <Text style={[typography.body, { color: colors.textSecondary }]}>
            {[job.deliveredAtUtc && dateTime(job.deliveredAtUtc), job.recipientName && t('done.by', { name: job.recipientName })].filter(Boolean).join(' · ')}
          </Text>
          {job.deliveryPending ? <Text style={[typography.label, { color: colors.warning }]}>{t('done.pending')}</Text> : null}
        </Card>
      ) : null}

      {message ? <Text style={[typography.label, { color: colors.critical }]}>{message}</Text> : null}
    </ScrollView>
  );
}

/** What is still on the phone, and anything the server refused (with the reason and a way out). */
function SyncBar({ items, sending }) {
  const { colors } = useTheme();
  const { t } = useLanguage();
  if (!items.length) return null;
  const refused = items.filter((i) => i.error);
  return (
    <View style={[styles.sync, { borderBottomColor: colors.border, backgroundColor: colors.card }]}>
      <Text style={[typography.label, { color: colors.warning }]}>
        {sending ? t('sending') : t('waiting', { count: items.length })}
      </Text>
      {refused.map((item) => (
        <View key={item.id} style={styles.row}>
          <Text style={[typography.caption, styles.flex, { color: colors.critical }]}>{t('refused', { message: item.error })}</Text>
          <Pressable onPress={() => outbox.discard(item.id)} accessibilityRole="button" hitSlop={8}>
            <Text style={[typography.label, { color: colors.accent }]}>{t('remove')}</Text>
          </Pressable>
        </View>
      ))}
    </View>
  );
}

function LanguagePicker() {
  const { colors } = useTheme();
  const { code, setLanguage, t } = useLanguage();
  const [open, setOpen] = useState(false);
  return (
    <View>
      <Pressable onPress={() => setOpen((o) => !o)} accessibilityRole="button" accessibilityLabel={t('language')} hitSlop={8}>
        <Text style={[typography.label, { color: colors.accent }]}>{code.toUpperCase()}</Text>
      </Pressable>
      {open ? (
        <View style={[styles.menu, { backgroundColor: colors.card, borderColor: colors.border }]}>
          {LANGUAGES.map((l) => (
            <Pressable key={l.code} onPress={() => { setLanguage(l.code); setOpen(false); }} accessibilityRole="button" style={styles.menuItem}>
              <Text style={[typography.body, { color: l.code === code ? colors.accent : colors.text }]}>{l.name}</Text>
            </Pressable>
          ))}
        </View>
      ) : null}
    </View>
  );
}

function DriverHome() {
  const { session } = useSession();
  const driver = { id: session.account.driverId, name: session.account.displayName };
  const { colors } = useTheme();
  const { t } = useLanguage();
  const [jobs, setJobs] = useState([]);
  const [selectedId, setSelectedId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [offline, setOffline] = useState(false);
  const [error, setError] = useState(null);
  const mounted = useRef(true);
  const { items, sending, flush } = useOutbox();

  const load = useCallback(async () => {
    try {
      const list = await driverApi.jobs(driver.id);
      if (!mounted.current) return;
      setJobs(list);
      setOffline(false);
      setError(null);
      AsyncStorage.setItem(JOBS_KEY, JSON.stringify(list)).catch(() => {});
    } catch (e) {
      if (!mounted.current) return;
      // No signal: keep working from the jobs saved on the phone.
      const saved = JSON.parse((await AsyncStorage.getItem(JOBS_KEY).catch(() => null)) ?? 'null');
      if (saved && typeof e.status !== 'number') {
        setJobs((current) => (current.length ? current : saved));
        setOffline(true);
      } else {
        setError(e.message);
      }
    } finally {
      if (mounted.current) setLoading(false);
    }
  }, [driver.id]);

  useEffect(() => {
    mounted.current = true;
    AsyncStorage.getItem(JOBS_KEY).then((raw) => { if (raw && mounted.current) setJobs((j) => (j.length ? j : JSON.parse(raw))); }).catch(() => {});
    load();
    const timer = setInterval(load, 60000);
    return () => { mounted.current = false; clearInterval(timer); };
  }, [load]);

  // Everything goes through the outbox; it is sent at once when possible, and the list refreshes afterwards.
  const act = useCallback(async (type, jobId, payload, photoUri) => {
    await outbox.add(type, jobId, payload, photoUri);
    const result = await flush();
    if (result.sent) load();
  }, [flush, load]);

  const shown = jobs.map((j) => applyPending(j, items));
  const selected = shown.find((j) => j.id === selectedId);

  return (
    <Screen>
      <View style={[styles.header, { borderBottomColor: colors.border, backgroundColor: colors.card }]}>
        <View style={styles.flex}>
          <Text style={[typography.title, { color: colors.text }]}>{driver.name}</Text>
          <Text style={[typography.caption, { color: colors.muted }]}>{t('driver')}</Text>
        </View>
        <LanguagePicker />
        <Button label={t('signOut')} variant="secondary" onPress={() => accountApi.signOut()} />
      </View>
      <SyncBar items={items} sending={sending} />

      {selected ? (
        <JobScreen key={selected.id} job={selected} act={act} onBack={() => { setSelectedId(null); load(); }} />
      ) : (
        <ScrollView contentContainerStyle={styles.screen} refreshControl={<RefreshControl refreshing={loading} onRefresh={load} />}>
          <Text style={[typography.headline, { color: colors.text }]}>{t('yourJobs')}</Text>
          {offline ? <Text style={[typography.label, { color: colors.warning }]}>{t('offlineJobs')}</Text> : null}
          {error ? <Text style={[typography.label, { color: colors.critical }]}>{error}</Text> : null}
          {!loading && shown.length === 0 ? <EmptyNote title={t('noJobsTitle')} body={t('noJobsBody')} /> : null}
          {shown.map((job) => {
            const status = jobStatusMeta(job.status);
            return (
              <Card key={job.id} onPress={() => setSelectedId(job.id)} accessibilityLabel={jobTitle(job)} style={styles.gapSm}>
                <View style={styles.row}>
                  <Text style={[typography.bodyStrong, styles.flex, { color: colors.text }]} numberOfLines={1}>{jobTitle(job)}</Text>
                  <Pill label={t(`status.${job.status}`)} tone={status.tone} />
                </View>
                <Text style={[typography.body, { color: colors.textSecondary }]} numberOfLines={1}>{job.destination}</Text>
                {job.reference ? <Text style={[typography.caption, { color: colors.muted }]}>{t('order', { ref: job.reference })}</Text> : null}
              </Card>
            );
          })}
        </ScrollView>
      )}
    </Screen>
  );
}

export default function DriverApp() {
  return (
    <LanguageProvider>
      <DriverHome />
    </LanguageProvider>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  header: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md, borderBottomWidth: StyleSheet.hairlineWidth, zIndex: 10 },
  sync: { paddingHorizontal: spacing.lg, paddingVertical: spacing.sm, gap: spacing.xs, borderBottomWidth: StyleSheet.hairlineWidth },
  screen: { padding: spacing.lg, gap: spacing.lg, width: '100%', maxWidth: 640, alignSelf: 'center', paddingBottom: spacing.xxxl },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  gapSm: { gap: spacing.xs },
  gapMd: { gap: spacing.md },
  big: { minHeight: 52 },
  thumbs: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  thumb: { width: 72, height: 72, borderRadius: radius.md, borderWidth: StyleSheet.hairlineWidth, backgroundColor: '#FFFFFF' },
  review: { borderWidth: 1, borderRadius: radius.lg, padding: spacing.lg, gap: spacing.md },
  reviewImage: { width: '100%', aspectRatio: 4 / 3, borderRadius: radius.md },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  chip: { borderWidth: 1, borderRadius: radius.md, paddingHorizontal: spacing.md, paddingVertical: spacing.sm },
  menu: { position: 'absolute', top: 28, right: 0, borderWidth: StyleSheet.hairlineWidth, borderRadius: radius.md, paddingVertical: spacing.xs, minWidth: 220, zIndex: 20, elevation: 6 },
  menuItem: { paddingHorizontal: spacing.lg, paddingVertical: spacing.sm },
});
