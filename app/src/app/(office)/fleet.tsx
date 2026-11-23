// Fleet: the trucks and the drivers. Drivers connect their own phone with a one-time code; no hardware to install.
import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { api, unwrap } from '@/api/client';
import { Button, Card, Divider, EmptyNote, ErrorNote, Field, Page, SectionTitle } from '@/components/ui';
import { FormError } from '@/features/accounts/auth-frame';
import { DriverRow } from '@/features/fleet/driver-row';
import { fleetChanged, useDrivers, useVehicles } from '@/features/fleet/fleet-queries';
import { useLayout } from '@/hooks/use-layout';
import { useTheme } from '@/theme/theme-provider';
import { spacing, typography } from '@/theme/tokens';

function AddTruck() {
  const { colors } = useTheme();
  const [name, setName] = useState('');
  const [registration, setRegistration] = useState('');
  const add = useMutation({
    mutationFn: () =>
      unwrap(api.POST('/api/v1/vehicles', { body: { name: name.trim(), registration: registration.trim() || null } })),
    onSuccess: () => {
      setName('');
      setRegistration('');
      fleetChanged();
    },
  });
  return (
    <Card>
      <Text style={[typography.bodyStrong, { color: colors.text }]}>Add a truck</Text>
      <Field label="Name" placeholder="e.g. Truck-1 or the plate" value={name} onChangeText={setName} />
      <Field
        label="Registration (optional)"
        placeholder="e.g. SK-1234-AB"
        value={registration}
        onChangeText={setRegistration}
        autoCapitalize="characters"
      />
      <FormError message={add.error?.message} />
      <Button label="Add truck" onPress={() => add.mutate()} disabled={!name.trim()} busy={add.isPending} />
    </Card>
  );
}

function AddDriver() {
  const { colors } = useTheme();
  const [fullName, setFullName] = useState('');
  const [phone, setPhone] = useState('');
  const add = useMutation({
    mutationFn: () =>
      unwrap(api.POST('/api/v1/drivers', { body: { fullName: fullName.trim(), phone: phone.trim() || null } })),
    onSuccess: () => {
      setFullName('');
      setPhone('');
      fleetChanged();
    },
  });
  return (
    <Card>
      <Text style={[typography.bodyStrong, { color: colors.text }]}>Add a driver</Text>
      <Field
        label="Full name"
        placeholder="e.g. Ana Petrovska"
        value={fullName}
        onChangeText={setFullName}
        autoComplete="off"
      />
      <Field
        label="Phone (optional)"
        placeholder="+389 70 123 456"
        value={phone}
        onChangeText={setPhone}
        keyboardType="phone-pad"
      />
      <FormError message={add.error?.message} />
      <Button label="Add driver" onPress={() => add.mutate()} disabled={!fullName.trim()} busy={add.isPending} />
    </Card>
  );
}

export default function Fleet() {
  const { colors } = useTheme();
  const { isWide } = useLayout();
  const vehicles = useVehicles();
  const drivers = useDrivers();

  const trucks = (
    <View style={styles.column}>
      <SectionTitle title={`Trucks${vehicles.data ? ` · ${vehicles.data.length}` : ''}`} />
      {vehicles.error ? <ErrorNote message={vehicles.error.message} onRetry={() => void vehicles.refetch()} /> : null}
      {vehicles.data?.length === 0 ? (
        <EmptyNote
          title="No trucks yet"
          body="Add each truck once. Its position comes from the driver's phone during a job."
        />
      ) : vehicles.data ? (
        <Card padded={false}>
          {vehicles.data.map((v, i) => (
            <View key={v.id}>
              {i > 0 ? <Divider /> : null}
              <View style={styles.row}>
                <Text style={[typography.bodyStrong, styles.flex, { color: colors.text }]}>{v.name}</Text>
                <Text style={[typography.label, { color: colors.textSecondary }]}>{v.registration ?? ''}</Text>
              </View>
            </View>
          ))}
        </Card>
      ) : null}
      <AddTruck />
    </View>
  );

  const people = (
    <View style={styles.column}>
      <SectionTitle title={`Drivers${drivers.data ? ` · ${drivers.data.length}` : ''}`} />
      {drivers.error ? <ErrorNote message={drivers.error.message} onRetry={() => void drivers.refetch()} /> : null}
      {drivers.data?.length === 0 ? (
        <EmptyNote
          title="No drivers yet"
          body="Add a driver, then connect their phone with a code. The phone becomes the truck's GPS and camera."
        />
      ) : drivers.data ? (
        <Card padded={false}>
          {drivers.data.map((d, i) => (
            <View key={d.id}>
              {i > 0 ? <Divider /> : null}
              <DriverRow driver={d} />
            </View>
          ))}
        </Card>
      ) : null}
      <AddDriver />
    </View>
  );

  return (
    <Page title="Fleet">
      {isWide ? (
        <View style={styles.columns}>
          {trucks}
          {people}
        </View>
      ) : (
        <>
          {people}
          {trucks}
        </>
      )}
    </Page>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  columns: { flexDirection: 'row', gap: spacing.xl, alignItems: 'flex-start' },
  column: { flex: 1, gap: spacing.md },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.md,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.md,
  },
});
