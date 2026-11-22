// Admin: your account, the company, the office team, and connecting drivers' phones with one-time codes.
import React, { useCallback, useEffect, useState } from 'react';
import { Platform, Pressable, StyleSheet, Text, View } from 'react-native';
import { useSession } from '../contexts/SessionContext';
import { useTheme } from '../contexts/ThemeContext';
import { spacing, typography } from '../theme/tokens';
import { accountApi, jobsApi } from '../utils/api';
import { fromResponse, getSession, setSession } from '../utils/session';
import { shareLink } from '../utils/share';
import { Button, Card, Field, Segmented, timeAgo } from './ui/kit';

const MIN_PASSWORD = 10;

function Action({ label, onPress, tone }) {
  const { colors } = useTheme();
  return (
    <Pressable onPress={onPress} accessibilityRole="button" hitSlop={8}>
      <Text style={[typography.label, { color: tone === 'critical' ? colors.critical : colors.accent }]}>{label}</Text>
    </Pressable>
  );
}

/** Runs an API call with busy/error state; returns its result or undefined on failure. */
function useAction() {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const run = async (fn) => {
    setBusy(true);
    setError(null);
    try {
      return await fn();
    } catch (e) {
      setError(e.message);
      return undefined;
    } finally {
      setBusy(false);
    }
  };
  return { busy, error, setError, run };
}

function ErrorText({ error }) {
  const { colors } = useTheme();
  return error ? <Text style={[typography.label, { color: colors.critical }]}>{error}</Text> : null;
}

/** A link to hand over: copy or share it, and say plainly whether we emailed it. */
function LinkBox({ title, link, emailed, to, expires }) {
  const { colors } = useTheme();
  const [notice, setNotice] = useState(null);
  return (
    <View style={[styles.linkBox, { borderColor: colors.border, backgroundColor: colors.sunken }]}>
      <Text style={[typography.bodyStrong, { color: colors.text }]}>{title}</Text>
      <Text style={[typography.caption, { color: colors.textSecondary }]}>
        {emailed
          ? `We emailed it to ${to}. You can also send it yourself.`
          : `Email isn't set up on this server, so nothing was sent. Send this link to ${to} yourself (WhatsApp, Viber, email).`}
        {expires ? ` It works until ${new Date(expires).toLocaleString([], { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })}.` : ''}
      </Text>
      <Text selectable numberOfLines={2} style={[typography.caption, styles.mono, { color: colors.text }]}>{link}</Text>
      <View style={styles.actions}>
        <Button label="Copy link" variant="secondary" onPress={async () => setNotice(await shareLink(link, title))} />
        {notice ? <Text style={[typography.caption, { color: colors.muted }]}>{notice}</Text> : null}
      </View>
    </View>
  );
}

