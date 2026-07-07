import React, { useCallback, useEffect, useState } from 'react';
import { View, Text, StyleSheet, ScrollView, Pressable, TextInput, ActivityIndicator } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { adminApi } from '../utils/api';
import { useTheme } from '../contexts/ThemeContext';
import { useLayout } from '../utils/layout';
import Screen from './Screen';
import EmptyState from './ui/EmptyState';
import RetryPanel from './ui/RetryPanel';
import { SkeletonCard } from './ui/SkeletonLoader';

export default function AdminPanel() {
  const [auditLogs, setAuditLogs] = useState([]);
  const [apiKeys, setApiKeys] = useState([]);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [newKeyName, setNewKeyName] = useState('Mobile App');
  const [createdKey, setCreatedKey] = useState(null);
  const insets = useSafeAreaInsets();
  const { colors } = useTheme();
  const { horizontalPadding, titleSize, bodySize, cardPadding } = useLayout();

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const [logs, keys] = await Promise.all([
        adminApi.auditLogs(),
        adminApi.apiKeys(),
      ]);
      setAuditLogs(logs ?? []);
      setApiKeys(keys ?? []);
    } catch (e) {
      setError(e.message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const createKey = async () => {
    setCreating(true);
    setCreatedKey(null);
    try {
      const result = await adminApi.createApiKey({
        name: newKeyName.trim() || 'API Key',
        scopes: 'vehicle:read vehicle:simulate fleet:admin platform:admin',
        expiresInDays: 90,
      });
      setCreatedKey(result);
      await load();
    } catch (e) {
      setError(e.message);
    } finally {
      setCreating(false);
    }
  };

  return (
    <Screen style={styles.screen}>
      <ScrollView contentContainerStyle={[styles.container, { paddingHorizontal: horizontalPadding, paddingBottom: insets.bottom + 24 }]}>
        <Text style={[styles.heading, { fontSize: titleSize, color: colors.text }]}>Admin Portal</Text>
        <Text style={[styles.subheading, { fontSize: bodySize, color: colors.muted }]}>
          Audit trail and API keys (platform:admin scope in production).
        </Text>

        {error ? <RetryPanel message={error} onRetry={load} /> : null}

        <Text style={[styles.section, { color: colors.text }]}>Create API Key</Text>
        <View style={[styles.card, { padding: cardPadding, backgroundColor: colors.card }]}>
          <TextInput
            style={[styles.input, { borderColor: colors.border, color: colors.text }]}
            value={newKeyName}
            onChangeText={setNewKeyName}
            placeholder="Key name"
            placeholderTextColor={colors.muted}
          />
          <Pressable style={[styles.primaryBtn, { backgroundColor: colors.accent }]} onPress={createKey} disabled={creating}>
            {creating ? <ActivityIndicator color="#fff" /> : <Text style={styles.primaryBtnText}>Generate Key</Text>}
          </Pressable>
          {createdKey?.apiKey ? (
            <Text style={[styles.success, { color: colors.success }]}>
              New key (copy now): {createdKey.apiKey}
            </Text>
          ) : null}
        </View>

        <Text style={[styles.section, { color: colors.text }]}>API Keys</Text>
        {loading ? (
          <SkeletonCard lines={2} />
        ) : apiKeys.length === 0 ? (
          <EmptyState title="No API keys" body="Generate a key for integrations or mobile clients." icon="🔑" />
        ) : (
          apiKeys.map((key) => (
            <View key={key.id} style={[styles.card, { padding: cardPadding, backgroundColor: colors.card }]}>
              <Text style={[styles.cardTitle, { color: colors.text }]}>{key.name}</Text>
              <Text style={[styles.muted, { color: colors.textSecondary }]}>{key.keyPrefix}… · {key.scopes}</Text>
            </View>
          ))
        )}

        <Text style={[styles.section, { color: colors.text }]}>Audit Logs</Text>
        {loading ? (
          <SkeletonCard lines={3} />
        ) : auditLogs.length === 0 ? (
          <EmptyState title="No audit entries" body="Actions like incident resolve and driver assignment appear here." icon="📋" />
        ) : (
          auditLogs.map((log) => (
            <View key={log.id} style={[styles.card, { padding: cardPadding, backgroundColor: colors.card }]}>
              <Text style={[styles.cardTitle, { color: colors.text }]}>{log.action} · {log.entityType}</Text>
              <Text style={[styles.muted, { color: colors.textSecondary }]}>{log.details ?? log.entityId}</Text>
              <Text style={[styles.time, { color: colors.muted }]}>{new Date(log.occurredAtUtc).toLocaleString()}</Text>
            </View>
          ))
        )}

        <Pressable style={[styles.refresh, { backgroundColor: colors.accent }]} onPress={load}>
          <Text style={styles.refreshText}>Refresh</Text>
        </Pressable>
      </ScrollView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  container: { paddingTop: 8 },
  heading: { fontWeight: '700' },
  subheading: { marginBottom: 16, marginTop: 4 },
  section: { fontSize: 17, fontWeight: '700', marginTop: 20, marginBottom: 8 },
  card: { borderRadius: 12, marginBottom: 8 },
  cardTitle: { fontWeight: '600' },
  muted: { marginTop: 4 },
  time: { fontSize: 12, marginTop: 6 },
  input: { borderWidth: 1, borderRadius: 10, padding: 12, marginBottom: 10 },
  primaryBtn: { borderRadius: 10, padding: 12, alignItems: 'center' },
  primaryBtnText: { color: '#FFF', fontWeight: '700' },
  success: { marginTop: 10, fontSize: 13 },
  refresh: { marginTop: 20, borderRadius: 12, padding: 14, alignItems: 'center' },
  refreshText: { color: '#FFF', fontWeight: '700' },
});
