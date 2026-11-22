import React, { useCallback, useEffect, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  Pressable,
  ActivityIndicator,
  ScrollView,
} from 'react-native';
import AsyncStorage from '@react-native-async-storage/async-storage';
import { Ionicons } from '@expo/vector-icons';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { demoApi, getApiConfig } from '../utils/api';
import { notifyJobsChanged } from '../utils/events';
import { useTheme } from '../contexts/ThemeContext';
import { radius, spacing, typography } from '../theme/tokens';

const STORAGE_KEY = '@omniops/onboarding_complete';

const STEPS = [
  {
    title: 'Welcome to OmniOps',
    body: 'Run your deliveries from one place: see where every truck is, tell customers when it arrives, and get proof of delivery the moment the goods are handed over.',
    icon: 'snow',
  },
  {
    title: 'No hardware needed',
    body: 'Drivers use this same app on their phone in driver mode. Their phone shares the truck\'s location during a delivery. This app is connected to the server below.',
    icon: 'radio-outline',
  },
  {
    title: 'See it working',
    body: 'Load a demo day: two trucks on the road with live positions, and one delivery waiting to start.',
    icon: 'play-circle-outline',
  },
  {
    title: 'You are all set',
    body: 'Live shows today\'s deliveries and when each one arrives. Create deliveries under Jobs, send customers their tracking link, and invoice delivered jobs under Invoices. On a computer, press Ctrl K to search.',
    icon: 'checkmark-circle-outline',
  },
];

export function useOnboarding() {
  const [visible, setVisible] = useState(false);
  const [checked, setChecked] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const done = await AsyncStorage.getItem(STORAGE_KEY);
        setVisible(done !== 'true');
      } catch {
        setVisible(true);
      } finally {
        setChecked(true);
      }
    })();
  }, []);

  const complete = useCallback(async () => {
    await AsyncStorage.setItem(STORAGE_KEY, 'true');
    setVisible(false);
  }, []);

  const reset = useCallback(async () => {
    await AsyncStorage.removeItem(STORAGE_KEY);
    setVisible(true);
  }, []);

  return { visible: checked && visible, complete, reset };
}

