import { StyleSheet, Text, TextInput, View, type TextInputProps } from 'react-native';
import { useTheme } from '@/theme/theme-provider';
import { radius, spacing, typography } from '@/theme/tokens';

interface FieldProps extends TextInputProps {
  label?: string;
  /** Shown under the input in red. */
  error?: string | null;
}

export function Field({ label, error, style, multiline, ...props }: FieldProps) {
  const { colors } = useTheme();
  return (
    <View style={styles.field}>
      {label ? <Text style={[typography.label, { color: colors.textSecondary }]}>{label}</Text> : null}
      <TextInput
        accessibilityLabel={label}
        placeholderTextColor={colors.muted}
        multiline={multiline}
        style={[
          styles.input,
          { borderColor: error ? colors.critical : colors.border, backgroundColor: colors.card, color: colors.text },
          multiline && styles.multiline,
          style,
        ]}
        {...props}
      />
      {error ? <Text style={[typography.caption, { color: colors.critical }]}>{error}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  field: { gap: 6 },
  input: { borderWidth: 1, borderRadius: radius.md, paddingHorizontal: spacing.md, paddingVertical: 10, fontSize: 15 },
  multiline: { minHeight: 72, textAlignVertical: 'top' },
});
