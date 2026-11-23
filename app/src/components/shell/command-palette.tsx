// Ctrl K (or the search box): jump to any job or invoice by customer, order number, place, truck or driver, or to a
// screen by name.
import { Ionicons } from '@expo/vector-icons';
import { router, type Href } from 'expo-router';
import { useEffect, useMemo, useRef, useState, type ComponentRef } from 'react';
import { FlatList, Modal, Pressable, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { isActive } from '@/components/ui/pressable-state';
import { useInvoiceOverview, useJobs } from '@/features/office-queries';
import { searchRecords } from '@/features/search/search';
import { useTheme } from '@/theme/theme-provider';
import { floating, radius, spacing, typography } from '@/theme/tokens';

const SCREENS: { id: string; label: string; href: Href; keywords: string }[] = [
  { id: 'today', label: 'Today', href: '/', keywords: 'today map live trucks arrival eta home needs money' },
  { id: 'jobs', label: 'Jobs', href: '/jobs', keywords: 'jobs deliveries orders proof tracking link customer driver' },
  {
    id: 'new-job',
    label: 'New job',
    href: { pathname: '/jobs', params: { create: '1' } },
    keywords: 'create add delivery order',
  },
  { id: 'invoices', label: 'Invoices', href: '/invoices', keywords: 'invoice bill money paid unpaid overdue reminder' },
  {
    id: 'fleet',
    label: 'Fleet: trucks and drivers',
    href: '/fleet',
    keywords: 'vehicles trucks drivers phone code connect',
  },
  {
    id: 'settings',
    label: 'Settings: company, team, invoices',
    href: '/settings',
    keywords: 'team invite password account company audit history bank vat waiting',
  },
];

interface Row {
  id: string;
  label: string;
  subtitle?: string;
  href: Href;
}

export function CommandPalette({ visible, onClose }: { visible: boolean; onClose: () => void }) {
  const { colors, isDark } = useTheme();
  const insets = useSafeAreaInsets();
  const [query, setQuery] = useState('');
  const input = useRef<ComponentRef<typeof TextInput>>(null);
  // autoFocus doesn't fire inside a modal on the web; focus once it's shown so typing right after Ctrl K lands here.
  useEffect(() => {
    if (!visible) return;
    const t = setTimeout(() => input.current?.focus(), 30);
    return () => clearTimeout(t);
  }, [visible]);
  const jobs = useJobs('All');
  const overview = useInvoiceOverview();

  const rows = useMemo<Row[]>(() => {
    const q = query.trim().toLowerCase();
    const screens = SCREENS.filter((s) => !q || `${s.label} ${s.keywords}`.toLowerCase().includes(q));
    const records = searchRecords({ jobs: jobs.data ?? [], invoices: overview.data?.invoices ?? [] }, query);
    return [...records, ...screens];
  }, [query, jobs.data, overview.data]);

  const close = () => {
    setQuery('');
    onClose();
  };
  const go = (row: Row) => {
    close();
    router.navigate(row.href);
  };

  return (
    <Modal visible={visible} transparent animationType="fade" onRequestClose={close}>
      <Pressable
        style={[styles.backdrop, { backgroundColor: colors.overlay, paddingTop: insets.top + 72 }]}
        onPress={close}
      >
        <Pressable onPress={() => {}} style={[styles.panel, floating(isDark), { backgroundColor: colors.elevated }]}>
          <View style={[styles.inputRow, { borderBottomColor: colors.border }]}>
            <Ionicons name="search" size={16} color={colors.muted} />
            <TextInput
              ref={input}
              autoFocus
              value={query}
              onChangeText={setQuery}
              placeholder="Customer, order, place, truck, invoice…"
              placeholderTextColor={colors.muted}
              style={[typography.body, styles.input, { color: colors.text }]}
              onSubmitEditing={() => rows[0] && go(rows[0])}
              accessibilityLabel="Search"
            />
            <Text style={[typography.caption, { color: colors.muted }]}>Esc</Text>
          </View>
          <FlatList
            data={rows}
            keyExtractor={(r) => r.id}
            keyboardShouldPersistTaps="handled"
            style={styles.list}
            ListEmptyComponent={
              <Text style={[typography.body, styles.empty, { color: colors.muted }]}>Nothing matches “{query}”.</Text>
            }
            renderItem={({ item }) => (
              <Pressable
                onPress={() => go(item)}
                accessibilityRole="button"
                style={(s) => [styles.row, isActive(s) && { backgroundColor: colors.sunken }]}
              >
                <Text style={[typography.bodyStrong, { color: colors.text }]} numberOfLines={1}>
                  {item.label}
                </Text>
                {item.subtitle ? (
                  <Text style={[typography.caption, { color: colors.muted }]} numberOfLines={1}>
                    {item.subtitle}
                  </Text>
                ) : null}
              </Pressable>
            )}
          />
        </Pressable>
      </Pressable>
    </Modal>
  );
}

const styles = StyleSheet.create({
  backdrop: { flex: 1, alignItems: 'center', paddingHorizontal: spacing.md },
  panel: { width: '100%', maxWidth: 600, borderRadius: radius.lg, overflow: 'hidden' },
  inputRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing.sm,
    paddingHorizontal: spacing.lg,
    borderBottomWidth: StyleSheet.hairlineWidth,
  },
  input: { flex: 1, paddingVertical: spacing.md, outlineStyle: 'none' } as object,
  list: { maxHeight: 420 },
  row: { paddingHorizontal: spacing.lg, paddingVertical: spacing.sm + 2, gap: 1 },
  empty: { padding: spacing.lg },
});