export default function OnboardingWizard({ visible, onComplete }) {
  const { colors } = useTheme();
  const insets = useSafeAreaInsets();
  const { apiBaseUrl } = getApiConfig();
  const [step, setStep] = useState(0);
  const [bootstrapping, setBootstrapping] = useState(false);
  const [bootstrapMsg, setBootstrapMsg] = useState(null);
  const [demoStatus, setDemoStatus] = useState(null);

  useEffect(() => {
    if (!visible) return;
    setStep(0);
    setBootstrapMsg(null);
    demoApi.status().then(setDemoStatus).catch(() => setDemoStatus(null));
  }, [visible]);

  const bootstrap = async () => {
    setBootstrapping(true);
    setBootstrapMsg(null);
    try {
      const result = await demoApi.bootstrap(6);
      setBootstrapMsg(result.message ?? 'Demo day loaded.');
      notifyJobsChanged();
      await demoApi.status().then(setDemoStatus).catch(() => {});
    } catch (e) {
      setBootstrapMsg(e.message);
    } finally {
      setBootstrapping(false);
    }
  };

  const current = STEPS[step];
  const isLast = step === STEPS.length - 1;
  const isBootstrapStep = step === 2;

  const next = async () => {
    if (isBootstrapStep && !bootstrapMsg) {
      await bootstrap();
      return;
    }
    if (isLast) {
      onComplete();
      return;
    }
    setStep((s) => s + 1);
  };

  return (
    <Modal visible={visible} animationType="slide" presentationStyle="fullScreen">
      <View style={[styles.root, { backgroundColor: colors.background, paddingTop: insets.top + spacing.lg }]}>
        <ScrollView contentContainerStyle={styles.content}>
          <Ionicons name={current.icon} size={44} color={colors.text} style={styles.heroIcon} />
          <Text style={[styles.title, { color: colors.text }]}>{current.title}</Text>
          <Text style={[styles.body, { color: colors.textSecondary }]}>{current.body}</Text>

          {step === 1 && (
            <View style={[styles.card, { backgroundColor: colors.card, borderColor: colors.border }]}>
              <Text style={[styles.label, { color: colors.muted }]}>API endpoint</Text>
              <Text style={[styles.mono, { color: colors.text }]} selectable>
                {apiBaseUrl || 'Not configured — set EXPO_PUBLIC_API_URL'}
              </Text>
            </View>
          )}

          {isBootstrapStep && (
            <View style={[styles.card, { backgroundColor: colors.card, borderColor: colors.border }]}>
              {demoStatus ? (
                <>
                  <Text style={[styles.label, { color: colors.muted }]}>Demo organization</Text>
                  <Text style={{ color: colors.text, ...typography.bodyStrong }}>
                    {demoStatus.organizationName} · {demoStatus.vehicleCount} vehicles
                  </Text>
                </>
              ) : null}
              {bootstrapping ? (
                <ActivityIndicator color={colors.accent} style={{ marginTop: spacing.md }} />
              ) : null}
              {bootstrapMsg ? (
                <Text style={[styles.hint, { color: colors.success }]}>{bootstrapMsg}</Text>
              ) : null}
            </View>
          )}

          <View style={styles.dots}>
            {STEPS.map((_, i) => (
              <View
                key={i}
                style={[
                  styles.dot,
                  { backgroundColor: i === step ? colors.accent : colors.border },
                ]}
              />
            ))}
          </View>
        </ScrollView>

        <View style={[styles.footer, { paddingBottom: insets.bottom + spacing.lg }]}>
          {step > 0 ? (
            <Pressable onPress={() => setStep((s) => s - 1)} style={styles.secondaryBtn}>
              <Text style={{ color: colors.accent, ...typography.bodyStrong }}>Back</Text>
            </Pressable>
          ) : (
            <View style={styles.secondaryBtn} />
          )}
          <Pressable
            onPress={next}
            disabled={bootstrapping}
            style={({ pressed }) => [
              styles.primaryBtn,
              { backgroundColor: colors.accent, opacity: pressed || bootstrapping ? 0.8 : 1 },
            ]}
          >
            <Text style={styles.primaryText}>
              {isLast ? 'Get started' : isBootstrapStep && !bootstrapMsg ? 'Load demo day' : 'Continue'}
            </Text>
          </Pressable>
        </View>
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1 },
  content: { paddingHorizontal: spacing.xl, paddingBottom: spacing.xl, width: '100%', maxWidth: 560, alignSelf: 'center' },
  heroIcon: { alignSelf: 'center', marginBottom: spacing.xl },
  title: { ...typography.headline, textAlign: 'center', marginBottom: spacing.md },
  body: { ...typography.body, textAlign: 'center', marginBottom: spacing.xl },
  card: {
    borderRadius: radius.lg,
    borderWidth: StyleSheet.hairlineWidth,
    padding: spacing.lg,
    marginBottom: spacing.lg,
  },
  label: { ...typography.label, marginBottom: spacing.xs },
  mono: { fontFamily: 'monospace', fontSize: 13 },
  hint: { ...typography.body, marginTop: spacing.md, textAlign: 'center' },
  dots: { flexDirection: 'row', justifyContent: 'center', gap: spacing.sm, marginTop: spacing.lg },
  dot: { width: 8, height: 8, borderRadius: radius.pill },
  footer: {
    flexDirection: 'row',
    alignItems: 'center',
    paddingHorizontal: spacing.xl,
    gap: spacing.md,
    width: '100%',
    maxWidth: 560,
    alignSelf: 'center',
  },
  secondaryBtn: { flex: 1, alignItems: 'center', paddingVertical: spacing.md },
  primaryBtn: {
    flex: 2,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  primaryText: { ...typography.bodyStrong, color: '#FFFFFF' },
});
