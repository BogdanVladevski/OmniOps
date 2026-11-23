// The driver's phone: a header with their name and language, what's waiting to be sent, then the screens.
import { Stack } from 'expo-router';
import { StyleSheet, Text, View } from 'react-native';
import { Button, Screen } from '@/components/ui';
import { signOut } from '@/features/accounts/account-api';
import { DriverProvider, useDriver } from '@/features/driver/driver-context';
import { LanguagePicker, SyncBar } from '@/features/driver/driver-parts';
import { useLanguage } from '@/i18n';
import { useSession } from '@/lib/use-session';
import { useTheme } from '@/theme/theme-provider';
import { spacing, typography } from '@/theme/tokens';

function Header() {
  const { colors } = useTheme();
  const { t } = useLanguage();
  const name = useSession()?.account.displayName ?? '';
  const { items, sending } = useDriver();
  return (
    <>
      <View style={[styles.header, { borderBottomColor: colors.border, backgroundColor: colors.card }]}>
        <View style={styles.flex}>
          <Text style={[typography.title, { color: colors.text }]}>{name}</Text>
          <Text style={[typography.caption, { color: colors.muted }]}>{t('driver')}</Text>
        </View>
        <LanguagePicker />
        <Button label={t('signOut')} variant="secondary" onPress={() => void signOut()} />
      </View>
      <SyncBar items={items} sending={sending} />
    </>
  );
}

export default function DriverLayout() {
  const { colors } = useTheme();
  return (
    <DriverProvider>
      <Screen>
        <Header />
        <Stack
          screenOptions={{ headerShown: false, contentStyle: { backgroundColor: colors.background }, title: 'OmniOps' }}
        />
      </Screen>
    </DriverProvider>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
    borderBottomWidth: StyleSheet.hairlineWidth,
    zIndex: 10,
  },
});
