import { StyleSheet, Text, View } from 'react-native';
import { useTheme } from '@/theme/theme-provider';
import { radius, spacing, typography } from '@/theme/tokens';
import { Button } from './button';

export function EmptyNote({ title, body }: { title: string; body?: string }) {
  const { colors } = useTheme();
  return (
    <View style={[styles.empty, { borderColor: colors.border }]}>
      <Text style={[typography.bodyStrong, { color: colors.text }]}>{title}</Text>
      {body ? <Text style={[typography.body, styles.centre, { color: colors.textSecondary }]}>{body}</Text> : null}
    </View>
  );
}

export function ErrorNote({ message, onRetry }: { message: string; onRetry?: () => void }) {
  const { colors } = useTheme();
  return (
    <View
      style={[styles.error, { borderColor: colors.border, backgroundColor: colors.card }]}
      accessibilityRole="alert"
    >
      <Text style={[typography.body, { color: colors.critical }]}>{message}</Text>
      {onRetry ? <Button label="Try again" variant="secondary" onPress={onRetry} /> : null}
    </View>
  );
}

export function SectionTitle({ title, right }: { title: string; right?: React.ReactNode }) {
  const { colors } = useTheme();
  return (
    <View style={styles.section}>
      <Text style={[typography.title, styles.grow, { color: colors.text }]} accessibilityRole="header">
        {title}
      </Text>
      {right}
    </View>
  );
}

const styles = StyleSheet.create({
  grow: { flex: 1 },
  centre: { textAlign: 'center' },
  empty: {
    alignItems: 'center',
    gap: spacing.xs,
    paddingVertical: spacing.xl,
    paddingHorizontal: spacing.lg,
    borderWidth: 1,
    borderStyle: 'dashed',
    borderRadius: radius.lg,
  },
  error: { gap: spacing.md, padding: spacing.lg, borderWidth: StyleSheet.hairlineWidth, borderRadius: radius.lg },
  section: { flexDirection: 'row', alignItems: 'center', marginTop: spacing.md },
});
