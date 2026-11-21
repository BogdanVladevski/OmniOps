import React, { useMemo, useState } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useFleet } from '../contexts/FleetContext';
import { useTheme } from '../contexts/ThemeContext';
import { radius, spacing, toneColors, typography } from '../theme/tokens';
import { alertSummary, groupAlerts, SEVERITY_LABEL } from '../utils/alerts';
import { resolveShipmentIdForVehicle } from '../utils/fleetConfig';
import { useLayout } from '../utils/layout';
import IncidentReplay from './IncidentReplay';
import { Button, Card, EmptyNote, Page, Pill, Segmented, timeAgo } from './ui/kit';

const FILTERS = [
  { key: 'all', label: 'All' },
  { key: 'critical', label: 'Critical' },
  { key: 'warning', label: 'Warnings' },
  { key: 'info', label: 'Info' },
];

function AlertRow({ group, onPress, first }) {
  const { colors } = useTheme();
  const t = toneColors(colors, group.meta.severity);
  return (
    <Pressable
      onPress={onPress}
      accessibilityRole="button"
      style={({ hovered, pressed }) => [
        styles.row,
        !first && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border },
        (hovered || pressed) && { backgroundColor: colors.sunken },
      ]}
    >
      <View style={[styles.stripe, { backgroundColor: t.fg }]} />
      <View style={[styles.icon, { backgroundColor: t.bg }]}>
        <Ionicons name={group.meta.icon} size={18} color={t.fg} />
      </View>
      <View style={styles.flex}>
        <View style={styles.rowHead}>
          <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>
            {group.meta.label}
          </Text>
          {group.count > 1 ? (
            <View style={[styles.count, { backgroundColor: colors.sunken }]}>
              <Text style={[typography.caption, { color: colors.textSecondary, fontWeight: '700' }]}>×{group.count}</Text>
            </View>
          ) : null}
        </View>
        <Text style={[typography.caption, { color: colors.textSecondary }]} numberOfLines={2}>
          {group.alert.vehicleId} · {alertSummary(group.alert) || group.alert.title}
        </Text>
      </View>
      <View style={styles.rowEnd}>
        <Pill label={SEVERITY_LABEL[group.meta.severity]} tone={group.meta.severity} />
        <Text style={[typography.caption, { color: colors.muted }]}>{timeAgo(group.alert.generatedAt)}</Text>
      </View>
    </Pressable>
  );
}

