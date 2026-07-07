import React from 'react';
import { View, Text, StyleSheet, Pressable } from 'react-native';
import { useTheme } from '../../contexts/ThemeContext';
import { radius, spacing, typography } from '../../theme/tokens';

export default function EmptyState({ title, body, icon = '📭', actionLabel, onAction }) {
  const { colors } = useTheme();
  return (
    <View style={[styles.container, { backgroundColor: colors.card, borderColor: colors.border }]}>
      <Text style={styles.icon} accessibilityElementsHidden>
        {icon}
      </Text>
      <Text style={[styles.title, { color: colors.text }]} accessibilityRole="header">
        {title}
      </Text>
      {body ? (
        <Text style={[styles.body, { color: colors.textSecondary }]}>{body}</Text>
      ) : null}
      {actionLabel && onAction ? (
        <Pressable
          onPress={onAction}
          style={({ pressed }) => [
            styles.action,
            { backgroundColor: colors.accent, opacity: pressed ? 0.85 : 1 },
          ]}
          accessibilityRole="button"
          accessibilityLabel={actionLabel}
        >
          <Text style={styles.actionText}>{actionLabel}</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    borderRadius: radius.lg,
    borderWidth: StyleSheet.hairlineWidth,
    padding: spacing.xl,
    alignItems: 'center',
  },
  icon: { fontSize: 32, marginBottom: spacing.sm },
  title: { ...typography.title, textAlign: 'center', marginBottom: spacing.xs },
  body: { ...typography.body, textAlign: 'center', marginBottom: spacing.md },
  action: {
    marginTop: spacing.sm,
    paddingHorizontal: spacing.lg,
    paddingVertical: spacing.sm + 2,
    borderRadius: radius.md,
  },
  actionText: { ...typography.bodyStrong, color: '#FFFFFF' },
});
