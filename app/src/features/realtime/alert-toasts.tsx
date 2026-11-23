// Live alerts as quiet notices in a corner: a status dot, the sentence, and the job one tap away.
import { router } from 'expo-router';
import { useEffect } from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useLayout } from '@/hooks/use-layout';
import { useTheme } from '@/theme/theme-provider';
import { floating, radius, spacing, toneColors, typography, type Tone } from '@/theme/tokens';
import { useLive, type LiveAlert } from './live-updates';

const TONE: Record<string, Tone> = {
  JobTemperatureOutOfRange: 'critical',
  JobProblemReported: 'critical',
  JobWaitingCharged: 'warning',
  JobDelivered: 'ok',
};
const SHOW_FOR_MS = 12_000;

function Toast({ alert, onDone }: { alert: LiveAlert; onDone: () => void }) {
  const { colors, isDark } = useTheme();
  useEffect(() => {
    const t = setTimeout(onDone, SHOW_FOR_MS);
    return () => clearTimeout(t);
  }, [onDone]);
  const tone = toneColors(colors, TONE[alert.kind] ?? 'neutral');
  return (
    <Pressable
      onPress={() => {
        onDone();
        if (alert.jobId) router.navigate({ pathname: '/jobs', params: { open: alert.jobId } });
      }}
      accessibilityRole="alert"
      style={[styles.toast, floating(isDark), { backgroundColor: colors.elevated, borderColor: colors.border }]}
    >
      <View style={[styles.dot, { backgroundColor: tone.fg }]} />
      <View style={styles.text}>
        <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={2}>
          {alert.title}
        </Text>
        <Text style={[typography.label, { color: colors.textSecondary }]} numberOfLines={3}>
          {alert.message}
        </Text>
      </View>
    </Pressable>
  );
}

export function AlertToasts() {
  const { alerts, dismiss } = useLive();
  const { isWide } = useLayout();
  const insets = useSafeAreaInsets();
  if (alerts.length === 0) return null;
  return (
    <View
      pointerEvents="box-none"
      style={[
        styles.stack,
        isWide
          ? { top: 64, right: spacing.xl, width: 360 }
          : { top: insets.top + spacing.sm, left: spacing.md, right: spacing.md },
      ]}
    >
      {alerts.slice(0, 3).map((alert) => (
        <Toast key={alert.id} alert={alert} onDone={() => dismiss(alert.id)} />
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  stack: { position: 'absolute', gap: spacing.sm, zIndex: 3000 },
  toast: {
    flexDirection: 'row',
    gap: spacing.md,
    padding: spacing.md,
    borderRadius: radius.lg,
    borderWidth: StyleSheet.hairlineWidth,
  },
  dot: { width: 8, height: 8, borderRadius: 4, marginTop: 7 },
  text: { flex: 1, gap: 2 },
});
