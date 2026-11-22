// The driver's phone: today's jobs, loading note, temperature log, live GPS during the trip, proof of delivery.
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Image, Platform, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import * as Location from 'expo-location';
import * as ImagePicker from 'expo-image-picker';
import { useDriverMode } from '../../contexts/DriverModeContext';
import { useTheme } from '../../contexts/ThemeContext';
import { radius, spacing, typography } from '../../theme/tokens';
import { driverApi } from '../../utils/api';
import {
  formatCelsius, formatRange, hasRecentReading, isOutOfRange, isRefrigerated, jobStatusMeta, jobTitle,
  parseTemperatureInput, toLocationPoint,
} from '../../utils/jobs';
import { enqueuePoints, flushPoints, pendingPoints } from '../../utils/locationQueue';
import Screen from '../Screen';
import { Button, Card, EmptyNote, Field, Pill, timeAgo } from '../ui/kit';

const FLUSH_EVERY_MS = 20000;

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
        await flushPoints(jobId);
        if (!cancelled) setState((s) => ({ ...s, lastSentAt: new Date().toISOString(), pending: 0, error: null }));
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
  let text;
  let tone = colors.textSecondary;
  switch (tracking.status) {
    case 'sharing':
      text = tracking.error
        ? `No connection — ${tracking.pending} location update${tracking.pending === 1 ? '' : 's'} saved, will send when back online`
        : `Sharing your location${tracking.lastSentAt ? ` · sent ${timeAgo(tracking.lastSentAt)}` : ''}. Keep the app open while driving.`;
      if (tracking.error) tone = colors.warning;
      break;
    case 'denied':
      text = 'Location is off for OmniOps. Turn it on in Settings so the office and customer can follow the delivery.';
      tone = colors.critical;
      break;
    case 'starting':
      text = 'Starting location sharing…';
      break;
    case 'error':
      text = `Location problem: ${tracking.error}`;
      tone = colors.critical;
      break;
    default:
      return null;
  }
  return <Text style={[typography.label, { color: tone }]}>{text}</Text>;
}

/** Opens the camera (or gallery) and returns the chosen image asset, or null if cancelled. */
async function pickPhoto(fromLibrary) {
  if (!fromLibrary && Platform.OS !== 'web') {
    const permission = await ImagePicker.requestCameraPermissionsAsync();
    if (!permission.granted) throw new Error('Camera access is off. Turn it on in Settings, or choose a photo from the gallery.');
  }
  const options = { mediaTypes: ['images'], quality: 0.6, exif: false };
  const result = fromLibrary ? await ImagePicker.launchImageLibraryAsync(options) : await ImagePicker.launchCameraAsync(options);
  return result.canceled || !result.assets?.length ? null : result.assets[0];
}

