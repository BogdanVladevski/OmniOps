// Settings cards: your account, the company, and (owners) the office team.
import { useMutation, useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { Platform, StyleSheet, Text, View } from 'react-native';
import { api, unwrap } from '@/api/client';
import { keys, queryClient } from '@/api/query-client';
import type { TeamMember } from '@/api/types';
import { Button, Card, Field, Segmented, TextAction } from '@/components/ui';
import { fromResponse, getSession, setSession } from '@/lib/session';
import { shareLink } from '@/lib/share';
import { useSession } from '@/lib/use-session';
import { timeAgo } from '@/lib/time';
import { useTheme } from '@/theme/theme-provider';
import { radius, spacing, typography } from '@/theme/tokens';
import { signOut } from './account-api';
import { FormError, looksLikeEmail, MIN_PASSWORD } from './auth-frame';

const shortDate = (iso: string) =>
  new Date(iso).toLocaleString([], { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' });

/** A link to hand over: copy or share it, and say plainly whether we emailed it. */
function LinkBox({
  title,
  link,
  emailed,
  to,
  expires,
}: {
  title: string;
  link: string;
  emailed: boolean;
  to: string;
  expires: string;
}) {
  const { colors } = useTheme();
  const [notice, setNotice] = useState<string | null>(null);
  return (
    <View style={[styles.linkBox, { borderColor: colors.border, backgroundColor: colors.sunken }]}>
      <Text style={[typography.bodyStrong, { color: colors.text }]}>{title}</Text>
      <Text style={[typography.caption, { color: colors.textSecondary }]}>
        {emailed
          ? `We emailed it to ${to}. You can also send it yourself.`
          : `Email isn't set up on this server, so nothing was sent. Send this link to ${to} yourself (WhatsApp, Viber, email).`}{' '}
        It works until {shortDate(expires)}.
      </Text>
      <Text selectable numberOfLines={2} style={[typography.caption, styles.mono, { color: colors.text }]}>
        {link}
      </Text>
      <View style={styles.actions}>
        <Button
          label={Platform.OS === 'web' ? 'Copy link' : 'Share link'}
          variant="secondary"
          onPress={() => void shareLink(link, title).then(setNotice)}
        />
        {notice ? <Text style={[typography.caption, { color: colors.muted }]}>{notice}</Text> : null}
      </View>
    </View>
  );
}

export function AccountCard() {
  const { colors } = useTheme();
  const account = useSession()?.account;
  const [panel, setPanel] = useState<'name' | 'password' | null>(null);
  const [name, setName] = useState('');
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [done, setDone] = useState<string | null>(null);

  const rename = useMutation({
    mutationFn: () => unwrap(api.PUT('/api/v1/account/me', { body: { displayName: name.trim() } })),
    onSuccess: async (updated) => {
      const s = getSession();
      if (s) await setSession({ ...s, account: updated });
      setPanel(null);
    },
  });
  const password = useMutation({
    mutationFn: () =>
      unwrap(api.POST('/api/v1/account/password', { body: { currentPassword: current, newPassword: next } })),
    onSuccess: async (fresh) => {
      await setSession(fromResponse(fresh));
      setPanel(null);
      setDone('Password changed. Other devices were signed out.');
    },
  });
  const everywhere = useMutation({
    mutationFn: () => unwrap(api.POST('/api/v1/account/sign-out-everywhere')),
    onSuccess: () => void setSession(null),
  });

  if (!account) return null;
  const open = (p: 'name' | 'password') => {
    setPanel(panel === p ? null : p);
    setDone(null);
    setName(account.displayName);
    setCurrent('');
    setNext('');
  };

  return (
    <Card>
      <View style={styles.row}>
        <View style={styles.flex}>
          <Text style={[typography.bodyStrong, { color: colors.text }]}>{account.displayName}</Text>
          <Text style={[typography.caption, { color: colors.muted }]}>
            {[account.email, account.role === 'Owner' ? 'Owner' : 'Dispatcher', account.organizationName]
              .filter(Boolean)
              .join(' · ')}
          </Text>
        </View>
        <Button label="Sign out" variant="secondary" onPress={() => void signOut()} />
      </View>
      <View style={styles.actions}>
        <TextAction label="Change name" onPress={() => open('name')} />
        <TextAction label="Change password" onPress={() => open('password')} />
        <TextAction label="Sign out on all devices" onPress={() => everywhere.mutate()} />
      </View>
      {panel === 'name' ? (
        <View style={styles.gap}>
          <Field label="Your name" value={name} onChangeText={setName} autoComplete="name" />
          <View>
            <Button label="Save" onPress={() => rename.mutate()} disabled={!name.trim()} busy={rename.isPending} />
          </View>
        </View>
      ) : null}
      {panel === 'password' ? (
        <View style={styles.gap}>
          <Field
            label="Current password"
            value={current}
            onChangeText={setCurrent}
            secureTextEntry
            autoComplete="current-password"
          />
          <Field
            label={`New password (at least ${MIN_PASSWORD} characters)`}
            value={next}
            onChangeText={setNext}
            secureTextEntry
            autoComplete="new-password"
          />
          <View>
            <Button
              label="Save new password"
              onPress={() => password.mutate()}
              disabled={!current || next.length < MIN_PASSWORD}
              busy={password.isPending}
            />
          </View>
        </View>
      ) : null}
      <FormError message={rename.error?.message ?? password.error?.message ?? everywhere.error?.message} />
      {done ? <Text style={[typography.caption, { color: colors.ok }]}>{done}</Text> : null}
    </Card>
  );
}

export function CompanyCard() {
  const { colors } = useTheme();
  const current = useSession()?.account.organizationName ?? '';
  const [name, setName] = useState(current);
  const save = useMutation({
    mutationFn: () => unwrap(api.PUT('/api/v1/account/company', { body: { name: name.trim() } })),
    onSuccess: async (company) => {
      const s = getSession();
      if (s) await setSession({ ...s, account: { ...s.account, organizationName: company.name } });
    },
  });
  return (
    <Card>
      <Field label="Company name" value={name} onChangeText={setName} />
      <Text style={[typography.caption, { color: colors.muted }]}>
        Shown to your team and drivers. The name on invoices is set in Invoices → Invoice settings.
      </Text>
      <View style={styles.actions}>
        <Button
          label="Save"
          onPress={() => save.mutate()}
          disabled={!name.trim() || name.trim() === current}
          busy={save.isPending}
        />
        {save.isSuccess && name.trim() === current ? (
          <Text style={[typography.caption, { color: colors.ok }]}>Saved</Text>
        ) : null}
      </View>
      <FormError message={save.error?.message} />
    </Card>
  );
}

type Handover = { title: string; link: string; emailed: boolean; to: string; expires: string };

/** Owners: who can use the office app, invitations, roles, removing people and reset links. */
export function TeamCard() {
  const { colors } = useTheme();
  const team = useQuery({ queryKey: keys.team, queryFn: () => unwrap(api.GET('/api/v1/account/team')) });
  const [email, setEmail] = useState('');
  const [role, setRole] = useState<'Dispatcher' | 'Owner'>('Dispatcher');
  const [handover, setHandover] = useState<Handover | null>(null);
  const [confirmRemove, setConfirmRemove] = useState<string | null>(null);
  const refresh = () => void queryClient.invalidateQueries({ queryKey: keys.team });

  const invite = useMutation({
    mutationFn: (to: string) => unwrap(api.POST('/api/v1/account/team/invitations', { body: { email: to, role } })),
    onSuccess: (result, to) => {
      setEmail('');
      if (result.link)
        setHandover({
          title: `Invitation for ${to}`,
          link: result.link,
          emailed: result.emailed,
          to,
          expires: result.expiresAtUtc,
        });
      refresh();
    },
  });
  const change = useMutation({
    mutationFn: (call: () => Promise<unknown>) => call(),
    onMutate: () => setHandover(null),
    onSuccess: refresh,
  });
  const reset = useMutation({
    mutationFn: (m: TeamMember) =>
      unwrap(api.POST('/api/v1/account/team/{userId}/reset-link', { params: { path: { userId: m.userId } } })),
    onSuccess: (r, m) =>
      setHandover({
        title: `Password reset for ${m.displayName}`,
        link: r.link,
        emailed: r.emailed,
        to: m.email ?? m.displayName,
        expires: r.expiresAtUtc,
      }),
  });

  const setMemberRole = (m: TeamMember) =>
    change.mutate(() =>
      unwrap(
        api.PUT('/api/v1/account/team/{userId}/role', {
          params: { path: { userId: m.userId } },
          body: { role: m.role === 'Owner' ? 'Dispatcher' : 'Owner' },
        }),
      ),
    );
  const remove = (m: TeamMember) =>
    change.mutate(() =>
      unwrap(api.DELETE('/api/v1/account/team/{userId}', { params: { path: { userId: m.userId } } })),
    );
  const withdraw = (id: string) =>
    change.mutate(() => unwrap(api.DELETE('/api/v1/account/team/invitations/{id}', { params: { path: { id } } })));

  return (
    <Card>
      <Text style={[typography.body, { color: colors.textSecondary }]}>
        Dispatchers plan jobs, follow trucks and send invoices. Owners can also manage the team and company settings.
        Drivers don&apos;t need an invitation: they connect their phone with a code under Fleet.
      </Text>
      <View>
        {(team.data?.members ?? []).map((m, i) => (
          <View
            key={m.userId}
            style={[
              styles.person,
              i > 0 && { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border },
            ]}
          >
            <View style={styles.flex}>
              <Text style={[typography.bodyStrong, { color: m.isActive ? colors.text : colors.muted }]}>
                {m.displayName}
                {m.isYou ? ' (you)' : ''}
              </Text>
              <Text style={[typography.caption, { color: colors.muted }]}>
                {[
                  m.email,
                  m.role,
                  m.isActive
                    ? m.lastSignInAtUtc
                      ? `last signed in ${timeAgo(m.lastSignInAtUtc)}`
                      : 'not signed in yet'
                    : 'removed',
                ]
                  .filter(Boolean)
                  .join(' · ')}
              </Text>
            </View>
            {!m.isYou && m.isActive ? (
              <View style={styles.actions}>
                <TextAction
                  label={m.role === 'Owner' ? 'Make dispatcher' : 'Make owner'}
                  onPress={() => setMemberRole(m)}
                />
                <TextAction label="Reset link" onPress={() => reset.mutate(m)} />
                {confirmRemove === m.userId ? (
                  <TextAction
                    label="Tap again to remove"
                    tone="critical"
                    onPress={() => {
                      setConfirmRemove(null);
                      remove(m);
                    }}
                  />
                ) : (
                  <TextAction label="Remove" tone="critical" onPress={() => setConfirmRemove(m.userId)} />
                )}
              </View>
            ) : null}
          </View>
        ))}
        {(team.data?.invitations ?? []).map((inv) => (
          <View
            key={inv.id}
            style={[styles.person, { borderTopWidth: StyleSheet.hairlineWidth, borderTopColor: colors.border }]}
          >
            <View style={styles.flex}>
              <Text style={[typography.body, { color: colors.textSecondary }]}>{inv.email}</Text>
              <Text style={[typography.caption, { color: colors.muted }]}>
                Invited as {inv.role.toLowerCase()} · link works until{' '}
                {new Date(inv.expiresAtUtc).toLocaleDateString([], { day: 'numeric', month: 'short' })}
              </Text>
            </View>
            <TextAction label="Withdraw" tone="critical" onPress={() => withdraw(inv.id)} />
          </View>
        ))}
      </View>
      <View style={[styles.gap, styles.invite, { borderTopColor: colors.border }]}>
        <Text style={[typography.bodyStrong, { color: colors.text }]}>Invite someone</Text>
        <Field
          label="Their work email"
          value={email}
          onChangeText={setEmail}
          keyboardType="email-address"
          autoCapitalize="none"
          autoComplete="off"
        />
        <Segmented
          options={[
            { key: 'Dispatcher', label: 'Dispatcher' },
            { key: 'Owner', label: 'Owner' },
          ]}
          value={role}
          onChange={setRole}
        />
        <View>
          <Button
            label="Create invitation"
            onPress={() => invite.mutate(email.trim())}
            disabled={!looksLikeEmail(email)}
            busy={invite.isPending}
          />
        </View>
      </View>
      {handover ? <LinkBox {...handover} /> : null}
      <FormError
        message={team.error?.message ?? invite.error?.message ?? change.error?.message ?? reset.error?.message}
      />
    </Card>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md },
  gap: { gap: spacing.md },
  actions: { flexDirection: 'row', flexWrap: 'wrap', alignItems: 'center', gap: spacing.lg },
  person: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    paddingVertical: spacing.md,
    flexWrap: 'wrap',
  },
  invite: { borderTopWidth: StyleSheet.hairlineWidth, paddingTop: spacing.md },
  linkBox: { borderWidth: StyleSheet.hairlineWidth, borderRadius: radius.lg, padding: spacing.lg, gap: spacing.sm },
  mono: {
    fontFamily: Platform.select({ ios: 'Menlo', android: 'monospace', default: 'ui-monospace, Menlo, monospace' }),
  },
});
