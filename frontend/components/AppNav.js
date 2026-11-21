// Navigation chrome: a labelled sidebar on laptops/desktops, a bottom tab bar on phones.
import React from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useFleet } from '../contexts/FleetContext';
import { useTheme } from '../contexts/ThemeContext';
import { radius, spacing, typography } from '../theme/tokens';
import { useLayout } from '../utils/layout';

export const TAB_META = {
  Overview: { icon: 'grid', label: 'Overview', hint: 'What is happening right now' },
  Map: { icon: 'map', label: 'Live map', hint: 'Where every truck is' },
  Alerts: { icon: 'notifications', label: 'Alerts', hint: 'Incidents that need attention' },
  Analytics: { icon: 'stats-chart', label: 'Analytics', hint: 'Trends and predictions' },
  Fleet: { icon: 'bus', label: 'Fleet', hint: 'Vehicles, drivers, copilot' },
  Admin: { icon: 'settings', label: 'Admin', hint: 'Organisation and access' },
};

function isLive(status) {
  return /live|connected/i.test(status ?? '') && !/dis|connecting/i.test(status ?? '');
}

export function ConnectionPill({ dark }) {
  const { connectionStatus } = useFleet();
  const { colors } = useTheme();
  const live = isLive(connectionStatus);
  const fg = live ? colors.ok : colors.warning;
  return (
    <View style={[styles.conn, { backgroundColor: dark ? 'rgba(255,255,255,0.06)' : colors.sunken }]}>
      <View style={[styles.connDot, { backgroundColor: fg }]} />
      <Text style={[typography.caption, { color: dark ? colors.sidebarText : colors.textSecondary }]} numberOfLines={1}>
        {live ? 'Live data connected' : connectionStatus}
      </Text>
    </View>
  );
}