export function AccountCard() {
  const { colors } = useTheme();
  const { session } = useSession();
  const [panel, setPanel] = useState(null); // null | name | password
  const [name, setName] = useState('');
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [done, setDone] = useState(null);
  const { busy, error, setError, run } = useAction();
  const a = session?.account;
  if (!a) return null;

  const open = (p) => {
    setPanel(panel === p ? null : p);
    setError(null);
    setDone(null);
    setName(a.displayName);
    setCurrent('');
    setNext('');
  };

  const saveName = async () => {
    const account = await run(() => accountApi.updateProfile(name.trim()));
    if (account) {
      await setSession({ ...getSession(), account });
      setPanel(null);
    }
  };

  const savePassword = async () => {
    const fresh = await run(() => accountApi.changePassword(current, next));
    if (fresh) {
      await setSession(fromResponse(fresh));
      setPanel(null);
      setDone('Password changed. Other devices were signed out.');
    }
  };

  return (
    <Card style={styles.gap}>
      <View style={styles.row}>
        <View style={styles.flex}>
          <Text style={[typography.bodyStrong, { color: colors.text }]}>{a.displayName}</Text>
          <Text style={[typography.caption, { color: colors.muted }]}>
            {[a.email, a.role === 'Owner' ? 'Owner' : 'Dispatcher', a.organizationName].filter(Boolean).join(' · ')}
          </Text>
        </View>
        <Button label="Sign out" variant="secondary" onPress={() => accountApi.signOut()} />
      </View>
      <View style={styles.actions}>
        <Action label="Change name" onPress={() => open('name')} />
        <Action label="Change password" onPress={() => open('password')} />
        <Action label="Sign out on all devices" onPress={() => run(() => accountApi.signOutEverywhere())} />
      </View>
      {panel === 'name' ? (
        <View style={styles.gap}>
          <Field label="Your name" value={name} onChangeText={setName} autoComplete="name" />
          <View><Button label={busy ? 'Saving…' : 'Save'} onPress={saveName} disabled={!name.trim() || busy} /></View>
        </View>
      ) : null}
      {panel === 'password' ? (
        <View style={styles.gap}>
          <Field label="Current password" value={current} onChangeText={setCurrent} secureTextEntry autoComplete="current-password" />
          <Field label={`New password (at least ${MIN_PASSWORD} characters)`} value={next} onChangeText={setNext} secureTextEntry autoComplete="new-password" />
          <View>
            <Button label={busy ? 'Saving…' : 'Save new password'} onPress={savePassword} disabled={!current || next.length < MIN_PASSWORD || busy} />
          </View>
        </View>
      ) : null}
      <ErrorText error={error} />
      {done ? <Text style={[typography.caption, { color: colors.ok }]}>{done}</Text> : null}
    </Card>
  );
}

export function CompanyCard() {
  const { colors } = useTheme();
  const { session } = useSession();
  const [name, setName] = useState(session?.account?.organizationName ?? '');
  const [saved, setSaved] = useState(false);
  const { busy, error, run } = useAction();
  const unchanged = name.trim() === (session?.account?.organizationName ?? '');

  const save = async () => {
    const company = await run(() => accountApi.updateCompany(name.trim()));
    if (company) {
      const s = getSession();
      await setSession({ ...s, account: { ...s.account, organizationName: company.name } });
      setSaved(true);
    }
  };

  return (
    <Card style={styles.gap}>
      <Field label="Company name" value={name} onChangeText={(v) => { setName(v); setSaved(false); }} />
      <Text style={[typography.caption, { color: colors.muted }]}>
        Shown to your team and drivers. The name on invoices is set in Invoices → Invoice details.
      </Text>
      <View style={styles.actions}>
        <Button label={busy ? 'Saving…' : 'Save'} onPress={save} disabled={!name.trim() || unchanged || busy} />
        {saved ? <Text style={[typography.caption, { color: colors.ok }]}>Saved</Text> : null}
      </View>
      <ErrorText error={error} />
    </Card>
  );
}

const ROLE_OPTIONS = [
  { key: 'Dispatcher', label: 'Dispatcher' },
  { key: 'Owner', label: 'Owner' },
];

function lastSeen(iso) {
  return iso ? `last signed in ${timeAgo(iso)}` : 'not signed in yet';
}

