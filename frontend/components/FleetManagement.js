import React, { useMemo, useState } from 'react';
import { View, Text, StyleSheet, ScrollView, Pressable, TextInput, ActivityIndicator } from 'react-native';
import { useFleet } from '../contexts/FleetContext';
import { useTheme } from '../contexts/ThemeContext';
import { fleetApi, copilotApi } from '../utils/api';
import { statusColor } from '../utils/fleetConfig';
import { useLayout } from '../utils/layout';
import Screen from './Screen';
import EmptyState from './ui/EmptyState';

const STATUS_FILTERS = ['all', 'ok', 'warning', 'offline'];

export default function FleetManagement() {
  const { defaultFleetId, fleetMeta, dbVehicles, refreshFleetMeta } = useFleet();
  const { colors } = useTheme();
  const { horizontalPadding, titleSize, bodySize, cardPadding } = useLayout();
  const [driverName, setDriverName] = useState('');
  const [copilotQuestion, setCopilotQuestion] = useState('Why might a vehicle be delayed?');
  const [copilotAnswer, setCopilotAnswer] = useState('');
  const [loading, setLoading] = useState(false);
  const [statusFilter, setStatusFilter] = useState('all');
  const [search, setSearch] = useState('');

  const filteredVehicles = useMemo(() => {
    return dbVehicles.filter((v) => {
      const matchesSearch =
        !search.trim() ||
        v.externalId?.toLowerCase().includes(search.toLowerCase()) ||
        v.registration?.toLowerCase().includes(search.toLowerCase());
      const status = (v.status ?? 'offline').toLowerCase();
      const matchesStatus = statusFilter === 'all' || status === statusFilter;
      return matchesSearch && matchesStatus;
    });
  }, [dbVehicles, search, statusFilter]);

  const createDriver = async () => {
    if (!driverName.trim()) return;
    setLoading(true);
    try {
      await fleetApi.createDriver({ fleetId: defaultFleetId, fullName: driverName.trim(), licenseNumber: null });
      setDriverName('');
      await refreshFleetMeta();
    } catch (e) {
      console.error(e);
    } finally {
      setLoading(false);
    }
  };

  const askCopilot = async () => {
    setLoading(true);
    try {
      const res = await copilotApi.ask(copilotQuestion, defaultFleetId);
      setCopilotAnswer(res.answer);
    } catch {
      setCopilotAnswer('Copilot unavailable — check API connection.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <Screen>
      <ScrollView contentContainerStyle={[styles.content, { paddingHorizontal: horizontalPadding }]}>
        <Text style={[styles.heading, { fontSize: titleSize, color: colors.text }]}>Fleet Management</Text>

        {fleetMeta && (
          <View style={[styles.card, { padding: cardPadding, backgroundColor: colors.card }]}>
            <Text style={[styles.cardTitle, { color: colors.text }]}>Fleet Statistics</Text>
            <Text style={[styles.metaLine, { color: colors.textSecondary }]}>Vehicles: {fleetMeta.vehicleCount}</Text>
            <Text style={[styles.metaLine, { color: colors.textSecondary }]}>Active trips: {fleetMeta.activeTripCount}</Text>
            <Text style={[styles.metaLine, { color: colors.textSecondary }]}>Drivers: {fleetMeta.driverCount}</Text>
            <Text style={[styles.metaLine, { color: colors.textSecondary }]}>Depots: {fleetMeta.depotCount}</Text>
          </View>
        )}

        <Text style={[styles.sectionTitle, { color: colors.text }]}>Registered Vehicles</Text>
        <TextInput
          style={[styles.input, { borderColor: colors.border, backgroundColor: colors.card, color: colors.text }]}
          placeholder="Search by ID or registration"
          placeholderTextColor={colors.muted}
          value={search}
          onChangeText={setSearch}
        />
        <ScrollView horizontal showsHorizontalScrollIndicator={false} style={styles.filterRow}>
          {STATUS_FILTERS.map((f) => (
            <Pressable
              key={f}
              onPress={() => setStatusFilter(f)}
              style={[
                styles.filterChip,
                { borderColor: colors.border, backgroundColor: statusFilter === f ? colors.accent : colors.card },
              ]}
            >
              <Text style={{ color: statusFilter === f ? '#fff' : colors.text, fontWeight: '600', fontSize: 12 }}>
                {f.toUpperCase()}
              </Text>
            </Pressable>
          ))}
        </ScrollView>

        {filteredVehicles.length === 0 ? (
          <EmptyState title="No vehicles match" body="Adjust filters or register vehicles via the API." icon="🚚" />
        ) : (
          filteredVehicles.map((v) => (
            <View key={v.id} style={[styles.card, { padding: cardPadding, backgroundColor: colors.card }]}>
              <View style={styles.vehicleHeader}>
                <Text style={[styles.vehicleId, { color: colors.text }]}>{v.externalId}</Text>
                <View style={[styles.badge, { backgroundColor: statusColor((v.status ?? 'offline').toLowerCase()) }]}>
                  <Text style={styles.badgeText}>{(v.status ?? 'offline').toUpperCase()}</Text>
                </View>
              </View>
              <Text style={[styles.metaLine, { color: colors.textSecondary }]}>VIN: {v.vin ?? '—'} · Reg: {v.registration ?? '—'}</Text>
            </View>
          ))
        )}

        <Text style={[styles.sectionTitle, { color: colors.text }]}>Add Driver</Text>
        <View style={[styles.card, { padding: cardPadding, backgroundColor: colors.card }]}>
          <TextInput
            style={[styles.input, { borderColor: colors.border, backgroundColor: colors.background, color: colors.text }]}
            placeholder="Driver full name"
            placeholderTextColor={colors.muted}
            value={driverName}
            onChangeText={setDriverName}
          />
          <Pressable style={[styles.button, { backgroundColor: colors.accent }]} onPress={createDriver} disabled={loading}>
            <Text style={styles.buttonText}>Create Driver</Text>
          </Pressable>
        </View>

        <Text style={[styles.sectionTitle, { color: colors.text }]}>AI Fleet Copilot</Text>
        <View style={[styles.card, { padding: cardPadding, backgroundColor: colors.card }]}>
          <TextInput
            style={[styles.input, { minHeight: 60, borderColor: colors.border, backgroundColor: colors.background, color: colors.text }]}
            multiline
            value={copilotQuestion}
            onChangeText={setCopilotQuestion}
          />
          <Pressable style={[styles.button, { backgroundColor: colors.accent }]} onPress={askCopilot} disabled={loading}>
            {loading ? <ActivityIndicator color="#fff" /> : <Text style={styles.buttonText}>Ask Copilot</Text>}
          </Pressable>
          {copilotAnswer ? (
            <Text style={[styles.copilotAnswer, { fontSize: bodySize, color: colors.text }]}>{copilotAnswer}</Text>
          ) : null}
        </View>
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  content: { paddingTop: 8, paddingBottom: 24 },
  heading: { fontWeight: '700', marginBottom: 12 },
  sectionTitle: { fontSize: 17, fontWeight: '700', marginTop: 16, marginBottom: 8 },
  card: { borderRadius: 14, marginBottom: 10 },
  cardTitle: { fontSize: 16, fontWeight: '700', marginBottom: 8 },
  vehicleHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  vehicleId: { fontSize: 16, fontWeight: '700' },
  badge: { borderRadius: 8, paddingHorizontal: 8, paddingVertical: 4 },
  badgeText: { color: '#fff', fontSize: 11, fontWeight: '700' },
  metaLine: { marginTop: 4 },
  filterRow: { marginBottom: 10, maxHeight: 40 },
  filterChip: { borderRadius: 16, borderWidth: 1, paddingHorizontal: 12, paddingVertical: 8, marginRight: 8 },
  input: { borderWidth: 1, borderRadius: 10, padding: 12, marginBottom: 10 },
  button: { borderRadius: 10, padding: 12, alignItems: 'center' },
  buttonText: { color: '#fff', fontWeight: '700' },
  copilotAnswer: { marginTop: 12, lineHeight: 20 },
});
