import type { ReactNode } from 'react';
import { Modal, Pressable, ScrollView, StyleSheet } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useLayout } from '@/hooks/use-layout';
import { useTheme } from '@/theme/theme-provider';
import { radius, spacing } from '@/theme/tokens';

/** A bottom sheet on phones and a centred dialog on wide screens. */
export function Sheet({
  visible,
  onClose,
  children,
  width = 560,
}: {
  visible: boolean;
  onClose: () => void;
  children: ReactNode;
  width?: number;
}) {
  const { colors } = useTheme();
  const { isWide } = useLayout();
  const insets = useSafeAreaInsets();
  return (
    <Modal visible={visible} animationType="fade" transparent onRequestClose={onClose}>
      <Pressable
        style={[styles.backdrop, { backgroundColor: colors.overlay }, isWide && styles.centred]}
        onPress={onClose}
        accessibilityLabel="Close"
      >
        <Pressable
          onPress={() => {}}
          style={[
            styles.sheet,
            { backgroundColor: colors.card, paddingBottom: Math.max(insets.bottom, spacing.lg) + spacing.sm },
            isWide && [styles.dialog, { width }],
          ]}
        >
          <ScrollView
            showsVerticalScrollIndicator={false}
            contentContainerStyle={styles.content}
            keyboardShouldPersistTaps="handled"
          >
            {children}
          </ScrollView>
        </Pressable>
      </Pressable>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: { flex: 1, justifyContent: 'flex-end' },
  centred: { justifyContent: 'center', alignItems: 'center' },
  sheet: { borderTopLeftRadius: radius.xl, borderTopRightRadius: radius.xl, padding: spacing.xl, maxHeight: '90%' },
  dialog: { borderRadius: radius.xl, maxWidth: '94%' },
  content: { gap: spacing.lg },
});
