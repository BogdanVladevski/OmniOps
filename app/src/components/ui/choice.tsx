import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '@/theme/theme-provider';
import { radius, spacing, typography } from '@/theme/tokens';

export interface ChoiceOption {
  id: string;
  label: string;
  detail?: string | null;
}

/** Pick one from a few (trucks, drivers, presets). Tapping the chosen one again clears it unless `required`. */
export function Choice({
  options,
  value,
  onChange,
  emptyText,
  required,
}: {
  options: ChoiceOption[];
  value: string | null;
  onChange: (id: string | null) => void;
  emptyText?: string;
  required?: boolean;
}) {
  const { colors } = useTheme();
  if (!options.length) return <Text style={[typography.caption, { color: colors.muted }]}>{emptyText}</Text>;
  return (
    <View style={styles.choices} accessibilityRole="radiogroup">
      {options.map((o) => {
        const active = o.id === value;
        return (
          <Pressable
            key={o.id}
            onPress={() => onChange(active && !required ? null : o.id)}
            accessibilityRole="radio"
            accessibilityState={{ selected: active }}
            style={[
              styles.choice,
              {
                borderColor: active ? colors.text : colors.border,
                backgroundColor: active ? colors.sunken : colors.card,
              },
            ]}
          >
            <Text style={[typography.label, { color: colors.text, fontWeight: active ? '600' : '500' }]}>
              {o.label}
            </Text>
            {o.detail ? <Text style={[typography.caption, { color: colors.muted }]}>{o.detail}</Text> : null}
          </Pressable>
        );
      })}
    </View>
  );
}

const styles = StyleSheet.create({
  choices: { flexDirection: 'row', flexWrap: 'wrap', gap: spacing.sm },
  choice: {
    borderWidth: 1,
    borderRadius: radius.md,
    paddingHorizontal: spacing.md,
    paddingVertical: spacing.sm,
    minWidth: 110,
  },
});
