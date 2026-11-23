import { Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '@/theme/theme-provider';
import { radius, typography } from '@/theme/tokens';

export interface SegmentOption<T extends string> {
  key: T;
  label: string;
  count?: number;
}

/** A segmented control for switching views or picking one of a few options. */
export function Segmented<T extends string>({
  options,
  value,
  onChange,
}: {
  options: SegmentOption<T>[];
  value: T;
  onChange: (key: T) => void;
}) {
  const { colors, isDark } = useTheme();
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false}>
      <View style={[styles.track, { backgroundColor: colors.sunken }]} accessibilityRole="tablist">
        {options.map((o) => {
          const active = o.key === value;
          return (
            <Pressable
              key={o.key}
              onPress={() => onChange(o.key)}
              accessibilityRole="tab"
              accessibilityState={{ selected: active }}
              style={[styles.segment, active && { backgroundColor: isDark ? colors.elevated : colors.card }]}
            >
              <Text
                style={[
                  typography.label,
                  { color: active ? colors.text : colors.textSecondary, fontWeight: active ? '600' : '500' },
                ]}
              >
                {o.label}
                {o.count != null ? <Text style={{ color: colors.muted }}>{`  ${o.count}`}</Text> : null}
              </Text>
            </Pressable>
          );
        })}
      </View>
    </ScrollView>
  );
}

const styles = StyleSheet.create({
  track: { flexDirection: 'row', borderRadius: radius.md, padding: 3, gap: 2 },
  segment: { paddingHorizontal: 14, paddingVertical: 6, borderRadius: 8 },
});
