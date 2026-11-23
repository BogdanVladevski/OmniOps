import { useMutation } from '@tanstack/react-query';
import { Stack } from 'expo-router';
import { useState } from 'react';
import { Text } from 'react-native';
import { api, unwrap } from '@/api/client';
import { Button, Field } from '@/components/ui';
import { AuthFrame, FormError, looksLikeEmail } from '@/features/accounts/auth-frame';
import { useSignInOptions } from '@/features/accounts/account-api';
import { useTheme } from '@/theme/theme-provider';
import { typography } from '@/theme/tokens';

export default function Forgot() {
  const { colors } = useTheme();
  const options = useSignInOptions();
  const [email, setEmail] = useState('');
  const request = useMutation({
    mutationFn: () => unwrap(api.POST('/api/v1/account/forgot-password', { body: { email: email.trim() } })),
  });
  const byEmail = options.data?.passwordResetByEmail;

  return (
    <AuthFrame
      title="Forgot your password?"
      intro={
        byEmail
          ? "Enter the email you sign in with and we'll send you a link to choose a new password."
          : 'Ask the owner of your company account to open Settings → Team and send you a reset link. If you are the only owner, contact support.'
      }
      links={[{ label: 'Back to sign in', href: '/sign-in' }]}
    >
      <Stack.Screen options={{ title: 'Reset password · OmniOps' }} />
      {byEmail ? (
        <>
          <Field
            label="Email"
            value={email}
            onChangeText={setEmail}
            keyboardType="email-address"
            autoCapitalize="none"
            autoComplete="email"
          />
          {request.isSuccess ? (
            <Text style={[typography.body, { color: colors.ok }]}>
              If {email.trim()} has an account, a link to choose a new password is on its way. It works for one hour.
            </Text>
          ) : null}
          <FormError message={request.error?.message} />
          <Button
            label="Send link"
            onPress={() => request.mutate()}
            disabled={!looksLikeEmail(email) || request.isSuccess}
            busy={request.isPending}
          />
        </>
      ) : null}
    </AuthFrame>
  );
}
