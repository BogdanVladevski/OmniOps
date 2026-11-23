import { Link, Stack } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { api, unwrap } from '@/api/client';
import { Button, Field } from '@/components/ui';
import { AuthFrame, FormError } from '@/features/accounts/auth-frame';
import { useSessionMutation, useSignInOptions } from '@/features/accounts/account-api';
import { useTheme } from '@/theme/theme-provider';
import { spacing, typography } from '@/theme/tokens';

export default function SignIn() {
  const { colors } = useTheme();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const options = useSignInOptions();
  const signIn = useSessionMutation(() =>
    unwrap(api.POST('/api/v1/account/sign-in', { body: { email: email.trim(), password } })),
  );
  const demo = useSessionMutation(() => unwrap(api.POST('/api/v1/account/demo')));
  const valid = email.trim() !== '' && password !== '' && !signIn.isPending;

  return (
    <AuthFrame
      title="Sign in"
      links={[
        { label: 'New here? Create a company account', href: '/sign-up' },
        { label: "I'm a driver — connect with a code", href: '/connect' },
      ]}
      footer={
        options.data?.demo ? (
          <View style={styles.demo}>
            <Button
              label="Try the demo company"
              variant="secondary"
              onPress={() => demo.mutate(undefined)}
              busy={demo.isPending}
            />
            <FormError message={demo.error?.message} />
            <Text style={[typography.caption, styles.center, { color: colors.muted }]}>
              Only on development servers.
            </Text>
          </View>
        ) : null
      }
    >
      <Stack.Screen options={{ title: 'Sign in · OmniOps' }} />
      <Field
        label="Email"
        value={email}
        onChangeText={setEmail}
        keyboardType="email-address"
        autoCapitalize="none"
        autoComplete="email"
      />
      <Field
        label="Password"
        value={password}
        onChangeText={setPassword}
        secureTextEntry
        autoComplete="current-password"
        onSubmitEditing={valid ? () => signIn.mutate(undefined) : undefined}
      />
      <Link href="/forgot" style={[typography.label, styles.forgot, { color: colors.accent }]}>
        Forgot password?
      </Link>
      <FormError message={signIn.error?.message} />
      <Button label="Sign in" onPress={() => signIn.mutate(undefined)} disabled={!valid} busy={signIn.isPending} />
    </AuthFrame>
  );
}

const styles = StyleSheet.create({
  forgot: { alignSelf: 'flex-end' },
  demo: { gap: spacing.xs, marginTop: spacing.lg },
  center: { textAlign: 'center' },
});