/** Owners: who can use the office app, invitations, roles, removing people and reset links. */
export function TeamCard() {
  const { colors } = useTheme();
  const [team, setTeam] = useState(null);
  const [email, setEmail] = useState('');
  const [role, setRole] = useState('Dispatcher');
  const [handover, setHandover] = useState(null); // { title, link, emailed, to, expires }
  const [confirmRemove, setConfirmRemove] = useState(null);
  const { busy, error, setError, run } = useAction();

  const load = useCallback(() => accountApi.team().then(setTeam).catch((e) => setError(e.message)), [setError]);
  useEffect(() => { load(); }, [load]);

  const invite = async () => {
    const to = email.trim();
    const result = await run(() => accountApi.invite(to, role));
    if (result) {
      setEmail('');
      setHandover({ title: `Invitation for ${to}`, link: result.link, emailed: result.emailed, to, expires: result.expiresAtUtc });
      load();
    }
  };

  const act = async (fn) => {
    setHandover(null);
    if ((await run(fn)) !== undefined) load();
  };

  const resetLink = async (m) => {
    const result = await run(() => accountApi.resetLink(m.userId));
    if (result) setHandover({ title: `Password reset for ${m.displayName}`, link: result.link, emailed: result.emailed, to: m.email, expires: result.expiresAtUtc });
  };

  const validEmail = /.+@.+\..+/.test(email.trim());

  return (
    <Card style={styles.gap}>
      <Text style={[typography.body, { color: colors.textSecondary }]}>
        Dispatchers plan jobs, follow trucks and send invoices. Owners can also manage the team and company settings.
        Drivers don't need an invitation: they connect their phone with a code.
      </Text>

      <View>
        {(team?.members ?? []).map((m, i) => (
          <View key={m.userId} style={[styles.person, i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }]}>
            <View style={styles.flex}>
              <Text style={[typography.bodyStrong, { color: m.isActive ? colors.text : colors.muted }]}>
                {m.displayName}{m.isYou ? ' (you)' : ''}
              </Text>
              <Text style={[typography.caption, { color: colors.muted }]}>
                {[m.email, m.role, m.isActive ? lastSeen(m.lastSignInAtUtc) : 'removed'].filter(Boolean).join(' · ')}
              </Text>
            </View>
            {!m.isYou && m.isActive ? (
              <View style={styles.actions}>
                <Action
                  label={m.role === 'Owner' ? 'Make dispatcher' : 'Make owner'}
                  onPress={() => act(() => accountApi.setRole(m.userId, m.role === 'Owner' ? 'Dispatcher' : 'Owner'))}
                />
                <Action label="Reset link" onPress={() => resetLink(m)} />
                {confirmRemove === m.userId ? (
                  <Action label="Tap again to remove" tone="critical" onPress={() => { setConfirmRemove(null); act(() => accountApi.removeTeammate(m.userId)); }} />
                ) : (
                  <Action label="Remove" tone="critical" onPress={() => setConfirmRemove(m.userId)} />
                )}
              </View>
            ) : null}
          </View>
        ))}
        {(team?.invitations ?? []).map((inv) => (
          <View key={inv.id} style={[styles.person, { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }]}>
            <View style={styles.flex}>
              <Text style={[typography.body, { color: colors.textSecondary }]}>{inv.email}</Text>
              <Text style={[typography.caption, { color: colors.muted }]}>
                Invited as {inv.role.toLowerCase()} · link works until {new Date(inv.expiresAtUtc).toLocaleDateString([], { day: 'numeric', month: 'short' })}
              </Text>
            </View>
            <Action label="Withdraw" tone="critical" onPress={() => act(() => accountApi.revokeInvitation(inv.id))} />
          </View>
        ))}
      </View>

      <View style={[styles.gap, styles.invite, { borderTopColor: colors.border }]}>
        <Text style={[typography.bodyStrong, { color: colors.text }]}>Invite someone</Text>
        <Field label="Their work email" value={email} onChangeText={setEmail} keyboardType="email-address" autoCapitalize="none" autoComplete="off" />
        <Segmented options={ROLE_OPTIONS} value={role} onChange={setRole} />
        <View><Button label={busy ? 'Please wait…' : 'Create invitation'} onPress={invite} disabled={!validEmail || busy} /></View>
      </View>

      {handover ? <LinkBox {...handover} /> : null}
      <ErrorText error={error} />
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
  actions: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: spacing.lg },
  person: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, paddingVertical: spacing.md, flexWrap: 'wrap' },
  invite: { borderTopWidth: StyleSheet.hairlineWidth, paddingTop: spacing.md },
  linkBox: { borderWidth: StyleSheet.hairlineWidth, borderRadius: 12, padding: spacing.lg, gap: spacing.sm },
  mono: { fontFamily: Platform.select({ ios: 'Menlo', android: 'monospace', default: 'ui-monospace, Menlo, monospace' }) },
  codeBox: { borderWidth: StyleSheet.hairlineWidth, borderRadius: 12, padding: spacing.lg, alignItems: 'center', gap: spacing.xs },
  code: { fontSize: 32, fontWeight: '700', letterSpacing: 4, fontVariant: ['tabular-nums'] },
});
