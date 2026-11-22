// Turns this device into a driver's phone. Shown on the Admin page.
import React, { useEffect, useState } from 'react';
import { StyleSheet, Text, View } from 'react-native';
import { useDriverMode } from '../contexts/DriverModeContext';
import { useTheme } from '../contexts/ThemeContext';
import { spacing, typography } from '../theme/tokens';
import { jobsApi } from '../utils/api';
import { Button, Card, Segmented } from './ui/kit';

export default function DriverModeCard() {
  const { colors } = useTheme();
  const { startDriverMode } = useDriverMode();
  const [drivers, setDrivers] = useState([]);
  const [driverId, setDriverId] = useState(null);
  const [error, setError] = useState(null);

  useEffect(() => {
    jobsApi.assignable()
      .then((o) => { setDrivers(o.drivers ?? []); setDriverId(o.drivers?.[0]?.id ?? null); })
      .catch((e) => setError(e.message));
  }, []);

  const chosen = drivers.find((d) => d.id === driverId);

  return (
    <Card style={styles.gap}>
      <Text style={[typography.body, { color: colors.textSecondary }]}>
        Use this phone as a driver's device: it shows only that driver's jobs, shares location during a trip and
        collects proof of delivery.
      </Text>
      {drivers.length ? (
        <Segmented options={drivers.map((d) => ({ key: d.id, label: d.label }))} value={driverId} onChange={setDriverId} />
      ) : (
        <Text style={[typography.caption, { color: colors.muted }]}>{error ?? 'Add a driver under Fleet first.'}</Text>
      )}
      <View>
        <Button
          label={chosen ? `Switch to driver mode as ${chosen.label}` : 'Switch to driver mode'}
          onPress={() => chosen && startDriverMode({ id: chosen.id, name: chosen.label })}
          disabled={!chosen}
        />
      </View>
    </Card>
  );
}

const styles = StyleSheet.create({ gap: { gap: spacing.md } });
