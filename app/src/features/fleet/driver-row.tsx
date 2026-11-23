// A driver: their phone number (for "Call driver") and a one-time code to connect their phone to the app.
import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { api, unwrap } from '@/api/client';
import type { Driver } from '@/api/types';
import { Field, TextAction } from '@/components/ui';
import { FormError } from '@/features/accounts/auth-frame';
import { useTheme } from '@/theme/theme-provider';
import { radius, spacing, typography } from '@/theme/tokens';
import { fleetChanged } from './fleet-queries';

export function DriverRow({ driver }: { driver: Driver }) {
  const { colors } = useTheme();
  const [phone, setPhone] = useState<string | null>(null); // non-null while editing
  const save = useMutation({
    mutationFn: (next: string) =>
      unwrap(
        api.PUT('/api/v1/drivers/{driverId}', {
          params: { path: { driverId: driver.id } },
          body: { fullName: driver.fullName, phone: next.trim() || null },
        }),
      ),
    onSuccess: () => {
      setPhone(null);
      fleetChanged();
    },
  });
  const code = useMutation({
    mutationFn: () => unwrap(api.POST('/api/v1/account/driver-codes', { body: { driverId: driver.id } })),
  });

  return (
    <View style={styles.wrap}>
      <View style={styles.row}>
        <View style={styles.flex}>
          <Text style={[typography.bodyStrong, { color: colors.text }]}>{driver.fullName}</Text>
          {phone == null ? (
            <Text style={[typography.caption, { color: driver.phone ? colors.textSecondary : colors.muted }]}>
              {driver.phone ?? 'No phone number. Add one to call from Today.'}
            </Text>
          ) : null}
        </View>
        {phone == null ? (
          <View style={styles.actions}>
            <TextAction
              label={driver.phone ? 'Change phone' : 'Add phone'}
              onPress={() => setPhone(driver.phone ?? '')}
            />
            <TextAction label={code.data ? 'New code' : 'Connect phone'} onPress={() => code.mutate()} />
          </View>
        ) : null}
      </View>
      {phone != null ? (
        <View style={styles.edit}>
          <View style={styles.flex}>
            <Field
              label="Phone"
              placeholder="+389 70 123 456"
              value={phone}
              onChangeText={setPhone}
              keyboardType="phone-pad"
              onSubmitEditing={() => save.mutate(phone)}
              autoFocus
            />
          </View>
          <TextAction label="Save" onPress={() => save.mutate(phone)} />
          <TextAction label="Cancel" onPress={() => setPhone(null)} />
        </View>
      ) : null}
      {code.data ? (
        <View style={[styles.codeBox, { borderColor: colors.border, backgroundColor: colors.sunken }]}>
          <Text selectable style={[styles.code, { color: colors.text }]}>
            {code.data.code}
          </Text>
          <Text style={[typography.caption, styles.center, { color: colors.muted }]}>
            On {driver.fullName.split(' ')[0]}&apos;s phone: open OmniOps, choose &quot;I&apos;m a driver&quot; and type
            this code. It works once, until{' '}
            {new Date(code.data.expiresAtUtc).toLocaleString([], {
              weekday: 'short',
              hour: '2-digit',
              minute: '2-digit',
            })}
            .
          </Text>
        </View>
      ) : null}
      <FormError message={save.error?.message ?? code.error?.message} />
    </View>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  wrap: { paddingHorizontal: spacing.lg, paddingVertical: spacing.md, gap: spacing.sm },
  row: { flexDirection: 'row', alignItems: 'center', gap: spacing.md, flexWrap: 'wrap' },
  actions: { flexDirection: 'row', gap: spacing.lg },
  edit: { flexDirection: 'row', alignItems: 'flex-end', gap: spacing.lg },
  codeBox: {
    borderWidth: StyleSheet.hairlineWidth,
    borderRadius: radius.lg,
    padding: spacing.lg,
    alignItems: 'center',
    gap: spacing.xs,
  },
  code: { fontSize: 32, fontWeight: '700', letterSpacing: 4, fontVariant: ['tabular-nums'] },
  center: { textAlign: 'center' },
});
