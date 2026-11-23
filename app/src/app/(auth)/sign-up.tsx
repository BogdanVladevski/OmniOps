import { Stack } from 'expo-router';
import { useState } from 'react';
import { api, unwrap } from '@/api/client';
import { Button, Field } from '@/components/ui';
import { AuthFrame, FormError, looksLikeEmail, MIN_PASSWORD } from '@/features/accounts/auth-frame';
import { useSessionMutation } from '@/features/accounts/account-api';

export default function SignUp() {
  const [form, setForm] = useState({ companyName: '', name: '', email: '', password: '' });
  const set = (key: keyof typeof form) => (value: string) => setForm((f) => ({ ...f, [key]: value }));
  const signUp = useSessionMutation(() =>
    unwrap(
      api.POST('/api/v1/account/sign-up', {
        body: {
          companyName: form.companyName.trim(),
          name: form.name.trim(),
          email: form.email.trim(),
          password: form.password,
        },
      }),
    ),
  );
  const valid =
    form.companyName.trim() && form.name.trim() && looksLikeEmail(form.email) && form.password.length >= MIN_PASSWORD;

  return (
    <AuthFrame
      title="Create your company account"
      intro="Free while we're in early access. You can add trucks and drivers right after."
      links={[
        { label: 'I have an account — sign in', href: '/sign-in' },
        { label: "I'm a driver — connect with a code", href: '/connect' },
      ]}
    >
      <Stack.Screen options={{ title: 'Create account · OmniOps' }} />
      <Field
        label="Company name"
        value={form.companyName}
        onChangeText={set('companyName')}
        placeholder="e.g. Vardar Transport"
        autoComplete="organization"
      />
      <Field label="Your name" value={form.name} onChangeText={set('name')} autoComplete="name" />
      <Field
        label="Work email"
        value={form.email}
        onChangeText={set('email')}
        keyboardType="email-address"
        autoCapitalize="none"
        autoComplete="email"
      />
      <Field
        label={`Password (at least ${MIN_PASSWORD} characters)`}
        value={form.password}
        onChangeText={set('password')}
        secureTextEntry
        autoComplete="new-password"
      />
      <FormError message={signUp.error?.message} />
      <Button
        label="Create account"
        onPress={() => signUp.mutate(undefined)}
        disabled={!valid}
        busy={signUp.isPending}
      />
    </AuthFrame>
  );
}
