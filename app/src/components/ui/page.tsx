import type { ReactNode } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';
import { useLayout } from '@/hooks/use-layout';
import { useTheme } from '@/theme/theme-provider';
import { spacing, typography } from '@/theme/tokens';
import { Screen } from './screen';

/** A scrollable page with a readable maximum width and an optional title row with actions. */
export function Page({ title, actions, children }: { title?: string; actions?: ReactNode; children: ReactNode }) {
  const { colors } = useTheme();
  const { horizontalPadding, isWide } = useLayout();
  return (
    <Screen edges={isWide ? ['left', 'right'] : ['top', 'left', 'right']}>
      <ScrollView contentContainerStyle={styles.scroll} showsVerticalScrollIndicator={false}>
        <View style={[styles.inner, { paddingHorizontal: horizontalPadding }]}>
          {title ? (
            <View style={styles.header}>
              <Text style={[isWide ? typography.display : typography.headline, styles.title, { color: colors.text }]}>
                {title}
              </Text>
              {actions ? <View style={styles.actions}>{actions}</View> : null}
            </View>
          ) : null}
          {children}
        </View>
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  scroll: { paddingBottom: spacing.xxxl },
  inner: { width: '100%', maxWidth: 1080, alignSelf: 'center', paddingTop: spacing.xl, gap: spacing.lg },
  header: { flexDirection: 'row', alignItems: 'center', flexWrap: 'wrap', gap: spacing.md },
  title: { flex: 1 },
  actions: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
});
