// Shared building blocks for every screen: page scaffold, cards, metrics, status pills, charts.
import React from 'react';
import { Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { Ionicons } from '@expo/vector-icons';
import { useTheme } from '../../contexts/ThemeContext';
import { elevation, radius, spacing, toneColors, typography } from '../../theme/tokens';
import { useLayout } from '../../utils/layout';
import Screen from '../Screen';

/** Scrollable page with a centred max-width column, consistent padding and a header. */
export function Page({ title, subtitle, actions, children, scroll = true }) {
  const { colors } = useTheme();
  const { horizontalPadding, contentMaxWidth, isWide } = useLayout();
  const body = (
    <View style={[styles.pageInner, { maxWidth: contentMaxWidth, paddingHorizontal: horizontalPadding }]}>
      {title ? (
        <View style={[styles.pageHeader, isWide && styles.pageHeaderWide]}>
          <View style={styles.flex}>
            <Text style={[typography.headline, { color: colors.text }, isWide && typography.display]}>{title}</Text>
            {subtitle ? (
              <Text style={[typography.body, styles.pageSubtitle, { color: colors.textSecondary }]}>{subtitle}</Text>
            ) : null}
          </View>
          {actions ? <View style={styles.pageActions}>{actions}</View> : null}
        </View>
      ) : null}
      {children}
    </View>
  );
  return (
    <Screen>
      {scroll ? (
        <ScrollView contentContainerStyle={styles.pageScroll} showsVerticalScrollIndicator={false}>
          {body}
        </ScrollView>
      ) : (
        body
      )}
    </Screen>
  );
}

export function Card({ children, style, padded = true, onPress, accessibilityLabel }) {
  const { colors, isDark } = useTheme();
  const { cardPadding } = useLayout();
  const cardStyle = [
    styles.card,
    { backgroundColor: colors.card },
    elevation(isDark),
    padded && { padding: cardPadding },
    style,
  ];
  if (onPress) {
    return (
      <Pressable
        onPress={onPress}
        accessibilityRole="button"
        accessibilityLabel={accessibilityLabel}
        style={({ pressed, hovered }) => [...cardStyle, (pressed || hovered) && { opacity: 0.92 }]}
      >
        {children}
      </Pressable>
    );
  }
  return <View style={cardStyle}>{children}</View>;
}

export function SectionTitle({ title, subtitle, right }) {
  const { colors } = useTheme();
  return (
    <View style={styles.sectionTitle}>
      <View style={styles.flex}>
        <Text style={[typography.title, { color: colors.text }]}>{title}</Text>
        {subtitle ? <Text style={[typography.caption, { color: colors.muted, marginTop: 2 }]}>{subtitle}</Text> : null}
      </View>
      {right}
    </View>
  );
}

/** Lays children out in N equal columns, wrapping as needed. */
export function Grid({ columns = 2, gap = spacing.md, children }) {
  const items = React.Children.toArray(children).filter(Boolean);
  return (
    <View style={[styles.grid, { marginHorizontal: -gap / 2 }]}>
      {items.map((child, i) => (
        <View key={i} style={{ width: `${100 / columns}%`, paddingHorizontal: gap / 2, marginBottom: gap }}>
          {child}
        </View>
      ))}
    </View>
  );
}

export function StatTile({ label, value, hint, icon, tone = 'neutral' }) {
  const { colors } = useTheme();
  const t = toneColors(colors, tone);
  return (
    <Card style={styles.stat}>
      <View style={styles.statTop}>
        <Text style={[typography.overline, { color: colors.muted }]} numberOfLines={1}>{label}</Text>
        {icon ? (
          <View style={[styles.statIcon, { backgroundColor: t.bg }]}>
            <Ionicons name={icon} size={16} color={t.fg} />
          </View>
        ) : null}
      </View>
      <Text
        style={[typography.metric, { color: tone === 'neutral' ? colors.text : t.fg }]}
        numberOfLines={1}
        adjustsFontSizeToFit
      >
        {value}
      </Text>
      {hint ? <Text style={[typography.caption, { color: colors.textSecondary }]} numberOfLines={2}>{hint}</Text> : null}
    </Card>
  );
}

export function Pill({ label, tone = 'neutral', icon, style }) {
  const { colors } = useTheme();
  const t = toneColors(colors, tone);
  return (
    <View style={[styles.pill, { backgroundColor: t.bg }, style]}>
      {icon ? <Ionicons name={icon} size={12} color={t.fg} /> : <View style={[styles.pillDot, { backgroundColor: t.fg }]} />}
      <Text style={[typography.label, { color: t.fg }]} numberOfLines={1}>{label}</Text>
    </View>
  );
}

export function Button({ label, icon, onPress, variant = 'primary', disabled, style }) {
  const { colors } = useTheme();
  const primary = variant === 'primary';
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled}
      accessibilityRole="button"
      style={({ pressed, hovered }) => [
        styles.button,
        primary
          ? { backgroundColor: colors.accent }
          : { backgroundColor: 'transparent', borderWidth: 1, borderColor: colors.border },
        (pressed || hovered) && { opacity: 0.85 },
        disabled && { opacity: 0.5 },
        style,
      ]}
    >
      {icon ? <Ionicons name={icon} size={16} color={primary ? colors.onAccent : colors.text} /> : null}
      <Text style={[typography.bodyStrong, { color: primary ? colors.onAccent : colors.text }]}>{label}</Text>
    </Pressable>
  );
}

