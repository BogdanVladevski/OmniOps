import { Stack, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { api, unwrap } from '@/api/client';
import { Button, Field } from '@/components/ui';
import { AuthFrame, FormError, MIN_PASSWORD } from '@/features/accounts/auth-frame';
import { useSessionMutation } from '@/features/accounts/account-api';

export default function ResetPassword() {
  const { token } = useLocalSearchParams<{ token: string }>();
  const [password, setPassword] = useState('');
  const reset = useSessionMutation(() =>
    unwrap(api.POST('/api/v1/account/reset-password', { body: { token, password } })),
  );
  const valid = password.length >= MIN_PASSWORD;

  return (
    <AuthFrame
      title="Choose a new password"
      intro="You'll be signed out everywhere else, and signed in here."
      links={[{ label: 'Back to sign in', href: '/sign-in' }]}
    >
      <Stack.Screen options={{ title: 'New password · OmniOps' }} />
      <Field
        label={`New password (at least ${MIN_PASSWORD} characters)`}
        value={password}
        onChangeText={setPassword}
        secureTextEntry
        autoComplete="new-password"
        onSubmitEditing={valid ? () => reset.mutate(undefined) : undefined}
      />
      <FormError message={reset.error?.message} />
      <Button
        label="Save and sign in"
        onPress={() => reset.mutate(undefined)}
        disabled={!valid}
        busy={reset.isPending}
      />
    </AuthFrame>
  );
}
