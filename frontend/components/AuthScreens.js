// Before sign-in: sign in, create a company account, connect a driver's phone with a code,
// or finish a link from an email (join a company, choose a new password).
import React, { useEffect, useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '../contexts/ThemeContext';
import { spacing, typography } from '../theme/tokens';
import { accountApi } from '../utils/api';
import { clearPendingLink } from '../utils/links';
import { fromResponse, setSession } from '../utils/session';
import Screen from './Screen';
import { Button, Card, Field } from './ui/kit';

const MIN_PASSWORD = 10;

function Link({ label, onPress }) {
  const { colors } = useTheme();
  return (
    <Pressable onPress={onPress} accessibilityRole="button" hitSlop={10}>
      <Text style={[typography.label, { color: colors.accent }]}>{label}</Text>
    </Pressable>
  );
}

/** `link` is a pending { kind: 'join' | 'reset', token } from the address bar; `onLinkDone` forgets it. */
export default function AuthScreens({ link = null, onLinkDone }) {
  const { colors } = useTheme();
  // signIn | signUp | driver | forgot | reset | join
  const [mode, setMode] = useState(link?.kind ?? 'signIn');
  const [form, setForm] = useState({ company: '', name: '', email: '', password: '', code: '' });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState(null);
  const [options, setOptions] = useState({});
  const [invitation, setInvitation] = useState(null);
  const set = (k) => (v) => setForm((f) => ({ ...f, [k]: v }));

  useEffect(() => {
    accountApi.options().then((o) => setOptions(o ?? {})).catch(() => {});
  }, []);

  useEffect(() => {
    if (link?.kind !== 'join') return;
    accountApi.invitation(link.token).then(setInvitation).catch((e) => setError(e.message));
  }, [link]);

  const leaveLink = () => {
    clearPendingLink();
    onLinkDone?.();
  };

  const go = (next) => {
    if (mode === 'join' || mode === 'reset') leaveLink();
    setMode(next);
    setError(null);
    setNotice(null);
  };

  const run = (fn) => async () => {
    setBusy(true);
    setError(null);
    try {
      const session = fromResponse(await fn());
      if (link) leaveLink();
      await setSession(session);
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  const requestReset = async () => {
    setBusy(true);
    setError(null);
    try {
      await accountApi.forgotPassword(email);
      setNotice(`If ${email} has an account, a link to choose a new password is on its way. It works for one hour.`);
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  const email = form.email.trim();
  const valid = {
    signIn: email && form.password,
    signUp: form.company.trim() && form.name.trim() && /.+@.+\..+/.test(email) && form.password.length >= MIN_PASSWORD,
    driver: form.code.replace(/[^a-z0-9]/gi, '').length === 8,
    forgot: /.+@.+\..+/.test(email) && !notice,
    reset: form.password.length >= MIN_PASSWORD,
    join: !!invitation && form.name.trim() && form.password.length >= MIN_PASSWORD,
  }[mode];

  const submit = {
    signIn: run(() => accountApi.signIn(email, form.password)),
    signUp: run(() => accountApi.signUp({ companyName: form.company.trim(), name: form.name.trim(), email, password: form.password })),
    driver: run(() => accountApi.pair(form.code)),
    forgot: requestReset,
    reset: run(() => accountApi.resetPassword(link?.token, form.password)),
    join: run(() => accountApi.accept(link?.token, form.name.trim(), form.password)),
  }[mode];

  const newPassword = (
    <Field
      label={`New password (at least ${MIN_PASSWORD} characters)`}
      value={form.password}
      onChangeText={set('password')}
      secureTextEntry
      autoComplete="new-password"
      onSubmitEditing={valid ? submit : undefined}
    />
  );

  return (
    <Screen>
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={styles.flex}>
        <ScrollView contentContainerStyle={styles.wrap} keyboardShouldPersistTaps="handled">
          <Text style={[styles.wordmark, { color: colors.text }]}>OmniOps</Text>
          <Text style={[typography.body, styles.tagline, { color: colors.textSecondary }]}>
            Deliveries, proof and invoices for small fleets. No hardware needed.
          </Text>

          <Card style={styles.card}>
            {mode === 'signIn' ? (
              <>
                <Text style={[typography.title, { color: colors.text }]}>Sign in</Text>
                <Field label="Email" value={form.email} onChangeText={set('email')} keyboardType="email-address" autoCapitalize="none" autoComplete="email" />
                <Field label="Password" value={form.password} onChangeText={set('password')} secureTextEntry autoComplete="password" onSubmitEditing={valid ? submit : undefined} />
                <View style={styles.forgot}><Link label="Forgot password?" onPress={() => go('forgot')} /></View>
              </>
            ) : null}

            {mode === 'signUp' ? (
              <>
                <Text style={[typography.title, { color: colors.text }]}>Create your company account</Text>
                <Field label="Company name" value={form.company} onChangeText={set('company')} placeholder="e.g. Vardar Transport" />
                <Field label="Your name" value={form.name} onChangeText={set('name')} autoComplete="name" />
                <Field label="Work email" value={form.email} onChangeText={set('email')} keyboardType="email-address" autoCapitalize="none" autoComplete="email" />
                <Field label={`Password (at least ${MIN_PASSWORD} characters)`} value={form.password} onChangeText={set('password')} secureTextEntry autoComplete="new-password" />
              </>
            ) : null}

            {mode === 'driver' ? (
              <>
                <Text style={[typography.title, { color: colors.text }]}>Connect this phone</Text>
                <Text style={[typography.body, { color: colors.textSecondary }]}>
                  Enter the 8-character code from your office. You only need to do this once.
                </Text>
                <Field
                  label="Code"
                  value={form.code}
                  onChangeText={(v) => set('code')(v.toUpperCase())}
                  placeholder="e.g. K7QX-M2PD"
                  autoCapitalize="characters"
                  autoCorrect={false}
                  maxLength={12}
                  style={styles.code}
                />
              </>
            ) : null}

            {mode === 'forgot' ? (
              <>
                <Text style={[typography.title, { color: colors.text }]}>Forgot your password?</Text>
                {options.passwordResetByEmail ? (
                  <>
                    <Text style={[typography.body, { color: colors.textSecondary }]}>
                      Enter the email you sign in with and we'll send you a link to choose a new password.
                    </Text>
                    <Field label="Email" value={form.email} onChangeText={set('email')} keyboardType="email-address" autoCapitalize="none" autoComplete="email" />
                  </>
                ) : (
                  <Text style={[typography.body, { color: colors.textSecondary }]}>
                    Ask the owner of your company account to open Admin → Team and send you a reset link.
                    If you are the only owner, contact support.
                  </Text>
                )}
                {notice ? <Text style={[typography.body, { color: colors.ok }]}>{notice}</Text> : null}
              </>
            ) : null}

            {mode === 'reset' ? (
              <>
                <Text style={[typography.title, { color: colors.text }]}>Choose a new password</Text>
                <Text style={[typography.body, { color: colors.textSecondary }]}>
                  You'll be signed out everywhere else, and signed in here.
                </Text>
                {newPassword}
              </>
            ) : null}

            {mode === 'join' ? (
              <>
                <Text style={[typography.title, { color: colors.text }]}>
                  {invitation ? `Join ${invitation.companyName}` : 'Join your team'}
                </Text>
                {invitation ? (
                  <Text style={[typography.body, { color: colors.textSecondary }]}>
                    You're invited as {invitation.role === 'Owner' ? 'an owner' : 'a dispatcher'}. You'll sign in with {invitation.email}.
                  </Text>
                ) : null}
                <Field label="Your name" value={form.name} onChangeText={set('name')} autoComplete="name" />
                {newPassword}
              </>
            ) : null}

            {error ? <Text style={[typography.label, { color: colors.critical }]}>{error}</Text> : null}
            {mode === 'forgot' && !options.passwordResetByEmail ? null : (
            <Button
              label={busy ? 'Please wait…' : {
                signIn: 'Sign in', signUp: 'Create account', driver: 'Connect',
                forgot: 'Send link', reset: 'Save and sign in', join: 'Join and sign in',
              }[mode]}
              onPress={submit}
              disabled={!valid || busy}
              style={styles.big}
            />
            )}
          </Card>

          <View style={styles.links}>
            {mode !== 'signIn' ? <Link label="I have an account — sign in" onPress={() => go('signIn')} /> : null}
            {mode !== 'signUp' ? <Link label="New here? Create a company account" onPress={() => go('signUp')} /> : null}
            {mode !== 'driver' ? <Link label="I'm a driver — connect with a code" onPress={() => go('driver')} /> : null}
          </View>

          {options.demo && mode !== 'join' && mode !== 'reset' ? (
            <View style={styles.demo}>
              <Button label="Try the demo company" variant="secondary" onPress={run(() => accountApi.demo())} disabled={busy} />
              <Text style={[typography.caption, { color: colors.muted, textAlign: 'center' }]}>Only on development servers.</Text>
            </View>
          ) : null}
        </ScrollView>
      </KeyboardAvoidingView>
    </Screen>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  wrap: { padding: spacing.lg, paddingTop: spacing.xxxl, width: '100%', maxWidth: 440, alignSelf: 'center', gap: spacing.lg },
  wordmark: { fontSize: 28, fontWeight: '700', letterSpacing: -0.6, textAlign: 'center' },
  tagline: { textAlign: 'center' },
  card: { gap: spacing.md },
  big: { minHeight: 48 },
  code: { fontSize: 22, letterSpacing: 3, textAlign: 'center' },
  links: { gap: spacing.md, alignItems: 'center' },
  forgot: { alignItems: 'flex-end' },
  demo: { gap: spacing.xs, marginTop: spacing.lg },
});
