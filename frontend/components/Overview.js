// Home screen: explains what OmniOps does and shows the live state of every shipment.
import React, { useMemo, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useFleet } from '../contexts/FleetContext';
import { useTheme } from '../contexts/ThemeContext';
import { radius, spacing, typography } from '../theme/tokens';
import { alertMeta, alertSummary, groupAlerts } from '../utils/alerts';
import { getVehicleStatus } from '../utils/fleetConfig';
import { useLayout } from '../utils/layout';
import { ConnectionPill } from './AppNav';
import VehicleDetail from './VehicleDetail';
import { Card, EmptyNote, Grid, Page, Pill, SectionTitle, StatTile, TempRangeBar, timeAgo, Button } from './ui/kit';

const STEPS = [
  { icon: 'radio-outline', title: 'Sense', body: 'Each truck streams location and cargo temperature every few seconds.' },
  { icon: 'pulse-outline', title: 'Detect', body: "Every reading is checked against the shipment's own safe temperature range." },
  { icon: 'shield-checkmark-outline', title: 'Respond', body: 'Breaches trigger an alert and a step-by-step response playbook for the crew.' },
];

const money = (n) => `$${Math.round(n ?? 0).toLocaleString()}`;

function shipmentState(t) {
  if (!t) return { tone: 'neutral', label: 'No signal' };
  const s = t.shipment;
  if (s && (t.engineTemperature > s.maxSafeTempCelsius || t.engineTemperature < s.minSafeTempCelsius)) {
    return { tone: 'critical', label: 'Temperature excursion' };
  }
  if (getVehicleStatus(t) === 'warning') return { tone: 'warning', label: 'Needs attention' };
  if (!s) return { tone: 'neutral', label: 'No shipment' };
  return { tone: 'ok', label: 'In safe range' };
}

function HowItWorks({ horizontal }) {
  const { colors } = useTheme();
  return (
    <View style={[styles.steps, horizontal && styles.stepsRow]}>
      {STEPS.map((s, i) => (
        <View key={s.title} style={[styles.step, horizontal && styles.flex]}>
          <View style={[styles.stepIcon, { backgroundColor: colors.accentMuted }]}>
            <Ionicons name={s.icon} size={18} color={colors.accentStrong} />
          </View>
          <View style={styles.flex}>
            <Text style={[typography.bodyStrong, { color: colors.text }]}>{i + 1}. {s.title}</Text>
            <Text style={[typography.caption, { color: colors.textSecondary }]}>{s.body}</Text>
          </View>
        </View>
      ))}
    </View>
  );
}

function ShipmentCard({ vehicleId, telemetry, onPress }) {
  const { colors } = useTheme();
  const state = shipmentState(telemetry);
  const s = telemetry?.shipment;
  return (
    <Card onPress={onPress} accessibilityLabel={`Open ${vehicleId}`} style={styles.shipment}>
      <View style={styles.shipmentHead}>
        <View style={styles.flex}>
          <Text style={[typography.title, { color: colors.text }]} numberOfLines={1}>
            {s?.productName ?? 'No active shipment'}
          </Text>
          <Text style={[typography.caption, { color: colors.muted }]} numberOfLines={1}>
            {s ? `Batch ${s.batchNumber} · ` : ''}{vehicleId}
          </Text>
        </View>
        <Pill label={state.label} tone={state.tone} />
      </View>

      {s ? (
        <TempRangeBar value={telemetry.engineTemperature} min={s.minSafeTempCelsius} max={s.maxSafeTempCelsius} />
      ) : (
        <Text style={[typography.caption, { color: colors.muted }]}>
          {telemetry ? `Cargo ${telemetry.engineTemperature}°C` : 'Waiting for the first reading from this truck.'}
        </Text>
      )}

      <View style={[styles.shipmentFoot, { borderTopColor: colors.border }]}>
        <View style={styles.meta}>
          <Ionicons name="cash-outline" size={14} color={colors.muted} />
          <Text style={[typography.caption, { color: colors.textSecondary }]}>{s ? money(s.valueAtRiskUsd) : '—'}</Text>
        </View>
        <View style={styles.meta}>
          <Ionicons name="speedometer-outline" size={14} color={colors.muted} />
          <Text style={[typography.caption, { color: colors.textSecondary }]}>{telemetry ? `${telemetry.speed} km/h` : '—'}</Text>
        </View>
        <View style={styles.meta}>
          <Ionicons name="time-outline" size={14} color={colors.muted} />
          <Text style={[typography.caption, { color: colors.textSecondary }]}>{telemetry ? timeAgo(telemetry.timestamp) : '—'}</Text>
        </View>
      </View>
    </Card>
  );
}

function CriticalAlerts({ groups, onViewAll }) {
  const { colors } = useTheme();
  if (groups.length === 0) {
    return <EmptyNote title="No critical alerts" body="All shipments are within their safe temperature range." />;
  }
  return (
    <Card padded={false}>
      {groups.slice(0, 4).map((g, i) => (
        <View key={g.key} style={[styles.alertRow, i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }]}>
          <View style={[styles.alertIcon, { backgroundColor: colors.criticalSoft }]}>
            <Ionicons name={g.meta.icon} size={16} color={colors.critical} />
          </View>
          <View style={styles.flex}>
            <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>
              {g.meta.label} · {g.alert.vehicleId}
            </Text>
            <Text style={[typography.caption, { color: colors.textSecondary }]} numberOfLines={2}>{alertSummary(g.alert)}</Text>
          </View>
          <Text style={[typography.caption, { color: colors.muted }]}>{timeAgo(g.alert.generatedAt)}</Text>
        </View>
      ))}
      <View style={[styles.alertFooter, { borderTopColor: colors.border }]}>
        <Button label="View all alerts" icon="arrow-forward" variant="secondary" onPress={onViewAll} />
      </View>
    </Card>
  );
}