/** Labelled text input styled to match the theme. */
export function Field({ label, style, multiline, ...props }) {
  const { colors } = useTheme();
  return (
    <View style={styles.field}>
      {label ? <Text style={[typography.label, { color: colors.textSecondary }]}>{label}</Text> : null}
      <TextInput
        placeholderTextColor={colors.muted}
        multiline={multiline}
        style={[
          styles.input,
          { borderColor: colors.border, backgroundColor: colors.sunken, color: colors.text },
          multiline && { minHeight: 72, textAlignVertical: 'top' },
          style,
        ]}
        {...props}
      />
    </View>
  );
}

/** Selectable filter chips. options: [{ key, label, count? }] */
export function Segmented({ options, value, onChange }) {
  const { colors } = useTheme();
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.segmented}>
      {options.map((o) => {
        const active = o.key === value;
        return (
          <Pressable
            key={o.key}
            onPress={() => onChange(o.key)}
            accessibilityRole="button"
            accessibilityState={{ selected: active }}
            style={[
              styles.segment,
              { borderColor: active ? colors.accent : colors.border, backgroundColor: active ? colors.accentMuted : colors.card },
            ]}
          >
            <Text style={[typography.label, { color: active ? colors.accentStrong : colors.textSecondary }]}>
              {o.label}
              {o.count != null ? `  ${o.count}` : ''}
            </Text>
          </Pressable>
        );
      })}
    </ScrollView>
  );
}

/**
 * Shows where the current cargo temperature sits relative to the shipment's safe range.
 * The safe band is drawn in the middle 60% of the track so readings just outside it stay visible.
 */
export function TempRangeBar({ value, min, max }) {
  const { colors } = useTheme();
  if (value == null || min == null || max == null) return null;
  const span = Math.max(max - min, 1);
  const lo = min - span / 3;
  const hi = max + span / 3;
  const pct = Math.min(100, Math.max(0, ((value - lo) / (hi - lo)) * 100));
  const inRange = value >= min && value <= max;
  const markerColor = inRange ? colors.ok : colors.critical;
  return (
    <View>
      <View style={[styles.rangeTrack, { backgroundColor: colors.sunken }]}>
        <View style={[styles.rangeSafe, { backgroundColor: colors.okSoft, borderColor: colors.ok }]} />
        <View style={[styles.rangeMarker, { left: `${pct}%`, backgroundColor: markerColor, borderColor: colors.card }]} />
      </View>
      <View style={styles.rangeLabels}>
        <Text style={[typography.caption, { color: colors.muted }]}>Safe {min}–{max}°C</Text>
        <Text style={[typography.caption, { color: markerColor, fontWeight: '700' }]}>
          {value}°C {inRange ? 'in range' : 'out of range'}
        </Text>
      </View>
    </View>
  );
}

