import React, { useMemo, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useFleet } from '../contexts/FleetContext';
import { useTheme } from '../contexts/ThemeContext';
import { spacing, typography } from '../theme/tokens';
import { fleetApi } from '../utils/api';
import { useLayout } from '../utils/layout';
import { Button, Card, EmptyNote, Field, Grid, Page, Pill, SectionTitle, Segmented, StatTile } from './ui/kit';

const STATUS_FILTERS = [
  { key: 'all', label: 'All' },
  { key: 'active', label: 'Active' },
  { key: 'maintenance', label: 'Maintenance' },
  { key: 'retired', label: 'Retired' },
];

function statusTone(status) {
  const s = (status ?? '').toLowerCase();
  if (s === 'active') return 'ok';
  if (s === 'maintenance') return 'warning';
  return 'neutral';
}

export default function FleetManagement() {
  const { defaultFleetId, fleetMeta, dbVehicles, refreshFleetMeta } = useFleet();
  const { colors } = useTheme();
  const { isTablet, isWide } = useLayout();
  const [driverName, setDriverName] = useState('');
  const [truck, setTruck] = useState({ name: '', registration: '' });
  const [truckMessage, setTruckMessage] = useState(null);
  const truckNameOk = /^[A-Za-z0-9_-]+$/.test(truck.name.trim());
  const [driverMessage, setDriverMessage] = useState(null);
  const [busy, setBusy] = useState(null);
  const [statusFilter, setStatusFilter] = useState('all');
  const [search, setSearch] = useState('');

  const vehicles = useMemo(() => {
    const q = search.trim().toLowerCase();
    return dbVehicles.filter((v) => {
      const matchesSearch = !q || v.externalId?.toLowerCase().includes(q) || v.registration?.toLowerCase().includes(q);
      const status = (v.status ?? 'offline').toLowerCase();
      return matchesSearch && (statusFilter === 'all' || status === statusFilter);
    });
  }, [dbVehicles, search, statusFilter]);

  const createDriver = async () => {
    if (!driverName.trim()) return;
    setBusy('driver');
    setDriverMessage(null);
    try {
      await fleetApi.createDriver({ fleetId: defaultFleetId, fullName: driverName.trim(), licenseNumber: null });
      setDriverMessage({ tone: 'ok', text: `${driverName.trim()} added to the fleet.` });
      setDriverName('');
      await refreshFleetMeta();
    } catch (e) {
      setDriverMessage({ tone: 'critical', text: `Could not add driver: ${e.message}` });
    } finally {
      setBusy(null);
    }
  };

  const createTruck = async () => {
    setBusy('truck');
    setTruckMessage(null);
    try {
      await fleetApi.createVehicle({
        fleetId: defaultFleetId, externalId: truck.name.trim(), registration: truck.registration.trim() || null, vin: null,
      });
      setTruckMessage({ tone: 'ok', text: `${truck.name.trim()} added to the fleet.` });
      setTruck({ name: '', registration: '' });
      await refreshFleetMeta();
    } catch (e) {
      setTruckMessage({ tone: 'critical', text: `Could not add truck: ${e.message}` });
    } finally {
      setBusy(null);
    }
  };

  const vehicleSection = (
    <View>
      <SectionTitle title="Registered vehicles" subtitle={`${dbVehicles.length} trucks in this fleet`} />
      <Field placeholder="Search by truck ID or registration" value={search} onChangeText={setSearch} />
      <View style={{ height: spacing.sm }} />
      <Segmented options={STATUS_FILTERS} value={statusFilter} onChange={setStatusFilter} />
      <View style={{ height: spacing.md }} />
      {vehicles.length === 0 ? (
        <EmptyNote icon="search-outline" title="No vehicles match" body="Try a different search or filter." />
      ) : (
        <Card padded={false}>
          {vehicles.map((v, i) => (
            <View key={v.id} style={[styles.row, i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }]}>
              <View style={styles.flex}>
                <Text style={[typography.bodyStrong, { color: colors.text }]}>{v.externalId}</Text>
                <Text style={[typography.caption, { color: colors.muted }]} numberOfLines={1}>
                  Reg {v.registration ?? '—'} · VIN {v.vin ?? '—'}
                </Text>
              </View>
              <Pill label={(v.status ?? 'Offline').toString()} tone={statusTone(v.status)} />
            </View>
          ))}
        </Card>
      )}
    </View>
  );

  const toolsSection = (
    <View>
      <SectionTitle title="Add a truck" />
      <Card style={styles.gap}>
        <Field label="Truck name" placeholder="e.g. Truck-1 or SK1234AB" value={truck.name} onChangeText={(v) => setTruck((t) => ({ ...t, name: v }))} autoCapitalize="characters" />
        {truck.name && !truckNameOk ? (
          <Text style={[typography.caption, { color: colors.critical }]}>Use letters, numbers, - or _ (no spaces).</Text>
        ) : null}
        <Field label="Registration (optional)" placeholder="e.g. SK-1234-AB" value={truck.registration} onChangeText={(v) => setTruck((t) => ({ ...t, registration: v }))} autoCapitalize="characters" />
        <Button label={busy === 'truck' ? 'Adding…' : 'Add truck'} onPress={createTruck} disabled={busy === 'truck' || !truckNameOk} />
        {truckMessage ? <Pill label={truckMessage.text} tone={truckMessage.tone} /> : null}
      </Card>

      <SectionTitle title="Add a driver" />
      <Card style={styles.gap}>
        <Field label="Full name" placeholder="e.g. Ana Petrovska" value={driverName} onChangeText={setDriverName} />
        <Button label={busy === 'driver' ? 'Adding…' : 'Add driver'} icon="person-add-outline" onPress={createDriver} disabled={busy === 'driver' || !driverName.trim()} />
        {driverMessage ? <Pill label={driverMessage.text} tone={driverMessage.tone} /> : null}
      </Card>
    </View>
  );

  return (
    <Page title="Fleet">
      <Grid columns={isTablet ? 4 : 2}>
        <StatTile label="Vehicles" value={fleetMeta ? String(fleetMeta.vehicleCount) : '—'} icon="bus-outline" tone="accent" />
        <StatTile label="Drivers" value={fleetMeta ? String(fleetMeta.driverCount) : '—'} icon="people-outline" />
        <StatTile label="Active trips" value={fleetMeta ? String(fleetMeta.activeTripCount) : '—'} icon="navigate-outline" />
        <StatTile label="Depots" value={fleetMeta ? String(fleetMeta.depotCount) : '—'} icon="business-outline" />
      </Grid>
      {isWide ? (
        <View style={styles.columns}>
          <View style={styles.flex}>{vehicleSection}</View>
          <View style={styles.flex}>{toolsSection}</View>
        </View>
      ) : (
        <>
          {vehicleSection}
          {toolsSection}
        </>
      )}
    </Page>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  columns: { flexDirection: 'row', gap: spacing.xl, alignItems: 'flex-start' },
  gap: { gap: spacing.md },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
});
