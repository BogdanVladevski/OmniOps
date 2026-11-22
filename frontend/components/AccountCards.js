// Admin: who is signed in, and connecting drivers' phones with one-time codes.
import React, { useEffect, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useSession } from '../contexts/SessionContext';
import { useTheme } from '../contexts/ThemeContext';
import { spacing, typography } from '../theme/tokens';
import { accountApi, jobsApi } from '../utils/api';
import { Button, Card, Segmented } from './ui/kit';

export function AccountCard() {
  const { colors } = useTheme();
  const { session } = useSession();
  const a = session?.account;
  if (!a) return null;
  return (
    <Card style={styles.row}>
      <View style={styles.flex}>
        <Text style={[typography.bodyStrong, { color: colors.text }]}>{a.displayName}</Text>
        <Text style={[typography.caption, { color: colors.muted }]}>
          {[a.email, a.role === 'Owner' ? 'Owner' : 'Office', a.organizationName].filter(Boolean).join(' · ')}
        </Text>
      </View>
      <Button label="Sign out" variant="secondary" onPress={() => accountApi.signOut()} />
    </Card>
  );
}

export function DriverCodeCard() {
  const { colors } = useTheme();
  const [drivers, setDrivers] = useState([]);
  const [driverId, setDriverId] = useState(null);
  const [code, setCode] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    jobsApi.assignable()
      .then((o) => { setDrivers(o.drivers ?? []); setDriverId(o.drivers?.[0]?.id ?? null); })
      .catch((e) => setError(e.message));
  }, []);

  const create = async () => {
    setBusy(true);
    setError(null);
    try {
      setCode(await accountApi.driverCode(driverId));
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card style={styles.gap}>
      <Text style={[typography.body, { color: colors.textSecondary }]}>
        Drivers use this app on their own phone. Create a code for a driver, then on their phone they choose
        "I'm a driver" and type it in. Codes work once, for 24 hours.
      </Text>
      {drivers.length ? (
        <Segmented options={drivers.map((d) => ({ key: d.id, label: d.label }))} value={driverId} onChange={(v) => { setDriverId(v); setCode(null); }} />
      ) : (
        <Text style={[typography.caption, { color: colors.muted }]}>{error ?? 'Add a driver under Fleet first.'}</Text>
      )}
      {code ? (
        <View style={[styles.codeBox, { borderColor: colors.border, backgroundColor: colors.sunken }]}>
          <Text style={[typography.caption, { color: colors.muted }]}>Code for {code.driverName}</Text>
          <Text selectable style={[styles.code, { color: colors.text }]}>{code.code}</Text>
          <Text style={[typography.caption, { color: colors.muted }]}>
            Valid until {new Date(code.expiresAtUtc).toLocaleString([], { weekday: 'short', hour: '2-digit', minute: '2-digit' })}
          </Text>
        </View>
      ) : null}
      {error && drivers.length ? <Text style={[typography.label, { color: colors.critical }]}>{error}</Text> : null}
      <View>
        <Button label={busy ? 'Creating…' : code ? 'Create a new code' : 'Create code'} onPress={create} disabled={!driverId || busy} />
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  gap: { gap: spacing.md },
  codeBox: { borderWidth: StyleSheet.hairlineWidth, borderRadius: 12, padding: spacing.lg, alignItems: 'center', gap: spacing.xs },
  code: { fontSize: 32, fontWeight: '700', letterSpacing: 4, fontVariant: ['tabular-nums'] },
});