function PhotoButtons({ label, busy, disabled, onPick }) {
  return (
    <View style={styles.row}>
      <Button label={busy ? 'Uploading…' : label} onPress={() => onPick(false)} disabled={disabled} style={styles.flex} />
      {Platform.OS !== 'web' ? (
        <Button label="From gallery" variant="secondary" onPress={() => onPick(true)} disabled={disabled} style={styles.flex} />
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

function JobScreen({ job, onBack, onChanged }) {
  const { colors } = useTheme();
  const [busy, setBusy] = useState(null);
  const [message, setMessage] = useState(null);
  const [deliveryPhotos, setDeliveryPhotos] = useState([]);
  const [pickupPhotos, setPickupPhotos] = useState([]);
  const [reading, setReading] = useState('');
  const [readings, setReadings] = useState([]); // logged on this screen: { value, at, uri }
  const [recipient, setRecipient] = useState('');
  const [note, setNote] = useState('');
  const [tempMessage, setTempMessage] = useState(null);
  // Counts the server already had when this screen opened; photos taken here are added on top.
  // (The job object refreshes after "Start trip", so adding its count to local photos would double count.)
  const baseline = useRef({ pickup: job.pickupDocumentCount ?? 0, delivery: job.photoCount ?? 0 }).current;
  const onTheWay = job.status === 'InProgress';
  const open = job.status === 'Planned' || onTheWay;
  const tracking = useJobTracking(job.id, onTheWay);
  const refrigerated = isRefrigerated(job);
  const deliveryCount = baseline.delivery + deliveryPhotos.length;
  const pickupCount = baseline.pickup + pickupPhotos.length;
  const lastLocal = readings[readings.length - 1];
  const lastReading = lastLocal ?? (job.lastTemperatureAtUtc ? { value: job.lastTemperatureCelsius, at: job.lastTemperatureAtUtc } : null);
  const parsedReading = parseTemperatureInput(reading);

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

  const start = () => run('start', async () => onChanged(await driverApi.start(job.id)));

  const addPhoto = (kind, onDone) => (fromLibrary) => run(kind, async () => {
    const asset = await pickPhoto(fromLibrary);
    if (!asset) return;
    await driverApi.uploadDocument(job.id, kind, asset, await currentCoords());
    onDone(asset.uri);
  });

  const logTemperature = (fromLibrary) => run('temperature', async () => {
    setTempMessage(null);
    if (parsedReading == null) throw new Error('Enter the temperature shown on the display first.');
    const asset = await pickPhoto(fromLibrary);
    if (!asset) return;
    const saved = await driverApi.uploadDocument(job.id, 'TemperaturePhoto', asset, await currentCoords(), parsedReading);
    setReadings((r) => [...r, { value: saved.temperatureCelsius, at: saved.capturedAtUtc, uri: asset.uri }]);
    setReading('');
    setTempMessage(isOutOfRange(job, saved.temperatureCelsius)
      ? { bad: true, text: `${formatCelsius(saved.temperatureCelsius)} is outside ${formatRange(job)}. The office has been told. Check the fridge unit's setting.` }
      : { bad: false, text: `${formatCelsius(saved.temperatureCelsius)} saved.` });
  });

  const deliver = () => run('deliver', async () => {
    // Make sure the office has the full route before the job closes.
    await flushPoints(job.id).catch(() => {});
    const coords = await currentCoords();
    onChanged(await driverApi.deliver(job.id, {
      recipientName: recipient.trim(),
      note: note.trim() || null,
      latitude: coords?.latitude ?? null,
      longitude: coords?.longitude ?? null,
    }));
  });

  const status = jobStatusMeta(job.status);
  const readingOk = !refrigerated || hasRecentReading(lastReading?.at);
  const canDeliver = deliveryCount > 0 && readingOk && recipient.trim().length > 0 && !busy;
  const dueAt = lastReading?.at
    ? new Date(new Date(lastReading.at).getTime() + 2 * 60 * 60 * 1000)
    : null;
  const overdue = onTheWay && refrigerated && (!lastReading || dueAt.getTime() <= Date.now());

  return (
    <ScrollView contentContainerStyle={styles.screen} keyboardShouldPersistTaps="handled">
      <Pressable onPress={onBack} accessibilityRole="button" hitSlop={12}>
        <Text style={[typography.label, { color: colors.accent }]}>‹ All jobs</Text>
      </Pressable>

      <View style={styles.gapSm}>
        <Pill label={status.label} tone={status.tone} />
        <Text style={[typography.headline, { color: colors.text }]}>{jobTitle(job)}</Text>
        <Text style={[typography.body, { color: colors.textSecondary }]}>
          {[job.origin, job.destination].filter(Boolean).join('  →  ')}
        </Text>
        <Text style={[typography.caption, { color: colors.muted }]}>
          {[job.reference && `Order ${job.reference}`, job.vehicleLabel, refrigerated && `Keep at ${formatRange(job)}`].filter(Boolean).join(' · ')}
        </Text>
      </View>

      {open ? (
        <Card style={styles.gapMd}>
          <Text style={[typography.title, { color: colors.text }]}>Loading note</Text>
          <Text style={[typography.body, { color: colors.textSecondary }]}>
            {pickupCount > 0
              ? `${pickupCount} photo${pickupCount === 1 ? '' : 's'} of the loading paperwork attached.`
              : 'Photograph the signed loading note or CMR when you collect the goods. It goes with the invoice.'}
          </Text>
          <Thumbs uris={pickupPhotos} />
          <PhotoButtons
            label={pickupCount > 0 ? 'Add another page' : 'Photograph loading note'}
            busy={busy === 'PickupDocument'}
            disabled={!!busy}
            onPick={addPhoto('PickupDocument', (uri) => setPickupPhotos((p) => [...p, uri]))}
          />
        </Card>
      ) : null}

      {open && refrigerated ? (
        <Card style={styles.gapMd}>
          <View style={styles.row}>
            <Text style={[typography.title, styles.flex, { color: colors.text }]}>Load temperature</Text>
            <Text style={[typography.label, { color: colors.textSecondary }]}>{formatRange(job)}</Text>
          </View>
          <Text style={[typography.body, { color: lastReading && isOutOfRange(job, lastReading.value) ? colors.critical : overdue ? colors.warning : colors.textSecondary }]}>
            {lastReading
              ? `Last: ${formatCelsius(lastReading.value)} at ${new Date(lastReading.at).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}. `
                + (onTheWay ? (overdue ? 'Next check is due now.' : `Next check by ${dueAt.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}.`) : '')
              : 'Log the temperature at loading, every 2 hours on the road, and at delivery.'}
          </Text>
          <Thumbs uris={readings.map((r) => r.uri)} />
          <Field
            label="Reading on the fridge display (°C)"
            placeholder="e.g. 4,5"
            value={reading}
            onChangeText={setReading}
            keyboardType="numbers-and-punctuation"
          />
          {reading && parsedReading == null ? (
            <Text style={[typography.caption, { color: colors.critical }]}>Enter a number like 4,5 or −18.</Text>
          ) : null}
          <PhotoButtons
            label="Photograph display and save"
            busy={busy === 'temperature'}
            disabled={!!busy || parsedReading == null}
            onPick={logTemperature}
          />
          {tempMessage ? (
            <Text style={[typography.label, { color: tempMessage.bad ? colors.critical : colors.ok }]}>{tempMessage.text}</Text>
          ) : null}
        </Card>
      ) : null}

      {job.status === 'Planned' ? (
        <Card style={styles.gapMd}>
          <Text style={[typography.body, { color: colors.text }]}>
            When you leave, start the trip. Your location is shared with the office and the customer until you deliver.
          </Text>
          <Button label={busy === 'start' ? 'Starting…' : 'Start trip'} onPress={start} disabled={!!busy} style={styles.big} />
        </Card>
      ) : null}

      {onTheWay ? (
        <>
          <TrackingLine tracking={tracking} />
          <Card style={styles.gapMd}>
            <Text style={[typography.title, { color: colors.text }]}>Proof of delivery</Text>
            <Text style={[typography.body, { color: colors.textSecondary }]}>
              Photograph the signed delivery note or the goods at the door. At least one photo is needed
              {refrigerated ? ', plus a temperature reading taken at the door.' : '.'}
            </Text>
            <Thumbs uris={deliveryPhotos} />
            {deliveryCount > deliveryPhotos.length ? (
              <Text style={[typography.caption, { color: colors.muted }]}>{deliveryCount} photo(s) already attached.</Text>
            ) : null}
            <PhotoButtons
              label="Take photo"
              busy={busy === 'DeliveryPhoto'}
              disabled={!!busy}
              onPick={addPhoto('DeliveryPhoto', (uri) => setDeliveryPhotos((p) => [...p, uri]))}
            />
            <Field label="Received by" placeholder="Name of the person who took the goods" value={recipient} onChangeText={setRecipient} autoCapitalize="words" />
            <Field label="Note (optional)" placeholder="e.g. Left at gate 2, one pallet short" value={note} onChangeText={setNote} multiline />
            {refrigerated && !readingOk ? (
              <Text style={[typography.caption, { color: colors.warning }]}>Log the temperature at the door before confirming.</Text>
            ) : null}
            <Button label={busy === 'deliver' ? 'Confirming…' : 'Confirm delivery'} onPress={deliver} disabled={!canDeliver} style={styles.big} />
          </Card>
        </>
      ) : null}

      {job.status === 'Completed' ? (
        <Card style={styles.gapSm}>
          <Text style={[typography.title, { color: colors.text }]}>Delivered</Text>
          <Text style={[typography.body, { color: colors.textSecondary }]}>
            {job.deliveredAtUtc ? new Date(job.deliveredAtUtc).toLocaleString() : ''}
            {job.recipientName ? ` · received by ${job.recipientName}` : ''}
          </Text>
        </Card>
      ) : null}

      {message ? <Text style={[typography.label, { color: colors.critical }]}>{message}</Text> : null}
    </ScrollView>
  );
}

export default function DriverApp() {
  const { driver, exitDriverMode } = useDriverMode();
  const { colors } = useTheme();
  const [jobs, setJobs] = useState([]);
  const [selectedId, setSelectedId] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const mounted = useRef(true);

  const load = useCallback(async () => {
    try {
      const list = await driverApi.jobs(driver.id);
      if (mounted.current) { setJobs(list); setError(null); }
    } catch (e) {
      if (mounted.current) setError(e.message);
    } finally {
      if (mounted.current) setLoading(false);
    }
  }, [driver.id]);

  useEffect(() => {
    mounted.current = true;
    load();
    const timer = setInterval(load, 60000);
    return () => { mounted.current = false; clearInterval(timer); };
  }, [load]);

  const selected = jobs.find((j) => j.id === selectedId);
  const onChanged = (updated) => setJobs((list) => list.map((j) => (j.id === updated.id ? { ...j, ...updated } : j)));

  return (
    <Screen>
      <View style={[styles.header, { borderBottomColor: colors.border, backgroundColor: colors.card }]}>
        <View style={styles.flex}>
          <Text style={[typography.title, { color: colors.text }]}>{driver.name}</Text>
          <Text style={[typography.caption, { color: colors.muted }]}>Driver</Text>
        </View>
        <Button label="Exit driver mode" variant="secondary" onPress={exitDriverMode} />
      </View>

      {selected ? (
        <JobScreen key={selected.id} job={selected} onBack={() => { setSelectedId(null); load(); }} onChanged={onChanged} />
      ) : (
        <ScrollView
          contentContainerStyle={styles.screen}
          refreshControl={<RefreshControl refreshing={loading} onRefresh={load} />}
        >
          <Text style={[typography.headline, { color: colors.text }]}>Your jobs</Text>
          {error ? <Text style={[typography.label, { color: colors.critical }]}>{error}</Text> : null}
          {!loading && jobs.length === 0 ? (
            <EmptyNote title="No jobs for you right now" body="When the office assigns you a delivery it appears here." />
          ) : null}
          {jobs.map((job) => {
            const status = jobStatusMeta(job.status);
            return (
              <Card key={job.id} onPress={() => setSelectedId(job.id)} accessibilityLabel={`Open ${jobTitle(job)}`} style={styles.gapSm}>
                <View style={styles.row}>
                  <Text style={[typography.bodyStrong, styles.flex, { color: colors.text }]} numberOfLines={1}>{jobTitle(job)}</Text>
                  <Pill label={status.label} tone={status.tone} />
                </View>
                <Text style={[typography.body, { color: colors.textSecondary }]} numberOfLines={1}>{job.destination}</Text>
                {job.reference ? <Text style={[typography.caption, { color: colors.muted }]}>Order {job.reference}</Text> : null}
              </Card>
            );
          })}
        </ScrollView>
      )}
    </Screen>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  header: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md, borderBottomWidth: StyleSheet.hairlineWidth },
  screen: { padding: spacing.lg, gap: spacing.lg, width: '100%', maxWidth: 640, alignSelf: 'center', paddingBottom: spacing.xxxl },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  gapSm: { gap: spacing.xs },
  gapMd: { gap: spacing.md },
  big: { minHeight: 52 },
  thumbs: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  thumb: { width: 72, height: 72, borderRadius: radius.md, borderWidth: StyleSheet.hairlineWidth },
});
