// Navigation chrome: slim top bar on laptops/desktops, plain bottom tab bar on phones.
import React from 'react';
import { Pressable, StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useFleet } from '../contexts/FleetContext';
import { useTheme } from '../contexts/ThemeContext';
import { spacing, typography } from '../theme/tokens';
import { useLayout } from '../utils/layout';

export const TAB_META = {
  Live: { icon: 'navigate', label: 'Live' },
  Jobs: { icon: 'clipboard', label: 'Jobs' },
  Alerts: { icon: 'notifications', label: 'Alerts' },
  Invoices: { icon: 'receipt', label: 'Invoices' },
  Fleet: { icon: 'cube', label: 'Fleet' },
  Admin: { icon: 'person-circle', label: 'Admin' },
};

function isLive(status) {
  return /live|connected/i.test(status ?? '') && !/dis|connecting/i.test(status ?? '');
}

export function ConnectionStatus() {
  const { connectionStatus } = useFleet();
  const { colors } = useTheme();
  const live = isLive(connectionStatus);
  return (
    <View style={styles.conn}>
      <View style={[styles.connDot, { backgroundColor: live ? colors.ok : colors.warning }]} />
      <Text style={[typography.caption, { color: colors.textSecondary }]} numberOfLines={1}>
        {live ? 'Live' : 'Reconnecting'}
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
    return { route, focused, meta, onPress, key: route.key, descriptor: descriptors[route.key] };
  });

  if (isWide) {
    return (
      <View style={[styles.topBar, { backgroundColor: colors.card, borderBottomColor: colors.border, paddingTop: insets.top }]}>
        <Text style={[styles.wordmark, { color: colors.text }]}>OmniOps</Text>
        <View style={styles.topLinks}>
          {items.map(({ key, focused, meta, onPress, route }) => (
            <Pressable
              key={key}
              onPress={onPress}
              accessibilityRole="tab"
              accessibilityState={{ selected: focused }}
              style={({ hovered }) => [styles.topLink, (focused || hovered) && { backgroundColor: colors.sunken }]}
            >
              <Text style={[typography.label, { color: focused ? colors.text : colors.textSecondary, fontWeight: focused ? '600' : '500' }]}>
                {meta.label}
              </Text>
              {route.name === 'Alerts' && alertCount > 0 ? (
                <Text style={[typography.caption, { color: colors.critical, fontWeight: '600' }]}>{alertCount}</Text>
              ) : null}
            </Pressable>
          ))}
        </View>
        <View style={styles.topRight}>
          <ConnectionStatus />
          <Pressable
            onPress={onSearch}
            style={({ hovered }) => [styles.searchBtn, { borderColor: colors.border }, hovered && { backgroundColor: colors.sunken }]}
            accessibilityRole="button"
            accessibilityLabel="Search"
          >
            <Ionicons name="search" size={14} color={colors.muted} />
            <Text style={[typography.caption, { color: colors.muted }]}>Search</Text>
            <Text style={[typography.caption, { color: colors.muted, opacity: 0.7 }]}>Ctrl K</Text>
          </Pressable>
          <Pressable onPress={toggle} style={styles.iconBtn} accessibilityRole="button" accessibilityLabel="Toggle dark mode">
            <Ionicons name={isDark ? 'sunny-outline' : 'moon-outline'} size={18} color={colors.textSecondary} />
          </Pressable>
        </View>
      </View>
    );
  }

  return (
    <View style={[styles.bottomBar, { backgroundColor: colors.card, borderTopColor: colors.border, paddingBottom: Math.max(insets.bottom, spacing.sm) }]}>
      {items.map(({ key, focused, meta, onPress, route }) => (
        <Pressable
          key={key}
          onPress={onPress}
          accessibilityRole="tab"
          accessibilityState={{ selected: focused }}
          accessibilityLabel={meta.label}
          style={styles.bottomItem}
        >
          <View>
            <Ionicons name={focused ? meta.icon : `${meta.icon}-outline`} size={22} color={focused ? colors.text : colors.muted} />
            {route.name === 'Alerts' && alertCount > 0 ? (
              <View style={[styles.badgeDot, { backgroundColor: colors.critical, borderColor: colors.card }]} />
            ) : null}
          </View>
          <Text style={[styles.bottomLabel, { color: focused ? colors.text : colors.muted }]}>{meta.label}</Text>
        </Pressable>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  topBar: { height: 56, flexDirection: 'row', alignItems: 'center', paddingHorizontal: spacing.xl, borderBottomWidth: StyleSheet.hairlineWidth, gap: spacing.xl, zIndex: 2000 },
  wordmark: { fontSize: 17, fontWeight: '700', letterSpacing: -0.4 },
  topLinks: { flexDirection: 'row', gap: 2, flex: 1 },
  topLink: { flexDirection: 'row', alignItems: 'center', gap: 6, paddingHorizontal: 12, paddingVertical: 7, borderRadius: 8 },
  topRight: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  conn: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  connDot: { width: 7, height: 7, borderRadius: 4 },
  searchBtn: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm, borderWidth: 1, borderRadius: 8, paddingHorizontal: 10, paddingVertical: 6, minWidth: 180 },
  iconBtn: { padding: 6 },
  bottomBar: { flexDirection: 'row', borderTopWidth: StyleSheet.hairlineWidth, paddingTop: 8 },
  bottomItem: { flex: 1, alignItems: 'center', gap: 3 },
  badgeDot: { position: 'absolute', top: -1, right: -3, width: 9, height: 9, borderRadius: 5, borderWidth: 2 },
  bottomLabel: { fontSize: 10.5, fontWeight: '500' },
});
