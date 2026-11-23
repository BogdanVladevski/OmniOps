// Navigation chrome: a slim top bar on laptops, a plain bottom tab bar on phones.
import { Ionicons } from '@expo/vector-icons';
import type { BottomTabBarProps } from 'expo-router/tabs';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { isActive } from '@/components/ui/pressable-state';
import { useLive } from '@/features/realtime/live-updates';
import { useLayout } from '@/hooks/use-layout';
import { useTheme } from '@/theme/theme-provider';
import { spacing, typography } from '@/theme/tokens';

type IconName = React.ComponentProps<typeof Ionicons>['name'];
export const TABS: Record<string, { label: string; icon: IconName; iconActive: IconName }> = {
  index: { label: 'Today', icon: 'today-outline', iconActive: 'today' },
  jobs: { label: 'Jobs', icon: 'clipboard-outline', iconActive: 'clipboard' },
  customers: { label: 'Customers', icon: 'people-outline', iconActive: 'people' },
  invoices: { label: 'Invoices', icon: 'receipt-outline', iconActive: 'receipt' },
  fleet: { label: 'Fleet', icon: 'cube-outline', iconActive: 'cube' },
  settings: { label: 'Settings', icon: 'settings-outline', iconActive: 'settings' },
};

function LiveStatus() {
  const { state } = useLive();
  const { colors } = useTheme();
  const live = state === 'live';
  return (
    <View style={styles.live} accessibilityLabel={live ? 'Live updates on' : 'Reconnecting'}>
      <View style={[styles.liveDot, { backgroundColor: live ? colors.ok : colors.warning }]} />
      <Text style={[typography.caption, { color: colors.textSecondary }]}>{live ? 'Live' : 'Reconnecting'}</Text>
    </View>
  );
}

export function OfficeNav({ state, emitter, navigateToTab, onSearch }: BottomTabBarProps & { onSearch: () => void }) {
  const { colors, isDark, setMode } = useTheme();
  const { isWide } = useLayout();
  const insets = useSafeAreaInsets();

  const items = state.routes.map((route, index) => {
    const focused = state.index === index;
    const meta = TABS[route.name] ?? { label: route.name, icon: 'ellipse-outline', iconActive: 'ellipse' };
    const onPress = () => {
      const event = emitter.emit({ type: 'tabPress', target: route.key, canPreventDefault: true });
      if (!focused && !event.defaultPrevented) navigateToTab(route.key);
    };
    return { key: route.key, focused, meta, onPress };
  });

  if (isWide) {
    return (
      <View
        style={[styles.top, { backgroundColor: colors.card, borderBottomColor: colors.border, paddingTop: insets.top }]}
      >
        <Text style={[styles.wordmark, { color: colors.text }]}>OmniOps</Text>
        <View style={styles.topLinks} accessibilityRole="tablist">
          {items.map(({ key, focused, meta, onPress }) => (
            <Pressable
              key={key}
              onPress={onPress}
              accessibilityRole="tab"
              accessibilityState={{ selected: focused }}
              style={(s) => [styles.topLink, (focused || isActive(s)) && { backgroundColor: colors.sunken }]}
            >
              <Text
                style={[
                  typography.label,
                  { color: focused ? colors.text : colors.textSecondary, fontWeight: focused ? '600' : '500' },
                ]}
              >
                {meta.label}
              </Text>
            </Pressable>
          ))}
        </View>
        <View style={styles.topRight}>
          <LiveStatus />
          <Pressable
            onPress={onSearch}
            accessibilityRole="search"
            accessibilityLabel="Search jobs and invoices"
            style={(s) => [
              styles.search,
              { borderColor: colors.border },
              isActive(s) && { backgroundColor: colors.sunken },
            ]}
          >
            <Ionicons name="search" size={14} color={colors.muted} />
            <Text style={[typography.caption, styles.flex, { color: colors.muted }]}>Search</Text>
            <Text style={[typography.caption, { color: colors.muted }]}>Ctrl K</Text>
          </Pressable>
          <Pressable
            onPress={() => setMode(isDark ? 'light' : 'dark')}
            style={styles.icon}
            accessibilityRole="button"
            accessibilityLabel="Switch light or dark"
          >
            <Ionicons name={isDark ? 'sunny-outline' : 'moon-outline'} size={18} color={colors.textSecondary} />
          </Pressable>
        </View>
      </View>
    );
  }

  return (
    <View
      style={[
        styles.bottom,
        {
          backgroundColor: colors.card,
          borderTopColor: colors.border,
          paddingBottom: Math.max(insets.bottom, spacing.sm),
        },
      ]}
      accessibilityRole="tablist"
    >
      {items.map(({ key, focused, meta, onPress }) => (
        <Pressable
          key={key}
          onPress={onPress}
          accessibilityRole="tab"
          accessibilityState={{ selected: focused }}
          accessibilityLabel={meta.label}
          style={styles.bottomItem}
        >
          <Ionicons
            name={focused ? meta.iconActive : meta.icon}
            size={22}
            color={focused ? colors.text : colors.muted}
          />
          <Text style={[styles.bottomLabel, { color: focused ? colors.text : colors.muted }]}>{meta.label}</Text>
        </Pressable>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  top: {
    height: 56,
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: spacing.xl,
    borderBottomWidth: StyleSheet.hairlineWidth,
    gap: spacing.xl,
    zIndex: 2000,
  },
  wordmark: { fontSize: 17, fontWeight: '700', letterSpacing: -0.4 },
  topLinks: { flexDirection: 'row', gap: 2, flex: 1 },
  topLink: { paddingHorizontal: 12, paddingVertical: 7, borderRadius: 8 },
  topRight: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  live: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  liveDot: { width: 7, height: 7, borderRadius: 4 },
  search: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    borderWidth: 1,
    borderRadius: 8,
    paddingHorizontal: 10,
    paddingVertical: 6,
    width: 200,
  },
  icon: { padding: 6 },
  bottom: { flexDirection: 'row', borderTopWidth: StyleSheet.hairlineWidth, paddingTop: 8 },
  bottomItem: { flex: 1, alignItems: 'center', gap: 3 },
  bottomLabel: { fontSize: 10.5, fontWeight: '500' },
});
