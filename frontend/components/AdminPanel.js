import React, { useCallback, useEffect, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTheme } from '../contexts/ThemeContext';
import { radius, spacing, typography } from '../theme/tokens';
import { adminApi, demoApi } from '../utils/api';
import { notifyJobsChanged } from '../utils/events';
import { useLayout } from '../utils/layout';
import DriverModeCard from './DriverModeCard';
import RetryPanel from './ui/RetryPanel';
import { SkeletonCard } from './ui/SkeletonLoader';
import { Button, Card, EmptyNote, Field, Page, Pill, SectionTitle, Segmented, timeAgo } from './ui/kit';

const THEME_OPTIONS = [
  { key: 'system', label: 'System' },
  { key: 'light', label: 'Light' },
  { key: 'dark', label: 'Dark' },
];

/** Demo trucks only move when the demo is refreshed; this puts them where they would be now. */
function DemoDayCard() {
  const { colors } = useTheme();
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState(null);
  const run = async () => {
    setBusy(true);
    try {
      setMessage((await demoApi.bootstrap()).message);
      notifyJobsChanged();
    } catch (e) {
      setMessage(e.message);
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card style={styles.demoCard}>
      <Text style={[typography.body, { color: colors.textSecondary }]}>
        Loads a demo delivery day, or moves the demo trucks to where they would be by now.
      </Text>
      <View><Button label={busy ? 'Loading…' : 'Load or refresh demo day'} variant="secondary" onPress={run} disabled={busy} /></View>
      {message ? <Text style={[typography.caption, { color: colors.muted }]}>{message}</Text> : null}
    </Card>
  );
}

export default function AdminPanel() {
  const { colors, themeMode, setThemeMode } = useTheme();
  const { isWide } = useLayout();
  const [auditLogs, setAuditLogs] = useState([]);
  const [apiKeys, setApiKeys] = useState([]);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [creating, setCreating] = useState(false);
  const [newKeyName, setNewKeyName] = useState('Mobile App');
  const [createdKey, setCreatedKey] = useState(null);

  const load = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const [logs, keys] = await Promise.all([adminApi.auditLogs(), adminApi.apiKeys()]);
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

  const access = (
    <View>
      <SectionTitle title="API keys" subtitle="Let other systems and devices connect to OmniOps" />
      <Card style={styles.gap}>
        <Field label="New key name" value={newKeyName} onChangeText={setNewKeyName} placeholder="e.g. Warehouse integration" />
        <Button label={creating ? 'Generating…' : 'Generate key'} icon="key-outline" onPress={createKey} disabled={creating} />
        {createdKey?.apiKey ? (
          <View style={[styles.secret, { backgroundColor: colors.okSoft }]}>
            <Text style={[typography.label, { color: colors.ok }]}>Copy this key now — it will not be shown again</Text>
            <Text selectable style={[typography.body, styles.mono, { color: colors.text }]}>{createdKey.apiKey}</Text>
          </View>
        ) : null}
      </Card>
      <View style={{ height: spacing.md }} />
      {loading ? (
        <SkeletonCard lines={2} />
      ) : apiKeys.length === 0 ? (
        <EmptyNote icon="key-outline" title="No API keys yet" body="Generate one for an integration or a mobile device." />
      ) : (
        <Card padded={false}>
          {apiKeys.map((key, i) => (
            <View key={key.id} style={[styles.row, i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }]}>
              <Ionicons name="key-outline" size={18} color={colors.muted} />
              <View style={styles.flex}>
                <Text style={[typography.bodyStrong, { color: colors.text }]}>{key.name}</Text>
                <Text style={[typography.caption, styles.mono, { color: colors.muted }]} numberOfLines={1}>{key.keyPrefix}…</Text>
              </View>
              <Pill label={`${String(key.scopes ?? '').split(' ').filter(Boolean).length} scopes`} tone="info" />
            </View>
          ))}
        </Card>
      )}

      <SectionTitle title="Demo data" />
      <DemoDayCard />

      <SectionTitle title="Driver mode" />
      <DriverModeCard />

      <SectionTitle title="Appearance" />
      <Card>
        <Segmented options={THEME_OPTIONS} value={themeMode} onChange={setThemeMode} />
      </Card>
    </View>
  );

  const audit = (
    <View>
      <SectionTitle title="Audit trail" subtitle="Who changed what, and when" />
      {loading ? (
        <SkeletonCard lines={3} />
      ) : auditLogs.length === 0 ? (
        <EmptyNote icon="document-text-outline" title="No audit entries yet" body="Actions such as resolving incidents or assigning drivers are recorded here." />
      ) : (
        <Card padded={false}>
          {auditLogs.map((log, i) => (
            <View key={log.id} style={[styles.row, i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }]}>
              <View style={[styles.dot, { backgroundColor: colors.accent }]} />
              <View style={styles.flex}>
                <Text style={[typography.bodyStrong, { color: colors.text }]}>{log.action} · {log.entityType}</Text>
                <Text style={[typography.caption, { color: colors.textSecondary }]} numberOfLines={2}>{log.details ?? log.entityId}</Text>
              </View>
              <Text style={[typography.caption, { color: colors.muted }]}>{timeAgo(log.occurredAtUtc)}</Text>
            </View>
          ))}
        </Card>
      )}
    </View>
  );

  return (
    <Page
      title="Admin"

      actions={<Button label="Refresh" icon="refresh" variant="secondary" onPress={load} />}
    >
      {error ? <RetryPanel message={error} onRetry={load} /> : null}
      {isWide ? (
        <View style={styles.columns}>
          <View style={styles.flex}>{access}</View>
          <View style={styles.flex}>{audit}</View>
        </View>
      ) : (
        <>
          {access}
          {audit}
        </>
      )}
    </Page>
  );
}

const styles = StyleSheet.create({
  demoCard: { gap: spacing.md },
  flex: { flex: 1 },
  columns: { flexDirection: 'row', gap: spacing.xl, alignItems: 'flex-start' },
  gap: { gap: spacing.md },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingHorizontal: spacing.lg, paddingVertical: spacing.md },
  dot: { width: 8, height: 8, borderRadius: 4 },
  secret: { borderRadius: radius.md, padding: spacing.md, gap: spacing.xs },
  mono: { fontFamily: 'monospace' },
});
