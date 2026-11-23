// An invitation from a company owner: choose a name and password, and you're in.
import { useQuery } from '@tanstack/react-query';
import { Stack, useLocalSearchParams } from 'expo-router';
import { useState } from 'react';
import { api, unwrap } from '@/api/client';
import { Button, Field } from '@/components/ui';
import { AuthFrame, FormError, MIN_PASSWORD } from '@/features/accounts/auth-frame';
import { useSessionMutation } from '@/features/accounts/account-api';

export default function Join() {
  const { token } = useLocalSearchParams<{ token: string }>();
  const [name, setName] = useState('');
  const [password, setPassword] = useState('');
  const invitation = useQuery({
    queryKey: ['invitation', token],
    queryFn: () => unwrap(api.GET('/api/v1/account/invitations/{token}', { params: { path: { token } } })),
    retry: false,
  });
  const accept = useSessionMutation(() =>
    unwrap(
      api.POST('/api/v1/account/invitations/{token}/accept', {
        params: { path: { token } },
        body: { name: name.trim(), password },
      }),
    ),
  );
  const info = invitation.data;
  const valid = !!info && name.trim() !== '' && password.length >= MIN_PASSWORD;

  return (
    <AuthFrame
      title={info ? `Join ${info.companyName}` : 'Join your team'}
      intro={
        info
          ? `You're invited as ${info.role === 'Owner' ? 'an owner' : 'a dispatcher'}. You'll sign in with ${info.email}.`
          : undefined
      }
      links={[{ label: 'I have an account — sign in', href: '/sign-in' }]}
    >
      <Stack.Screen options={{ title: 'Join · OmniOps' }} />
      <FormError message={invitation.error?.message} />
      <Field label="Your name" value={name} onChangeText={setName} autoComplete="name" />
      <Field
        label={`Password (at least ${MIN_PASSWORD} characters)`}
        value={password}
        onChangeText={setPassword}
        secureTextEntry
        autoComplete="new-password"
      />
      <FormError message={accept.error?.message} />
      <Button
        label="Join and sign in"
        onPress={() => accept.mutate(undefined)}
        disabled={!valid}
        busy={accept.isPending}
      />
    </AuthFrame>
  );
}