export default function Overview({ navigation }) {
  const { vehicleIds, vehicles, alerts, fleetMeta, setSelectedVehicleId } = useFleet();
  const { colors } = useTheme();
  const { isWide, isTablet, isDesktop } = useLayout();
  const [detailId, setDetailId] = useState(null);

  const stats = useMemo(() => {
    const live = vehicleIds.map((id) => vehicles[id]).filter(Boolean);
    const withShipment = live.filter((t) => t.shipment);
    const excursions = withShipment.filter((t) => shipmentState(t).tone === 'critical');
    return {
      live: live.length,
      shipments: withShipment.length,
      inRange: withShipment.length - excursions.length,
      excursions: excursions.length,
      atRisk: excursions.reduce((sum, t) => sum + (t.shipment.valueAtRiskUsd ?? 0), 0),
      protected: withShipment.reduce((sum, t) => sum + (t.shipment.valueAtRiskUsd ?? 0), 0),
    };
  }, [vehicleIds, vehicles]);

  const critical = useMemo(
    () => groupAlerts(alerts).filter((g) => g.meta.severity === 'critical'),
    [alerts],
  );

  const open = (id) => {
    setSelectedVehicleId(id);
    setDetailId(id);
  };

  const shipments = (
    <>
      <SectionTitle title="Shipments in transit" subtitle="Cargo temperature against each product's safe range" />
      <Grid columns={isDesktop ? 2 : isTablet && !isWide ? 2 : 1}>
        {vehicleIds.map((id) => (
          <ShipmentCard key={id} vehicleId={id} telemetry={vehicles[id]} onPress={() => open(id)} />
        ))}
      </Grid>
    </>
  );

  const side = (
    <>
      <SectionTitle title="Needs attention" subtitle="Critical cold-chain alerts" />
      <CriticalAlerts groups={critical} onViewAll={() => navigation.navigate('Alerts')} />
      <SectionTitle title="How OmniOps works" />
      <Card>
        <HowItWorks />
      </Card>
    </>
  );

  return (
    <Page
      title="Cold-chain overview"
      subtitle="OmniOps watches the temperature of every medicine shipment on the road and raises an alert the moment one leaves its safe range — so the crew can act before the cargo is lost."
      actions={isWide ? null : <ConnectionPill />}
    >
      <Grid columns={isTablet ? 4 : 2}>
        <StatTile
          label="On the road"
          value={`${stats.shipments}`}
          hint={`${stats.live} of ${vehicleIds.length} trucks reporting`}
          icon="cube-outline"
          tone="accent"
        />
        <StatTile
          label="In safe range"
          value={stats.shipments ? `${stats.inRange}/${stats.shipments}` : '—'}
          hint="Temperature within limits"
          icon="checkmark-circle-outline"
          tone="ok"
        />
        <StatTile
          label="Excursions now"
          value={`${stats.excursions}`}
          hint={stats.excursions ? 'Cargo outside its safe range' : 'No cargo at risk'}
          icon="thermometer-outline"
          tone={stats.excursions ? 'critical' : 'ok'}
        />
        <StatTile
          label="Value at risk"
          value={money(stats.atRisk)}
          hint={`of ${money(stats.protected)} being monitored${fleetMeta?.openIncidentCount != null ? ` · ${fleetMeta.openIncidentCount} open incidents` : ''}`}
          icon="shield-outline"
          tone={stats.atRisk ? 'critical' : 'neutral'}
        />
      </Grid>

      {isWide ? (
        <View style={styles.columns}>
          <View style={styles.mainCol}>{shipments}</View>
          <View style={styles.sideCol}>{side}</View>
        </View>
      ) : (
        <>
          {critical.length > 0 ? side : null}
          {shipments}
          {critical.length === 0 ? side : null}
        </>
      )}

      <VehicleDetail visible={!!detailId} vehicleId={detailId} onClose={() => setDetailId(null)} />
    </Page>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  columns: { flexDirection: 'row', gap: spacing.xl, alignItems: 'flex-start' },
  mainCol: { flex: 2 },
  sideCol: { flex: 1, minWidth: 300 },
  steps: { gap: spacing.lg },
  stepsRow: { flexDirection: 'row' },
  step: { flexDirection: 'row', gap: spacing.md, alignItems: 'flex-start' },
  stepIcon: { width: 36, height: 36, borderRadius: radius.md, alignItems: 'center', justifyContent: 'center' },
  shipment: { gap: spacing.md },
  shipmentHead: { flexDirection: 'row', alignItems: 'flex-start', gap: spacing.md },
  shipmentFoot: { flexDirection: 'row', justifyContent: 'space-between', borderTopWidth: StyleSheet.hairlineWidth, paddingTop: spacing.md },
  meta: { flexDirection: 'row', alignItems: 'center', gap: 6 },
  alertRow: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, padding: spacing.lg },
  alertIcon: { width: 34, height: 34, borderRadius: radius.md, alignItems: 'center', justifyContent: 'center' },
  alertFooter: { padding: spacing.md, borderTopWidth: StyleSheet.hairlineWidth, alignItems: 'flex-start' },
});
