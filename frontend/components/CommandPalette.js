import React, { useMemo, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  TextInput,
  Pressable,
  FlatList,
  Platform,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useFleet } from '../contexts/FleetContext';
import { useTheme } from '../contexts/ThemeContext';
import { demoApi } from '../utils/api';
import { radius, spacing, typography } from '../theme/tokens';

const TAB_ROUTES = [
  { id: 'map', label: 'Open Map', tab: 'Map', keywords: 'map live tracking' },
  { id: 'fleet', label: 'Fleet Dashboard', tab: 'Fleet', keywords: 'dashboard kpi executive' },
  { id: 'manage', label: 'Fleet Management', tab: 'Manage', keywords: 'vehicles drivers assign' },
  { id: 'analytics', label: 'Analytics', tab: 'Analytics', keywords: 'predictions safety trends' },
  { id: 'alerts', label: 'Alerts', tab: 'Alerts', keywords: 'incidents playbook' },
  { id: 'admin', label: 'Admin', tab: 'Admin', keywords: 'api keys audit logs' },
];

export default function CommandPalette({ visible, onClose, navigationRef, onAbout, onBootstrap }) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const { dbVehicles, vehicles, setSelectedVehicleId, refreshFleetMeta } = useFleet();
  const [query, setQuery] = useState('');
  const [busy, setBusy] = useState(false);

  const items = useMemo(() => {
    const q = query.trim().toLowerCase();
    const vehicleItems = (dbVehicles.length ? dbVehicles : Object.keys(vehicles).map((id) => ({ externalId: id, id })))
      .map((v) => ({
        id: `vehicle-${v.externalId ?? v.id}`,
        label: v.externalId ?? v.id ?? 'Vehicle',
        subtitle: v.make ? `${v.make} ${v.model ?? ''}`.trim() : 'Select on map',
        keywords: `vehicle ${v.externalId} ${v.make} ${v.model}`,
        action: 'vehicle',
        vehicleId: v.externalId ?? v.id,
      }));

    const actions = [
      { id: 'refresh', label: 'Refresh fleet data', subtitle: 'Reload statistics and vehicles', keywords: 'refresh sync reload', action: 'refresh' },
      { id: 'bootstrap', label: 'Bootstrap demo telemetry', subtitle: 'Queue sample packets for all vehicles', keywords: 'demo seed sample bootstrap', action: 'bootstrap' },
      { id: 'about', label: 'About OmniOps', subtitle: 'Version and environment', keywords: 'about version info', action: 'about' },
    ];

    const all = [...TAB_ROUTES.map((t) => ({ ...t, action: 'tab', subtitle: `Navigate to ${t.tab}` })), ...vehicleItems, ...actions];
    if (!q) return all.slice(0, 12);
    return all.filter(
      (item) =>
        item.label.toLowerCase().includes(q) ||
        item.subtitle?.toLowerCase().includes(q) ||
        item.keywords?.toLowerCase().includes(q),
    );
  }, [query, dbVehicles, vehicles]);

  const run = async (item) => {
    if (busy) return;
    setBusy(true);
    try {
      if (item.action === 'tab' && navigationRef?.current) {
        navigationRef.current.navigate(item.tab);
        onClose();
      } else if (item.action === 'vehicle') {
        setSelectedVehicleId(item.vehicleId);
        navigationRef?.current?.navigate('Map');
        onClose();
      } else if (item.action === 'refresh') {
        await refreshFleetMeta();
        onClose();
      } else if (item.action === 'bootstrap') {
        await demoApi.bootstrap(6);
        onBootstrap?.();
        onClose();
      } else if (item.action === 'about') {
        onAbout?.();
        onClose();
      }
    } catch {
      // palette closes on success paths only
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal visible={visible} animationType="fade" transparent onRequestClose={onClose}>
      <Pressable style={[styles.backdrop, { backgroundColor: colors.overlay }]} onPress={onClose}>
        <Pressable
          style={[
            styles.sheet,
            {
              marginTop: insets.top + spacing.xl,
              backgroundColor: colors.card,
              borderColor: colors.border,
            },
          ]}
          onPress={(e) => e.stopPropagation()}
        >
          <TextInput
            value={query}
            onChangeText={setQuery}
            placeholder="Search vehicles, screens, actions…"
            placeholderTextColor={colors.muted}
            style={[styles.input, { color: colors.text, borderColor: colors.border }]}
            autoFocus
            returnKeyType="search"
            accessibilityLabel="Command palette search"
          />
          <FlatList
            data={items}
            keyExtractor={(item) => item.id}
            keyboardShouldPersistTaps="handled"
            style={{ maxHeight: 360 }}
            renderItem={({ item }) => (
              <Pressable
                onPress={() => run(item)}
                style={({ pressed }) => [
                  styles.row,
                  { backgroundColor: pressed ? colors.accentMuted : 'transparent' },
                ]}
              >
                <Text style={[styles.rowLabel, { color: colors.text }]}>{item.label}</Text>
                {item.subtitle ? (
                  <Text style={[styles.rowSub, { color: colors.textSecondary }]} numberOfLines={1}>
                    {item.subtitle}
                  </Text>
                ) : null}
              </Pressable>
            )}
            ListEmptyComponent={
              <Text style={[styles.empty, { color: colors.muted }]}>No matches</Text>
            }
          />
          {Platform.OS === 'web' ? (
            <Text style={[styles.hint, { color: colors.muted }]}>Tip: Ctrl+K to open</Text>
          ) : null}
        </Pressable>
      </Pressable>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: { flex: 1, paddingHorizontal: spacing.lg },
  sheet: {
    borderRadius: radius.lg,
    borderWidth: StyleSheet.hairlineWidth,
    overflow: 'hidden',
  },
  input: {
    ...typography.body,
    padding: spacing.lg,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  row: { paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  rowLabel: { ...typography.bodyStrong },
  rowSub: { ...typography.caption, marginTop: 2 },
  empty: { padding: spacing.lg, textAlign: 'center' },
  hint: { ...typography.caption, padding: spacing.sm, textAlign: 'center' },
});
