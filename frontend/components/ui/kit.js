// Shared building blocks. Quiet by default: hairline borders, no icon tiles, colour only for status.
import React from 'react';
import { Pressable, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useTheme } from '../../contexts/ThemeContext';
import { radius, spacing, toneColors, typography } from '../../theme/tokens';
import { useLayout } from '../../utils/layout';
import Screen from '../Screen';

/** Scrollable page with a readable max width. */
export function Page({ title, subtitle, actions, children, scroll = true }) {
  const { colors } = useTheme();
  const { horizontalPadding, isWide } = useLayout();
  const body = (
    <View style={[styles.pageInner, { paddingHorizontal: horizontalPadding }]}>
      {title ? (
        <View style={[styles.pageHeader, isWide && styles.pageHeaderWide]}>
          <View style={styles.flex}>
            <Text style={[isWide ? typography.display : typography.headline, { color: colors.text }]}>{title}</Text>
            {subtitle ? <Text style={[typography.body, styles.pageSubtitle, { color: colors.textSecondary }]}>{subtitle}</Text> : null}
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
  const { colors } = useTheme();
  const base = [styles.card, { backgroundColor: colors.card, borderColor: colors.border }, padded && styles.cardPadded, style];
  if (onPress) {
    return (
      <Pressable
        onPress={onPress}
        accessibilityRole="button"
        accessibilityLabel={accessibilityLabel}
        style={({ pressed, hovered }) => [...base, (pressed || hovered) && { backgroundColor: colors.sunken }]}
      >
        {children}
      </Pressable>
    );
  }
  return <View style={base}>{children}</View>;
}

export function Divider({ inset = 0 }) {
  const { colors } = useTheme();
  return <View style={{ height: StyleSheet.hairlineWidth, backgroundColor: colors.border, marginLeft: inset }} />;
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

/** A labelled number. Only problems get colour. `icon` is accepted for compatibility and ignored. */
export function StatTile({ label, value, hint, tone = 'neutral' }) {
  const { colors } = useTheme();
  const color = tone === 'critical' || tone === 'warning' ? toneColors(colors, tone).fg : colors.text;
  return (
    <Card style={styles.stat}>
      <Text style={[typography.label, { color: colors.textSecondary }]} numberOfLines={1}>{label}</Text>
      <Text style={[typography.metric, { color }]} numberOfLines={1} adjustsFontSizeToFit>{value}</Text>
      {hint ? <Text style={[typography.caption, { color: colors.muted }]} numberOfLines={2}>{hint}</Text> : null}
    </Card>
  );
}

/** Status as a coloured dot and text — no filled badge. */
export function Pill({ label, tone = 'neutral', style }) {
  const { colors } = useTheme();
  const { fg } = toneColors(colors, tone);
  return (
    <View style={[styles.status, style]}>
      <View style={[styles.statusDot, { backgroundColor: fg }]} />
      <Text style={[typography.label, { color: tone === 'neutral' ? colors.textSecondary : fg }]} numberOfLines={1}>{label}</Text>
    </View>
  );
}

export function Button({ label, onPress, variant = 'primary', disabled, style }) {
  const { colors } = useTheme();
  const primary = variant === 'primary';
  return (
    <Pressable
      onPress={onPress}
      disabled={disabled}
      accessibilityRole="button"
      style={({ pressed, hovered }) => [
        styles.button,
        primary ? { backgroundColor: colors.primary } : { borderWidth: 1, borderColor: colors.border, backgroundColor: colors.card },
        (pressed || hovered) && { opacity: 0.82 },
        disabled && { opacity: 0.45 },
        style,
      ]}
    >
      <Text style={[typography.label, { fontWeight: '600', color: primary ? colors.onPrimary : colors.text }]}>{label}</Text>
    </Pressable>
  );
}

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
          { borderColor: colors.border, backgroundColor: colors.card, color: colors.text },
          multiline && { minHeight: 72, textAlignVertical: 'top' },
          style,
        ]}
        {...props}
      />
    </View>
  );
}

/** iOS-style segmented control. options: [{ key, label, count? }] */
export function Segmented({ options, value, onChange }) {
  const { colors, isDark } = useTheme();
  return (
    <ScrollView horizontal showsHorizontalScrollIndicator={false}>
      <View style={[styles.segmented, { backgroundColor: colors.sunken }]}>
        {options.map((o) => {
          const active = o.key === value;
          return (
            <Pressable
              key={o.key}
              onPress={() => onChange(o.key)}
              accessibilityRole="button"
              accessibilityState={{ selected: active }}
              style={[styles.segment, active && { backgroundColor: isDark ? colors.elevated : colors.card }]}
            >
              <Text style={[typography.label, { color: active ? colors.text : colors.textSecondary, fontWeight: active ? '600' : '500' }]}>
                {o.label}
                {o.count != null ? <Text style={{ color: colors.muted }}>{`  ${o.count}`}</Text> : null}
              </Text>
            </Pressable>
          );
        })}
      </View>
    </ScrollView>
  );
}