export default function AlertsFeed() {
  const { alerts, vehicles } = useFleet();
  const { colors } = useTheme();
  const { isWide } = useLayout();
  const insets = useSafeAreaInsets();
  const [filter, setFilter] = useState('all');
  const [selected, setSelected] = useState(null);
  const [replayAlert, setReplayAlert] = useState(null);

  const groups = useMemo(() => groupAlerts(alerts), [alerts]);
  const counts = useMemo(() => {
    const c = { all: groups.length, critical: 0, warning: 0, info: 0 };
    groups.forEach((g) => { c[g.meta.severity] += 1; });
    return c;
  }, [groups]);
  // Most severe first, newest first within a severity.
  const rank = { critical: 0, warning: 1, info: 2 };
  const visible = (filter === 'all' ? groups : groups.filter((g) => g.meta.severity === filter))
    .slice()
    .sort((a, b) => rank[a.meta.severity] - rank[b.meta.severity]);

  const apiBaseUrl = process.env.EXPO_PUBLIC_API_URL;
  const apiToken = process.env.EXPO_PUBLIC_API_TOKEN;
  const replayShipmentId = replayAlert ? resolveShipmentIdForVehicle(replayAlert.vehicleId, vehicles) : null;
  const sel = selected ? toneColors(colors, selected.meta.severity) : null;

  return (
    <Page
      title="Alerts"
      subtitle="Everything that needs a person's attention. Critical alerts mean medicine is outside its safe temperature; repeated events are grouped so nothing important gets buried."
    >
      <Segmented
        options={FILTERS.map((f) => ({ ...f, count: counts[f.key] }))}
        value={filter}
        onChange={setFilter}
      />
      <View style={{ height: spacing.md }} />

      {visible.length === 0 ? (
        <EmptyNote
          icon="notifications-off-outline"
          title={alerts.length === 0 ? 'No alerts yet' : 'Nothing in this category'}
          body={alerts.length === 0 ? 'Alerts appear here in real time when a truck or shipment needs attention.' : undefined}
        />
      ) : (
        <Card padded={false} style={styles.list}>
          {visible.map((g, i) => (
            <AlertRow key={g.key} group={g} first={i === 0} onPress={() => setSelected(g)} />
          ))}
        </Card>
      )}

      <Modal visible={!!selected} animationType="fade" transparent onRequestClose={() => setSelected(null)}>
        <Pressable style={[styles.backdrop, { backgroundColor: colors.overlay }, isWide && styles.backdropCentered]} onPress={() => setSelected(null)}>
          <Pressable
            style={[
              styles.sheet,
              { backgroundColor: colors.card, paddingBottom: Math.max(insets.bottom, spacing.lg) + spacing.sm },
              isWide && styles.sheetWide,
            ]}
            onPress={() => {}}
          >
            {selected ? (
              <>
                <View style={styles.sheetHead}>
                  <View style={[styles.icon, { backgroundColor: sel.bg }]}>
                    <Ionicons name={selected.meta.icon} size={18} color={sel.fg} />
                  </View>
                  <View style={styles.flex}>
                    <Text style={[typography.title, { color: colors.text }]}>{selected.meta.label}</Text>
                    <Text style={[typography.caption, { color: colors.muted }]}>
                      {selected.alert.vehicleId} · {new Date(selected.alert.generatedAt).toLocaleString()}
                      {selected.count > 1 ? ` · repeated ${selected.count}×` : ''}
                    </Text>
                  </View>
                  <Pill label={SEVERITY_LABEL[selected.meta.severity]} tone={selected.meta.severity} />
                </View>
                <ScrollView style={[styles.sheetBody, { backgroundColor: colors.sunken }]}>
                  <Text style={[typography.body, { color: colors.text }]}>
                    {selected.alert.instructions || selected.alert.title}
                  </Text>
                </ScrollView>
                <View style={styles.sheetActions}>
                  <Button label="Close" variant="secondary" onPress={() => setSelected(null)} style={styles.flex} />
                  <Button
                    label="Replay incident"
                    icon="play"
                    onPress={() => { setReplayAlert(selected.alert); setSelected(null); }}
                    style={styles.flex}
                  />
                </View>
              </>
            ) : null}
          </Pressable>
        </Pressable>
      </Modal>

      <IncidentReplay
        visible={!!replayAlert}
        alert={replayAlert}
        shipmentId={replayShipmentId}
        apiBaseUrl={apiBaseUrl}
        apiToken={apiToken}
        onClose={() => setReplayAlert(null)}
      />
    </Page>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  list: { overflow: 'hidden' },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingVertical: spacing.md, paddingHorizontal: spacing.lg },
  stripe: { position: 'absolute', left: 0, top: 0, bottom: 0, width: 3 },
  icon: { width: 36, height: 36, borderRadius: radius.md, alignItems: 'center', justifyContent: 'center' },
  rowHead: { flexDirection: 'row', alignItems: 'center', gap: spacing.sm },
  rowEnd: { alignItems: 'flex-end', gap: 6 },
  count: { paddingHorizontal: 6, paddingVertical: 1, borderRadius: radius.sm },
  backdrop: { flex: 1, justifyContent: 'flex-end' },
  backdropCentered: { justifyContent: 'center', alignItems: 'center' },
  sheet: { borderTopLeftRadius: radius.xl, borderTopRightRadius: radius.xl, padding: spacing.xl, gap: spacing.lg, maxHeight: '85%' },
  sheetWide: { borderRadius: radius.xl, width: 560, maxWidth: '92%' },
  sheetHead: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  sheetBody: { borderRadius: radius.md, padding: spacing.lg, maxHeight: 360 },
  sheetActions: { flexDirection: 'row', gap: spacing.md },
});
