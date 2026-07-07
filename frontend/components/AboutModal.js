import React from 'react';
import { View, Text, StyleSheet, Modal, Pressable, ScrollView, Platform } from 'react-native';
import Constants from 'expo-constants';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { getApiConfig } from '../utils/api';
import { useTheme } from '../contexts/ThemeContext';
import { radius, spacing, typography } from '../theme/tokens';

const appVersion = Constants.expoConfig?.version ?? '1.0.0';
const appName = Constants.expoConfig?.name ?? 'OmniOps';

export default function AboutModal({ visible, onClose }) {
  const { colors, themeMode } = useTheme();
  const insets = useSafeAreaInsets();
  const { apiBaseUrl } = getApiConfig();

  return (
    <Modal visible={visible} animationType="slide" transparent onRequestClose={onClose}>
      <Pressable style={[styles.backdrop, { backgroundColor: colors.overlay }]} onPress={onClose}>
        <Pressable
          style={[
            styles.sheet,
            {
              marginBottom: insets.bottom + spacing.lg,
              backgroundColor: colors.card,
              borderColor: colors.border,
            },
          ]}
          onPress={(e) => e.stopPropagation()}
        >
          <ScrollView>
            <Text style={styles.logo}>🚛</Text>
            <Text style={[styles.title, { color: colors.text }]}>{appName}</Text>
            <Text style={[styles.version, { color: colors.muted }]}>Version {appVersion}</Text>
            <Text style={[styles.tagline, { color: colors.textSecondary }]}>
              Intelligent Fleet Operations Platform — telemetry, incidents, analytics, and AI-assisted operations.
            </Text>

            <View style={[styles.row, { borderColor: colors.border }]}>
              <Text style={[styles.label, { color: colors.muted }]}>API</Text>
              <Text style={[styles.value, { color: colors.text }]} selectable>
                {apiBaseUrl || 'Not configured'}
              </Text>
            </View>
            <View style={[styles.row, { borderColor: colors.border }]}>
              <Text style={[styles.label, { color: colors.muted }]}>Theme</Text>
              <Text style={[styles.value, { color: colors.text }]}>{themeMode}</Text>
            </View>
            <View style={[styles.row, { borderColor: colors.border }]}>
              <Text style={[styles.label, { color: colors.muted }]}>Platform</Text>
              <Text style={[styles.value, { color: colors.text }]}>{Platform.OS}</Text>
            </View>
          </ScrollView>

          <Pressable
            onPress={onClose}
            style={({ pressed }) => [
              styles.closeBtn,
              { backgroundColor: colors.accent, opacity: pressed ? 0.85 : 1 },
            ]}
          >
            <Text style={styles.closeText}>Close</Text>
          </Pressable>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: { flex: 1, justifyContent: 'flex-end', paddingHorizontal: spacing.lg },
  sheet: {
    borderRadius: radius.lg,
    borderWidth: StyleSheet.hairlineWidth,
    padding: spacing.xl,
    maxHeight: '80%',
  },
  logo: { fontSize: 48, textAlign: 'center', marginBottom: spacing.sm },
  title: { ...typography.headline, textAlign: 'center' },
  version: { ...typography.body, textAlign: 'center', marginTop: spacing.xs },
  tagline: { ...typography.body, textAlign: 'center', marginVertical: spacing.lg },
  row: {
    paddingVertical: spacing.md,
    borderTopWidth: StyleSheet.hairlineWidth,
  },
  label: { ...typography.label, marginBottom: spacing.xs },
  value: { ...typography.body, fontFamily: Platform.OS === 'ios' ? 'Menlo' : 'monospace', fontSize: 12 },
  closeBtn: {
    marginTop: spacing.lg,
    borderRadius: radius.md,
    paddingVertical: spacing.md,
    alignItems: 'center',
  },
  closeText: { ...typography.bodyStrong, color: '#FFFFFF' },
});
