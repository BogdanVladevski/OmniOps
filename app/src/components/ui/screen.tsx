import type { ReactNode } from 'react';
import { StyleSheet, type StyleProp, type ViewStyle } from 'react-native';
import { SafeAreaView, type Edge } from 'react-native-safe-area-context';
import { useTheme } from '@/theme/theme-provider';

export function Screen({
  children,
  edges = ['top', 'left', 'right'],
  style,
}: {
  children: ReactNode;
  edges?: Edge[];
  style?: StyleProp<ViewStyle>;
}) {
  const { colors } = useTheme();
  return (
    <SafeAreaView edges={edges} style={[styles.screen, { backgroundColor: colors.background }, style]}>
      {children}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({ screen: { flex: 1 } });
