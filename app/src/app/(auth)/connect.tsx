// A driver connects their phone once, with the 8-character code from the office. No email, no password.
import { Stack } from 'expo-router';
import { useState } from 'react';
import { StyleSheet } from 'react-native';
import { api, unwrap } from '@/api/client';
import { Button, Field } from '@/components/ui';
import { AuthFrame, FormError } from '@/features/accounts/auth-frame';
import { useSessionMutation } from '@/features/accounts/account-api';
import { useLanguage } from '@/i18n';

export default function Connect() {
  const { t } = useLanguage();
  const [code, setCode] = useState('');
  const pair = useSessionMutation(() => unwrap(api.POST('/api/v1/account/pair', { body: { code } })));
  const valid = code.replace(/[^a-z0-9]/gi, '').length === 8;

  return (
    <AuthFrame
      title={t('connectTitle')}
      intro={t('connectIntro')}
      links={[{ label: 'I work in the office — sign in', href: '/sign-in' }]}
    >
      <Stack.Screen options={{ title: 'Connect · OmniOps' }} />
      <Field
        label={t('connectCode')}
        value={code}
        onChangeText={(v) => setCode(v.toUpperCase())}
        placeholder="K7QX-M2PD"
        autoCapitalize="characters"
        autoCorrect={false}
        maxLength={12}
        style={styles.code}
        onSubmitEditing={valid ? () => pair.mutate(undefined) : undefined}
      />
      <FormError message={pair.error?.message} />
      <Button label={t('connect')} onPress={() => pair.mutate(undefined)} disabled={!valid} busy={pair.isPending} />
    </AuthFrame>
  );
}

const styles = StyleSheet.create({ code: { fontSize: 22, letterSpacing: 3, textAlign: 'center' } });
