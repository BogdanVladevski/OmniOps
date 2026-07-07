import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useFleet } from '../../contexts/FleetContext';
import { useTheme } from '../../contexts/ThemeContext';
import { spacing, typography } from '../../theme/tokens';

export default function OfflineBanner() {
  const { connectionStatus, syncStatus } = useFleet();
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();

  const isOffline =
    syncStatus === 'offline' ||
    (connectionStatus && !/connected/i.test(connectionStatus) && /cannot reach|offline|missing/i.test(connectionStatus));

  if (!isOffline) return null;

  return (
    <View
      style={[
        styles.banner,
        {
          paddingTop: insets.top + spacing.xs,
          backgroundColor: colors.warning,
        },
      ]}
      accessibilityRole="alert"
      accessibilityLiveRegion="polite"
    >
      <Text style={styles.text}>Offline — showing cached fleet data</Text>
      <Text style={styles.sub} numberOfLines={2}>
        {connectionStatus}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  banner: {
    paddingHorizontal: spacing.lg,
    paddingBottom: spacing.sm,
  },
  text: {
    ...typography.bodyStrong,
    color: '#1C1C1E',
  },
  sub: {
    ...typography.caption,
    color: '#3A3A3C',
    marginTop: 2,
  },
});
