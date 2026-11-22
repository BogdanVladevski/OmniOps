// Before sign-in: sign in, create a company account, or connect a driver's phone with a code.
import React, { useEffect, useState } from 'react';
import { KeyboardAvoidingView, Platform, Pressable, ScrollView, StyleSheet, Text, View } from 'react-native';
import { useTheme } from '../contexts/ThemeContext';
import { spacing, typography } from '../theme/tokens';
import { accountApi } from '../utils/api';
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

export default function AuthScreens() {
  const { colors } = useTheme();
  const [mode, setMode] = useState('signIn'); // signIn | signUp | driver
  const [form, setForm] = useState({ company: '', name: '', email: '', password: '', code: '' });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [demoAvailable, setDemoAvailable] = useState(false);
  const set = (k) => (v) => setForm((f) => ({ ...f, [k]: v }));

  useEffect(() => {
    accountApi.options().then((o) => setDemoAvailable(!!o?.demo)).catch(() => {});
  }, []);

  const go = (next) => { setMode(next); setError(null); };

  const run = (fn) => async () => {
    setBusy(true);
    setError(null);
    try {
      await setSession(fromResponse(await fn()));
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
  }[mode];

  const submit = {
    signIn: run(() => accountApi.signIn(email, form.password)),
    signUp: run(() => accountApi.signUp({ companyName: form.company.trim(), name: form.name.trim(), email, password: form.password })),
    driver: run(() => accountApi.pair(form.code)),
  }[mode];

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

            {error ? <Text style={[typography.label, { color: colors.critical }]}>{error}</Text> : null}
            <Button
              label={busy ? 'Please wait…' : { signIn: 'Sign in', signUp: 'Create account', driver: 'Connect' }[mode]}
              onPress={submit}
              disabled={!valid || busy}
              style={styles.big}
            />
          </Card>

          <View style={styles.links}>
            {mode !== 'signIn' ? <Link label="I have an account — sign in" onPress={() => go('signIn')} /> : null}
            {mode !== 'signUp' ? <Link label="New here? Create a company account" onPress={() => go('signUp')} /> : null}
            {mode !== 'driver' ? <Link label="I'm a driver — connect with a code" onPress={() => go('driver')} /> : null}
          </View>

          {demoAvailable ? (
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
  demo: { gap: spacing.xs, marginTop: spacing.lg },
});
