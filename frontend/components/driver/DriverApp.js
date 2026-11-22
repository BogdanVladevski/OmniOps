// The driver's phone: today's jobs, start a trip (shares GPS), photograph proof of delivery, confirm delivery.
import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Image, Platform, Pressable, RefreshControl, ScrollView, StyleSheet, Text, View } from 'react-native';
import * as Location from 'expo-location';
import * as ImagePicker from 'expo-image-picker';
import { useDriverMode } from '../../contexts/DriverModeContext';
import { useTheme } from '../../contexts/ThemeContext';
import { radius, spacing, typography } from '../../theme/tokens';
import { driverApi } from '../../utils/api';
import { jobStatusMeta, jobTitle, toLocationPoint } from '../../utils/jobs';
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

function JobScreen({ job, onBack, onChanged }) {
  const { colors } = useTheme();
  const [busy, setBusy] = useState(null);
  const [message, setMessage] = useState(null);
  const [photos, setPhotos] = useState([]);
  const [recipient, setRecipient] = useState('');
  const [note, setNote] = useState('');
  const onTheWay = job.status === 'InProgress';
  const tracking = useJobTracking(job.id, onTheWay);
  const uploadedCount = Math.max(job.photoCount ?? 0, photos.length);

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

  const addPhoto = (fromLibrary) => run('photo', async () => {
    if (!fromLibrary && Platform.OS !== 'web') {
      const permission = await ImagePicker.requestCameraPermissionsAsync();
      if (!permission.granted) throw new Error('Camera access is off. Turn it on in Settings, or choose a photo from the gallery.');
    }
    const options = { mediaTypes: ['images'], quality: 0.6, exif: false };
    const result = fromLibrary
      ? await ImagePicker.launchImageLibraryAsync(options)
      : await ImagePicker.launchCameraAsync(options);
    if (result.canceled || !result.assets?.length) return;
    const asset = result.assets[0];
    await driverApi.uploadPhoto(job.id, asset, await currentCoords());
    setPhotos((p) => [...p, asset.uri]);
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
  const canDeliver = uploadedCount > 0 && recipient.trim().length > 0 && !busy;

  return (
    <ScrollView contentContainerStyle={styles.screen}>
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
          {[job.reference && `Order ${job.reference}`, job.vehicleLabel].filter(Boolean).join(' · ')}
        </Text>
      </View>

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
              Photograph the signed delivery note or the goods at the door. At least one photo is needed.
            </Text>
            {photos.length ? (
              <View style={styles.thumbs}>
                {photos.map((uri) => <Image key={uri} source={{ uri }} style={[styles.thumb, { borderColor: colors.border }]} />)}
              </View>
            ) : null}
            {uploadedCount > photos.length ? (
              <Text style={[typography.caption, { color: colors.muted }]}>{uploadedCount} photo(s) already attached.</Text>
            ) : null}
            <View style={styles.row}>
              <Button label={busy === 'photo' ? 'Uploading…' : 'Take photo'} onPress={() => addPhoto(false)} disabled={!!busy} style={styles.flex} />
              {Platform.OS !== 'web' ? (
                <Button label="From gallery" variant="secondary" onPress={() => addPhoto(true)} disabled={!!busy} style={styles.flex} />
              ) : null}
            </View>
            <Field label="Received by" placeholder="Name of the person who took the goods" value={recipient} onChangeText={setRecipient} autoCapitalize="words" />
            <Field label="Note (optional)" placeholder="e.g. Left at gate 2, one pallet short" value={note} onChangeText={setNote} multiline />
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