/** Where a reading sits against its safe range. The safe band is the middle 60% of the track. */
export function TempRangeBar({ value, min, max }) {
  const { colors } = useTheme();
  if (value == null || min == null || max == null) return null;
  const span = Math.max(max - min, 1);
  const lo = min - span / 3;
  const hi = max + span / 3;
  const pct = Math.min(100, Math.max(0, ((value - lo) / (hi - lo)) * 100));
  const inRange = value >= min && value <= max;
  return (
    <View>
      <View style={[styles.rangeTrack, { backgroundColor: colors.sunken }]}>
        <View style={[styles.rangeSafe, { backgroundColor: colors.border }]} />
        <View style={[styles.rangeMarker, { left: `${pct}%`, backgroundColor: inRange ? colors.text : colors.critical }]} />
      </View>
      <View style={styles.rangeLabels}>
        <Text style={[typography.caption, { color: colors.muted }]}>{min} °C</Text>
        <Text style={[typography.caption, { color: colors.muted }]}>safe range</Text>
        <Text style={[typography.caption, { color: colors.muted }]}>{max} °C</Text>
      </View>
    </View>
  );
}

/** Minimal bar chart scaled to the data's own min–max. */
export function BarChart({ values, color, height = 88, emptyText = 'Not enough history yet' }) {
  const { colors } = useTheme();
  const data = (values ?? []).filter((v) => typeof v === 'number' && Number.isFinite(v));
  if (data.length < 2) {
    return (
      <View style={[styles.chartEmpty, { height }]}>
        <Text style={[typography.caption, { color: colors.muted }]}>{emptyText}</Text>
      </View>
    );
  }
  const min = Math.min(...data);
  const max = Math.max(...data);
  const range = max - min || 1;
  const barColor = color ?? colors.text;
  return (
    <View>
      <View style={[styles.chart, { height }]}>
        {data.map((v, i) => (
          <View key={i} style={styles.chartCol}>
            <View
              style={{
                height: `${15 + ((v - min) / range) * 85}%`,
                backgroundColor: barColor,
                borderRadius: 2,
                opacity: i === data.length - 1 ? 1 : 0.3,
              }}
            />
          </View>
        ))}
      </View>
      <View style={styles.rangeLabels}>
        <Text style={[typography.caption, { color: colors.muted }]}>low {Math.round(min)}</Text>
        <Text style={[typography.caption, { color: colors.muted }]}>now {Math.round(data[data.length - 1])} · high {Math.round(max)}</Text>
      </View>
    </View>
  );
}

export function EmptyNote({ title, body }) {
  const { colors } = useTheme();
  return (
    <View style={[styles.empty, { borderColor: colors.border }]}>
      <Text style={[typography.bodyStrong, { color: colors.text }]}>{title}</Text>
      {body ? <Text style={[typography.body, { color: colors.textSecondary, textAlign: 'center' }]}>{body}</Text> : null}
    </View>
  );
}

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
  pageInner: { width: '100%', maxWidth: 1080, alignSelf: 'center', paddingTop: spacing.xl },
  pageHeader: { marginBottom: spacing.xl, gap: spacing.md },
  pageHeaderWide: { flexDirection: 'row', alignItems: 'flex-end' },
  pageSubtitle: { marginTop: spacing.xs, maxWidth: 640 },
  pageActions: { flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' },
  card: { borderRadius: radius.lg, borderWidth: StyleSheet.hairlineWidth, overflow: 'hidden' },
  cardPadded: { padding: spacing.lg },
  sectionTitle: { flexDirection: 'row', alignItems: 'flex-end', marginTop: spacing.xl, marginBottom: spacing.md, gap: spacing.md },
  grid: { flexDirection: 'row', flexWrap: 'wrap' },
  stat: { gap: spacing.xs },
  status: { flexDirection: 'row', alignItems: 'center', gap: 6, alignSelf: 'flex-start' },
  statusDot: { width: 7, height: 7, borderRadius: 4 },
  button: { alignItems: 'center', justifyContent: 'center', paddingHorizontal: spacing.lg, paddingVertical: 9, borderRadius: radius.md, minHeight: 38 },
  field: { gap: 6 },
  input: { borderWidth: 1, borderRadius: radius.md, paddingHorizontal: spacing.md, paddingVertical: 10, fontSize: 15 },
  segmented: { flexDirection: 'row', borderRadius: radius.md, padding: 3, gap: 2 },
  segment: { paddingHorizontal: 14, paddingVertical: 6, borderRadius: 8 },
  rangeTrack: { height: 6, borderRadius: radius.pill, justifyContent: 'center', marginTop: spacing.sm },
  rangeSafe: { position: 'absolute', left: '20%', width: '60%', height: 6 },
  rangeMarker: { position: 'absolute', width: 4, height: 16, borderRadius: 2, marginLeft: -2 },
  rangeLabels: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 6 },
  chart: { flexDirection: 'row', alignItems: 'flex-end', gap: 3 },
  chartCol: { flex: 1, height: '100%', justifyContent: 'flex-end' },
  chartEmpty: { alignItems: 'center', justifyContent: 'center' },
  empty: { alignItems: 'center', gap: spacing.xs, paddingVertical: spacing.xl, paddingHorizontal: spacing.lg, borderWidth: 1, borderStyle: 'dashed', borderRadius: radius.lg },
});
