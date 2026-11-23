import { StyleSheet, Text, View } from 'react-native';
import { useTheme } from '@/theme/theme-provider';
import { toneColors, typography, type Tone } from '@/theme/tokens';

/** Status as a coloured dot and text: no filled badges. */
export function Status({ label, tone = 'neutral' }: { label: string; tone?: Tone }) {
  const { colors } = useTheme();
  const { fg } = toneColors(colors, tone);
  return (
    <View style={styles.status}>
      <View style={[styles.dot, { backgroundColor: fg }]} />
      <Text style={[typography.label, { color: tone === 'neutral' ? colors.textSecondary : fg }]} numberOfLines={1}>
        {label}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  status: { flexDirection: 'row', alignItems: 'center', gap: 6, alignSelf: 'flex-start' },
  dot: { width: 7, height: 7, borderRadius: 4 },
});
