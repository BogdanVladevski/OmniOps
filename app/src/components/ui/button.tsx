import { Pressable, StyleSheet, Text, type StyleProp, type ViewStyle } from 'react-native';
import { useTheme } from '@/theme/theme-provider';
import { isActive } from './pressable-state';
import { radius, spacing, typography } from '@/theme/tokens';

interface ButtonProps {
  label: string;
  onPress?: () => void;
  variant?: 'primary' | 'secondary';
  disabled?: boolean;
  /** Shows "Please wait…" and blocks presses while an action runs. */
  busy?: boolean;
  style?: StyleProp<ViewStyle>;
}

export function Button({ label, onPress, variant = 'primary', disabled, busy, style }: ButtonProps) {
  const { colors } = useTheme();
  const primary = variant === 'primary';
  const off = disabled || busy;
  return (
    <Pressable
      onPress={onPress}
      disabled={off}
      accessibilityRole="button"
      accessibilityState={{ disabled: !!off, busy: !!busy }}
      style={(state) => [
        styles.button,
        primary
          ? { backgroundColor: colors.primary }
          : { borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card },
        isActive(state) && { opacity: 0.82 },
        off && { opacity: 0.45 },
        style,
      ]}
    >
      <Text style={[typography.label, styles.label, { color: primary ? colors.onPrimary : colors.text }]}>
        {busy ? 'Please wait…' : label}
      </Text>
    </Pressable>
  );
}

/** A text action ("Change phone", "Remove"): quieter than a button. */
export function TextAction({
  label,
  onPress,
  tone = 'accent',
}: {
  label: string;
  onPress: () => void;
  tone?: 'accent' | 'critical';
}) {
  const { colors } = useTheme();
  return (
    <Pressable onPress={onPress} accessibilityRole="button" hitSlop={8}>
      <Text style={[typography.label, { color: tone === 'critical' ? colors.critical : colors.accent }]}>{label}</Text>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: {
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: spacing.lg,
    paddingVertical: 9,
    borderRadius: radius.md,
    minHeight: 40,
  },
  label: { fontWeight: '600' },
});
