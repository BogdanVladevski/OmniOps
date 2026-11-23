// Building blocks of the driver's screens. Big targets, few words, the driver's language.
import * as ImagePicker from 'expo-image-picker';
import { useEffect, useState } from 'react';
import { Image, Platform, Pressable, StyleSheet, Text, View } from 'react-native';
import { Button, Card } from '@/components/ui';
import { formatDuration, minutesSince } from '@/features/jobs/waiting';
import { LANGUAGES, useLanguage, type StringKey } from '@/i18n';
import { useTheme } from '@/theme/theme-provider';
import { radius, spacing, typography } from '@/theme/tokens';
import { outbox, type OutboxItem } from './offline/outbox';
import type { PhotoVerdict } from './offline/photo-quality';
import type { TrackingState } from './use-job-tracking';

/** Opens the camera (or gallery) and returns the photo, or null if cancelled. */
export async function pickPhoto(
  fromLibrary: boolean,
  cameraOffMessage: string,
): Promise<ImagePicker.ImagePickerAsset | null> {
  if (!fromLibrary && Platform.OS !== 'web') {
    const permission = await ImagePicker.requestCameraPermissionsAsync();
    if (!permission.granted) throw new Error(cameraOffMessage);
  }
  const options: ImagePicker.ImagePickerOptions = { mediaTypes: ['images'], quality: 0.6, exif: false };
  const result = fromLibrary
    ? await ImagePicker.launchImageLibraryAsync(options)
    : await ImagePicker.launchCameraAsync(options);
  return result.canceled ? null : (result.assets[0] ?? null);
}

export function PhotoButtons({
  label,
  busy,
  disabled,
  onPick,
}: {
  label: string;
  busy: boolean;
  disabled: boolean;
  onPick: (fromLibrary: boolean) => void;
}) {
  const { t } = useLanguage();
  return (
    <View style={styles.row}>
      <Button
        label={label}
        onPress={() => onPick(false)}
        disabled={disabled}
        busy={busy}
        style={[styles.flex, styles.big]}
      />
      {Platform.OS !== 'web' ? (
        <Button
          label={t('fromGallery')}
          variant="secondary"
          onPress={() => onPick(true)}
          disabled={disabled}
          style={[styles.flex, styles.big]}
        />
      ) : null}
    </View>
  );
}

export function Thumbs({ uris }: { uris: string[] }) {
  const { colors } = useTheme();
  if (!uris.length) return null;
  return (
    <View style={styles.thumbs}>
      {uris.map((uri) => (
        <Image key={uri} source={{ uri }} style={[styles.thumb, { borderColor: colors.border }]} />
      ))}
    </View>
  );
}

/** "This photo looks blurry": the driver retakes it or keeps it anyway. */
export function PhotoReview({
  uri,
  verdict,
  onRetake,
  onKeep,
}: {
  uri: string;
  verdict: Exclude<PhotoVerdict, 'ok'>;
  onRetake: () => void;
  onKeep: () => void;
}) {
  const { colors } = useTheme();
  const { t } = useLanguage();
  return (
    <View style={[styles.review, { borderColor: colors.warning }]}>
      <Image source={{ uri }} style={styles.reviewImage} resizeMode="cover" />
      <Text style={[typography.body, { color: colors.text }]}>{t(`quality.${verdict}`)}</Text>
      <View style={styles.row}>
        <Button label={t('retake')} onPress={onRetake} style={[styles.flex, styles.big]} />
        <Button label={t('keepPhoto')} variant="secondary" onPress={onKeep} style={[styles.flex, styles.big]} />
      </View>
    </View>
  );
}

export function Chip({ label, selected, onPress }: { label: string; selected: boolean; onPress: () => void }) {
  const { colors } = useTheme();
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="radio"
      accessibilityState={{ selected }}
      style={[styles.chip, { borderColor: selected ? colors.accent : colors.border }]}
    >
      <Text style={[typography.label, { color: selected ? colors.accent : colors.textSecondary }]}>{label}</Text>
    </Pressable>
  );
}

/** "I've arrived" at a stop, then a running "Waiting since 10:05 · 1 h 05 min" so the driver sees it counts. */
export function ArrivalCard({
  stop,
  arrivedAt,
  busy,
  onArrive,
}: {
  stop: 'pickup' | 'delivery';
  arrivedAt: string | null;
  busy: boolean;
  onArrive: () => void;
}) {
  const { colors } = useTheme();
  const { t, time } = useLanguage();
  const [, tick] = useState(0);
  useEffect(() => {
    if (!arrivedAt) return;
    const timer = setInterval(() => tick((n) => n + 1), 30_000);
    return () => clearInterval(timer);
  }, [arrivedAt]);
  if (arrivedAt) {
    const duration = formatDuration(minutesSince(arrivedAt), { h: t('unit.h'), min: t('unit.min') });
    return (
      <Text style={[typography.label, { color: colors.textSecondary }]}>
        {t('wait.since', { time: time(arrivedAt), duration })}
      </Text>
    );
  }
  return (
    <Card>
      <Text style={[typography.body, { color: colors.textSecondary }]}>{t('wait.hint')}</Text>
      <Button
        label={t(stop === 'pickup' ? 'wait.arrivePickup' : 'wait.arriveDelivery')}
        variant="secondary"
        onPress={onArrive}
        busy={busy}
        style={styles.big}
      />
    </Card>
  );
}