export default function AppNav({ state, descriptors, navigation, onSearch, alertCount = 0 }) {
  const { colors, isDark, toggle } = useTheme();
  const { isWide } = useLayout();
  const insets = useSafeAreaInsets();

  const items = state.routes.map((route, index) => {
    const focused = state.index === index;
    const meta = TAB_META[route.name] ?? { icon: 'ellipse', label: route.name };
    const onPress = () => {
      const event = navigation.emit({ type: 'tabPress', target: route.key, canPreventDefault: true });
      if (!focused && !event.defaultPrevented) navigation.navigate(route.name);
    };
    return { route, focused, meta, onPress, options: descriptors[route.key].options };
  });

  if (isWide) {
    return (
      <View style={[styles.sidebar, { backgroundColor: colors.sidebar, paddingTop: insets.top + spacing.xl }]}>
        <View style={styles.brand}>
          <View style={[styles.logo, { backgroundColor: colors.accent }]}>
            <Ionicons name="snow" size={20} color={colors.onAccent} />
          </View>
          <View>
            <Text style={[typography.title, { color: '#FFFFFF' }]}>OmniOps</Text>
            <Text style={[typography.caption, { color: colors.sidebarText }]}>Cold-chain monitoring</Text>
          </View>
        </View>

        <Pressable
          onPress={onSearch}
          style={({ hovered }) => [styles.search, { backgroundColor: hovered ? colors.sidebarActive : 'rgba(255,255,255,0.05)' }]}
          accessibilityRole="button"
          accessibilityLabel="Search and quick actions"
        >
          <Ionicons name="search" size={15} color={colors.sidebarText} />
          <Text style={[typography.caption, styles.flex, { color: colors.sidebarText }]}>Search & actions</Text>
          <Text style={[typography.caption, styles.kbd, { color: colors.sidebarText }]}>Ctrl K</Text>
        </Pressable>

        <View style={styles.navList}>
          {items.map(({ route, focused, meta, onPress }) => (
            <Pressable
              key={route.key}
              onPress={onPress}
              accessibilityRole="tab"
              accessibilityState={{ selected: focused }}
              style={({ hovered }) => [
                styles.navItem,
                (focused || hovered) && { backgroundColor: colors.sidebarActive },
              ]}
            >
              {focused ? <View style={[styles.navIndicator, { backgroundColor: colors.accent }]} /> : null}
              <Ionicons name={focused ? meta.icon : `${meta.icon}-outline`} size={19} color={focused ? colors.accent : colors.sidebarText} />
              <Text style={[typography.bodyStrong, styles.flex, { color: focused ? '#FFFFFF' : colors.sidebarText }]}>
                {meta.label}
              </Text>
              {route.name === 'Alerts' && alertCount > 0 ? (
                <View style={[styles.badge, { backgroundColor: colors.critical }]}>
                  <Text style={styles.badgeText}>{alertCount > 99 ? '99+' : alertCount}</Text>
                </View>
              ) : null}
            </Pressable>
          ))}
        </View>

        <View style={styles.sidebarFooter}>
          <ConnectionPill dark />
          <Pressable
            onPress={toggle}
            style={({ hovered }) => [styles.navItem, hovered && { backgroundColor: colors.sidebarActive }]}
            accessibilityRole="button"
            accessibilityLabel="Toggle dark mode"
          >
            <Ionicons name={isDark ? 'sunny-outline' : 'moon-outline'} size={18} color={colors.sidebarText} />
            <Text style={[typography.body, { color: colors.sidebarText }]}>{isDark ? 'Light mode' : 'Dark mode'}</Text>
          </Pressable>
        </View>
      </View>
    );
  }

  return (
    <View
      style={[
        styles.bottomBar,
        { backgroundColor: colors.card, borderTopColor: colors.border, paddingBottom: Math.max(insets.bottom, spacing.sm) },
      ]}
    >
      {items.map(({ route, focused, meta, onPress }) => (
        <Pressable
          key={route.key}
          onPress={onPress}
          accessibilityRole="tab"
          accessibilityState={{ selected: focused }}
          accessibilityLabel={meta.label}
          style={styles.bottomItem}
        >
          <View style={[styles.bottomIconWrap, focused && { backgroundColor: colors.accentMuted }]}>
            <Ionicons name={focused ? meta.icon : `${meta.icon}-outline`} size={20} color={focused ? colors.accentStrong : colors.muted} />
            {route.name === 'Alerts' && alertCount > 0 ? <View style={[styles.dotBadge, { backgroundColor: colors.critical, borderColor: colors.card }]} /> : null}
          </View>
          <Text style={[styles.bottomLabel, { color: focused ? colors.accentStrong : colors.muted }]} numberOfLines={1}>
            {meta.label}
          </Text>
        </Pressable>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  sidebar: { width: 248, paddingHorizontal: spacing.md, paddingBottom: spacing.lg },
  brand: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.sm, marginBottom: spacing.xl },
  logo: { width: 38, height: 38, borderRadius: radius.md, alignItems: 'center', justifyContent: 'center' },
  search: {
    flexDirection: 'row', alignItems: 'center', gap: spacing.sm, borderRadius: radius.md,
    paddingHorizontal: spacing.md, paddingVertical: 9, marginBottom: spacing.lg,
  },
  kbd: { opacity: 0.7 },
  navList: { gap: 2, flex: 1 },
  navItem: {
    flexDirection: 'row', alignItems: 'center', gap: spacing.md, borderRadius: radius.md,
    paddingHorizontal: spacing.md, paddingVertical: 10,
  },
  navIndicator: { position: 'absolute', left: 0, top: 10, bottom: 10, width: 3, borderRadius: 2 },
  badge: { minWidth: 22, paddingHorizontal: 6, height: 20, borderRadius: 10, alignItems: 'center', justifyContent: 'center' },
  badgeText: { color: '#FFFFFF', fontSize: 11, fontWeight: '700' },
  sidebarFooter: { gap: spacing.sm },
  conn: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, borderRadius: radius.md, paddingHorizontal: spacing.md, paddingVertical: 8 },
  connDot: { width: 8, height: 8, borderRadius: 4 },
  bottomBar: { flexDirection: 'row', borderTopWidth: StyleSheet.hairlineWidth, paddingTop: 6 },
  bottomItem: { flex: 1, alignItems: 'center', gap: 2 },
  bottomIconWrap: { width: 44, height: 28, borderRadius: radius.pill, alignItems: 'center', justifyContent: 'center' },
  dotBadge: { position: 'absolute', top: 2, right: 8, width: 9, height: 9, borderRadius: 5, borderWidth: 2 },
  bottomLabel: { fontSize: 11, fontWeight: '600' },
});
