// The frame around every screen before sign-in: the name, one card, and the ways to switch.
import { Link, type Href } from 'expo-router';
import type { ReactNode } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text, View } from 'react-native';
import { Card, Screen } from '@/components/ui';
import { useTheme } from '@/theme/theme-provider';
import { spacing, typography } from '@/theme/tokens';

export const MIN_PASSWORD = 10;
export const looksLikeEmail = (value: string) => /.+@.+\..+/.test(value.trim());

export function AuthFrame({
  title,
  intro,
  children,
  links = [],
  footer,
}: {
  title: string;
  intro?: string;
  children: ReactNode;
  links?: { label: string; href: Href }[];
  footer?: ReactNode;
}) {
  const { colors } = useTheme();
  return (
    <Screen>
      <KeyboardAvoidingView behavior={Platform.OS === 'ios' ? 'padding' : undefined} style={styles.flex}>
        <ScrollView contentContainerStyle={styles.wrap} keyboardShouldPersistTaps="handled">
          <Text style={[styles.wordmark, { color: colors.text }]}>OmniOps</Text>
          <Text style={[typography.body, styles.center, { color: colors.textSecondary }]}>
            Deliveries, proof and invoices for small fleets. No hardware needed.
          </Text>
          <Card style={styles.card}>
            <Text style={[typography.title, { color: colors.text }]}>{title}</Text>
            {intro ? <Text style={[typography.body, { color: colors.textSecondary }]}>{intro}</Text> : null}
            {children}
          </Card>
          <View style={styles.links}>
            {links.map((link) => (
              <Link key={link.label} href={link.href} replace style={[typography.label, { color: colors.accent }]}>
                {link.label}
              </Link>
            ))}
          </View>
          {footer}
        </ScrollView>
      </KeyboardAvoidingView>
    </Screen>
  );
}

export function FormError({ message }: { message: string | null | undefined }) {
  const { colors } = useTheme();
  return message ? (
    <Text style={[typography.label, { color: colors.critical }]} accessibilityRole="alert">
      {message}
    </Text>
  ) : null;
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  wrap: {
    padding: spacing.lg,
    paddingTop: spacing.xxxl,
    width: '100%',
    maxWidth: 440,
    alignSelf: 'center',
    gap: spacing.lg,
  },
  wordmark: { fontSize: 28, fontWeight: '700', letterSpacing: -0.6, textAlign: 'center' },
  center: { textAlign: 'center' },
  card: { gap: spacing.md },
  links: { gap: spacing.md, alignItems: 'center' },
});