export function TrackingLine({ tracking }: { tracking: TrackingState }) {
  const { colors } = useTheme();
  const { t, time } = useLanguage();
  const line: Record<TrackingState['status'], [string, string] | null> = {
    sharing: tracking.error
      ? [t('gps.offline', { count: tracking.pending }), colors.warning]
      : [
          tracking.lastSentAt ? t('gps.sentAt', { time: time(tracking.lastSentAt) }) : t('gps.sharing'),
          colors.textSecondary,
        ],
    denied: [t('gps.denied'), colors.critical],
    starting: [t('gps.starting'), colors.textSecondary],
    error: [t('gps.error', { message: tracking.error ?? '' }), colors.critical],
    off: null,
  };
  const shown = line[tracking.status];
  return shown ? <Text style={[typography.label, { color: shown[1] }]}>{shown[0]}</Text> : null;
}

/** What is still on the phone, and anything the server refused (with the reason and a way out). */
export function SyncBar({ items, sending }: { items: OutboxItem[]; sending: boolean }) {
  const { colors } = useTheme();
  const { t } = useLanguage();
  if (!items.length) return null;
  return (
    <View
      style={[styles.sync, { borderBottomColor: colors.border, backgroundColor: colors.card }]}
      accessibilityLiveRegion="polite"
    >
      <Text style={[typography.label, { color: colors.warning }]}>
        {sending ? t('sending') : t('waiting', { count: items.length })}
      </Text>
      {items
        .filter((i) => i.error)
        .map((item) => (
          <View key={item.id} style={styles.row}>
            <Text style={[typography.caption, styles.flex, { color: colors.critical }]}>
              {t('refused', { message: item.error ?? '' })}
            </Text>
            <Pressable onPress={() => void outbox.discard(item.id)} accessibilityRole="button" hitSlop={8}>
              <Text style={[typography.label, { color: colors.accent }]}>{t('remove')}</Text>
            </Pressable>
          </View>
        ))}
    </View>
  );
}

export function LanguagePicker() {
  const { colors } = useTheme();
  const { code, setLanguage, t } = useLanguage();
  const [open, setOpen] = useState(false);
  return (
    <View>
      <Pressable
        onPress={() => setOpen((o) => !o)}
        accessibilityRole="button"
        accessibilityLabel={t('language')}
        hitSlop={8}
        style={styles.lang}
      >
        <Text style={[typography.label, { color: colors.accent }]}>{code.toUpperCase()}</Text>
      </Pressable>
      {open ? (
        <View style={[styles.menu, { backgroundColor: colors.elevated, borderColor: colors.border }]}>
          {LANGUAGES.map((l) => (
            <Pressable
              key={l.code}
              onPress={() => {
                setLanguage(l.code);
                setOpen(false);
              }}
              accessibilityRole="button"
              style={styles.menuItem}
            >
              <Text style={[typography.body, { color: l.code === code ? colors.accent : colors.text }]}>{l.name}</Text>
            </Pressable>
          ))}
        </View>
      ) : null}
    </View>
  );
}

export const statusKey = (status: string) => `status.${status}` as StringKey;

export const driverStyles = StyleSheet.create({
  screen: {
    padding: spacing.lg,
    gap: spacing.lg,
    width: '100%',
    maxWidth: 640,
    alignSelf: 'center',
    paddingBottom: spacing.xxxl,
  },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  flex: { flex: 1 },
  gapSm: { gap: spacing.xs },
  big: { minHeight: 52 },
  chips: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
});

const styles = StyleSheet.create({
  ...driverStyles,
  thumbs: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  thumb: {
    width: 72,
    height: 72,
    borderRadius: radius.md,
    borderWidth: StyleSheet.hairlineWidth,
    backgroundColor: '#FFFFFF',
  },
  review: { borderWidth: 1, borderRadius: radius.lg, padding: spacing.lg, gap: spacing.md },
  reviewImage: { width: '100%', aspectRatio: 4 / 3, borderRadius: radius.md },
  chip: {
    borderWidth: 1,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    minHeight: 40,
    justifyContent: 'center',
  },
  sync: {
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.sm,
    gap: spacing.xs,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  lang: { padding: spacing.xs },
  menu: {
    position: 'absolute',
    top: 32,
    right: 0,
    borderWidth: StyleSheet.hairlineWidth,
    borderRadius: radius.md,
    paddingVertical: spacing.xs,
    minWidth: 220,
    zIndex: 20,
    elevation: 6,
  },
  menuItem: { paddingHorizontal: spacing.lg, paddingVertical: spacing.sm },
});
