// A new delivery job: who it's for, where it goes, which truck. Everything else is optional.
import { useMutation } from '@tanstack/react-query';
import { useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { api, unwrap } from '@/api/client';
import { keys, queryClient } from '@/api/query-client';
import type { Job } from '@/api/types';
import { Button, Choice, Field, Sheet } from '@/components/ui';
import { FormError } from '@/features/accounts/auth-frame';
import { useJobOptions } from '@/features/office-queries';
import { geocode } from '@/lib/geocode';
import { useTheme } from '@/theme/theme-provider';
import { spacing, typography } from '@/theme/tokens';
import { parseTemperatureInput, TEMPERATURE_PRESETS } from './job-format';

const EMPTY = {
  vehicleId: null as string | null,
  driverId: null as string | null,
  customerName: '',
  reference: '',
  origin: '',
  destination: '',
  temp: 'none',
  min: '',
  max: '',
};

export function NewJobSheet({
  visible,
  onClose,
  onCreated,
}: {
  visible: boolean;
  onClose: () => void;
  onCreated: (job: Job, foundOnMap: boolean) => void;
}) {
  const { colors } = useTheme();
  const options = useJobOptions();
  const [form, setForm] = useState(EMPTY);
  const set =
    <K extends keyof typeof EMPTY>(key: K) =>
    (value: (typeof EMPTY)[K]) =>
      setForm((f) => ({ ...f, [key]: value }));

  // Temperature range: a preset, a custom pair, or none.
  const preset = TEMPERATURE_PRESETS.find((p) => p.key === form.temp);
  const customMin = parseTemperatureInput(form.min);
  const customMax = parseTemperatureInput(form.max);
  const range: readonly [number, number] | null =
    form.temp === 'custom'
      ? customMin != null && customMax != null && customMin < customMax
        ? [customMin, customMax]
        : null
      : (preset?.range ?? null);
  const rangeOk = form.temp !== 'custom' || range != null;
  const valid = !!form.vehicleId && form.customerName.trim() !== '' && form.destination.trim() !== '' && rangeOk;

  const create = useMutation({
    mutationFn: async () => {
      // Coordinates give the customer page a pin and an arrival estimate; the pickup's lets the driver's "arrived"
      // there count as evidence for waiting time. The job works without either.
      const [place, pickup] = await Promise.all([
        geocode(form.destination),
        form.origin.trim() ? geocode(form.origin) : null,
      ]);
      const job = await unwrap(
        api.POST('/api/v1/jobs', {
          body: {
            vehicleId: form.vehicleId!,
            driverId: form.driverId,
            customerName: form.customerName.trim(),
            reference: form.reference.trim() || null,
            origin: form.origin.trim() || null,
            originLatitude: pickup?.latitude ?? null,
            originLongitude: pickup?.longitude ?? null,
            destination: form.destination.trim(),
            destinationLatitude: place?.latitude ?? null,
            destinationLongitude: place?.longitude ?? null,
            plannedDeliveryUtc: null,
            minTempCelsius: range?.[0] ?? null,
            maxTempCelsius: range?.[1] ?? null,
          },
        }),
      );
      return { job, foundOnMap: !!place };
    },
    onSuccess: ({ job, foundOnMap }) => {
      setForm(EMPTY);
      void queryClient.invalidateQueries({ queryKey: keys.allJobs });
      onCreated(job, foundOnMap);
    },
  });

  return (
    <Sheet visible={visible} onClose={onClose}>
      <Text style={[typography.title, { color: colors.text }]}>New delivery job</Text>
      <Field
        label="Customer"
        placeholder="Who is the delivery for?"
        value={form.customerName}
        onChangeText={set('customerName')}
      />
      <Field label="Deliver to" placeholder="Street, town" value={form.destination} onChangeText={set('destination')} />
      <View style={styles.row}>
        <View style={styles.flex}>
          <Field
            label="From (optional)"
            placeholder="Depot or pickup"
            value={form.origin}
            onChangeText={set('origin')}
          />
        </View>
        <View style={styles.flex}>
          <Field
            label="Order no. (optional)"
            placeholder="PO / reference"
            value={form.reference}
            onChangeText={set('reference')}
          />
        </View>
      </View>
      <View style={styles.group}>
        <Text style={[typography.label, { color: colors.textSecondary }]}>Truck</Text>
        <Choice
          options={(options.data?.vehicles ?? []).map((v) => ({ id: v.id, label: v.name, detail: v.registration }))}
          value={form.vehicleId}
          onChange={set('vehicleId')}
          emptyText="No trucks yet. Add one under Fleet."
        />
      </View>
      <View style={styles.group}>
        <Text style={[typography.label, { color: colors.textSecondary }]}>Driver (optional)</Text>
        <Choice
          options={(options.data?.drivers ?? []).map((d) => ({ id: d.id, label: d.fullName, detail: d.phone }))}
          value={form.driverId}
          onChange={set('driverId')}
          emptyText="No drivers yet. Add one under Fleet."
        />
      </View>
      <View style={styles.group}>
        <Text style={[typography.label, { color: colors.textSecondary }]}>Refrigerated load</Text>
        <Choice
          options={TEMPERATURE_PRESETS.map((p) => ({ id: p.key, label: p.label }))}
          value={form.temp}
          onChange={(v) => set('temp')(v ?? 'none')}
          required
        />
        {form.temp === 'custom' ? (
          <View style={styles.row}>
            <View style={styles.flex}>
              <Field
                label="Lowest °C"
                placeholder="e.g. 2"
                value={form.min}
                onChangeText={set('min')}
                keyboardType="numbers-and-punctuation"
              />
            </View>
            <View style={styles.flex}>
              <Field
                label="Highest °C"
                placeholder="e.g. 8"
                value={form.max}
                onChangeText={set('max')}
                keyboardType="numbers-and-punctuation"
              />
            </View>
          </View>
        ) : null}
        {range ? (
          <Text style={[typography.caption, { color: colors.muted }]}>
            The driver logs the temperature with a photo at loading, every 2 hours and at delivery. Readings outside the
            range alert you.
          </Text>
        ) : null}
        {!rangeOk && (form.min || form.max) ? (
          <Text style={[typography.caption, { color: colors.critical }]}>
            Enter two numbers, the lowest first (e.g. 2 and 8).
          </Text>
        ) : null}
      </View>
      <FormError message={create.error?.message ?? options.error?.message} />
      <View style={styles.row}>
        <Button label="Cancel" variant="secondary" onPress={onClose} style={styles.flex} />
        <Button
          label="Create job"
          onPress={() => create.mutate()}
          disabled={!valid}
          busy={create.isPending}
          style={styles.flex}
        />
      </View>
    </Sheet>
  );
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  row: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
  group: { gap: spacing.xs },
});