/** Minimal bar chart scaled to the data's own min–max so small changes are visible. */
export function BarChart({ values, color, height = 96, emptyText = 'Not enough history yet' }) {
  const { colors } = useTheme();
  const data = (values ?? []).filter((v) => typeof v === 'number' && Number.isFinite(v));
  if (data.length < 2) {
    return (
      <View style={[styles.chartEmpty, { height, backgroundColor: colors.sunken }]}>
        <Text style={[typography.caption, { color: colors.muted }]}>{emptyText}</Text>
      </View>
    );
  }
  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = max - min || 1;
  return (
    <View>
      <View style={[styles.chart, { height }]}>
        {data.map((v, i) => (
          <View key={i} style={styles.chartCol}>
            <View
              style={{
                height: `${20 + ((v - min) / range) * 80}%`,
                backgroundColor: color,
                borderRadius: 4,
                opacity: i === data.length - 1 ? 1 : 0.55,
              }}
            />
          </View>
        ))}
      </View>
      <View style={styles.rangeLabels}>
        <Text style={[typography.caption, { color: colors.muted }]}>min {Math.round(min)}</Text>
        <Text style={[typography.caption, { color: colors.muted }]}>latest {Math.round(data[data.length - 1])} · max {Math.round(max)}</Text>
      </View>
    </View>
  );
}

export function EmptyNote({ icon = 'checkmark-circle-outline', title, body }) {
  const { colors } = useTheme();
  return (
    <Card style={styles.empty}>
      <Ionicons name={icon} size={28} color={colors.accent} />
      <Text style={[typography.bodyStrong, { color: colors.text, marginTop: spacing.sm }]}>{title}</Text>
      {body ? <Text style={[typography.body, { color: colors.textSecondary, textAlign: 'center' }]}>{body}</Text> : null}
    </Card>
  );
}

/** "3 min ago" style timestamps. */
export function timeAgo(iso) {
  const t = new Date(iso).getTime();
  if (!Number.isFinite(t)) return '';
  const s = Math.max(0, Math.round((Date.now() - t) / 1000));
  if (s < 45) return 'just now';
  const m = Math.round(s / 60);
  if (m < 60) return `${m} min ago`;
  const h = Math.round(m / 60);
  if (h < 24) return `${h} h ago`;
  return new Date(iso).toLocaleDateString();
}

const styles = StyleSheet.create({
  flex: { flex: 1 },
  pageScroll: { paddingBottom: spacing.xxxl },
  pageInner: { width: '100%', alignSelf: 'center', paddingTop: spacing.lg },
  pageHeader: { marginBottom: spacing.xl, gap: spacing.md },
  pageHeaderWide: { flexDirection: 'row', alignItems: 'flex-end', paddingTop: spacing.md },
  pageSubtitle: { marginTop: spacing.xs, maxWidth: 720 },
  pageActions: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
  card: { borderRadius: radius.lg },
  sectionTitle: { flexDirection: 'row', alignItems: 'center', marginTop: spacing.lg, marginBottom: spacing.md, gap: spacing.md },
  grid: { flexDirection: 'row', flexWrap: 'wrap' },
  stat: { gap: spacing.xs, minHeight: 118 },
  statTop: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', marginBottom: spacing.xs },
  statIcon: { width: 30, height: 30, borderRadius: radius.sm, alignItems: 'center', justifyContent: 'center' },
  pill: {
    flexDirection: 'row', alignItems: 'center', gap: 6, alignSelf: 'flex-start',
    paddingHorizontal: 10, paddingVertical: 4, borderRadius: radius.pill,
  },
  pillDot: { width: 6, height: 6, borderRadius: 3 },
  button: {
    flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: spacing.sm,
    paddingHorizontal: spacing.lg, paddingVertical: 10, borderRadius: radius.md, minHeight: 40,
  },
  segmented: { gap: spacing.sm, paddingVertical: spacing.xs },
  field: { gap: 6 },
  input: { borderWidth: 1, borderRadius: radius.md, paddingHorizontal: spacing.md, paddingVertical: 10, fontSize: 14 },
  segment: { paddingHorizontal: 14, paddingVertical: 8, borderRadius: radius.pill, borderWidth: 1 },
  rangeTrack: { height: 10, borderRadius: radius.pill, justifyContent: 'center', marginTop: spacing.sm },
  rangeSafe: { position: 'absolute', left: '20%', width: '60%', height: 10, borderRadius: radius.pill, borderWidth: 1 },
  rangeMarker: { position: 'absolute', width: 16, height: 16, borderRadius: 8, marginLeft: -8, borderWidth: 3 },
  rangeLabels: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 6 },
  chart: { flexDirection: 'row', alignItems: 'flex-end', gap: 4 },
  chartCol: { flex: 1, height: '100%', justifyContent: 'flex-end' },
  chartEmpty: { borderRadius: radius.md, alignItems: 'center', justifyContent: 'center' },
  empty: { alignItems: 'center', gap: spacing.xs, paddingVertical: spacing.xl },
});
