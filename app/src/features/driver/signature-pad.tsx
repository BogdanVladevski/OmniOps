// The receiver signs with a finger. Strokes are drawn live and turned into an image on save.
import { useState } from 'react';
import { PanResponder, StyleSheet, Text, View, type GestureResponderEvent } from 'react-native';
import Svg, { Path } from 'react-native-svg';
import { Button } from '@/components/ui';
import { useTheme } from '@/theme/theme-provider';
import { radius, spacing, typography } from '@/theme/tokens';
import { hasInk, type StrokePoint } from './offline/signature';

const toPath = (stroke: StrokePoint[]) =>
  stroke.length === 1
    ? `M${stroke[0]!.x} ${stroke[0]!.y} l0.1 0`
    : stroke.map((p, i) => `${i ? 'L' : 'M'}${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ');

export function SignaturePad({
  hint,
  clearLabel,
  saveLabel,
  onSave,
  busy,
}: {
  hint?: string;
  clearLabel: string;
  saveLabel: string;
  onSave: (strokes: StrokePoint[][], width: number, height: number) => void;
  busy: boolean;
}) {
  const { colors } = useTheme();
  const [strokes, setStrokes] = useState<StrokePoint[][]>([]);
  const [size, setSize] = useState({ width: 1, height: 1 });

  const [responder] = useState(() => {
    let current: StrokePoint[] | null = null; // the stroke being drawn
    const point = (e: GestureResponderEvent): StrokePoint => ({
      x: e.nativeEvent.locationX,
      y: e.nativeEvent.locationY,
    });
    return PanResponder.create({
      onStartShouldSetPanResponder: () => true,
      onMoveShouldSetPanResponder: () => true,
      // Signing must not scroll the page.
      onPanResponderTerminationRequest: () => false,
      onPanResponderGrant: (e) => {
        const stroke = [point(e)];
        current = stroke;
        setStrokes((s) => [...s, stroke]);
      },
      onPanResponderMove: (e) => {
        const stroke = current;
        if (!stroke) return;
        stroke.push(point(e));
        setStrokes((s) => [...s.slice(0, -1), [...stroke]]);
      },
      onPanResponderRelease: () => {
        current = null;
      },
    });
  });

  return (
    <View style={styles.wrap}>
      {hint ? <Text style={[typography.body, { color: colors.textSecondary }]}>{hint}</Text> : null}
      <View
        style={[styles.pad, { borderColor: colors.border }]}
        onLayout={(e) => setSize({ width: e.nativeEvent.layout.width, height: e.nativeEvent.layout.height })}
        {...responder.panHandlers}
        accessibilityLabel="Signature box"
      >
        <Svg width="100%" height="100%" pointerEvents="none">
          {strokes.map((s, i) => (
            <Path
              key={i}
              d={toPath(s)}
              stroke="#14181D"
              strokeWidth={2.5}
              strokeLinecap="round"
              strokeLinejoin="round"
              fill="none"
            />
          ))}
        </Svg>
        <View pointerEvents="none" style={[styles.line, { borderColor: colors.border }]} />
      </View>
      <View style={styles.row}>
        <Button
          label={clearLabel}
          variant="secondary"
          onPress={() => setStrokes([])}
          disabled={!strokes.length || busy}
          style={styles.flex}
        />
        <Button
          label={saveLabel}
          onPress={() => onSave(strokes, size.width, size.height)}
          disabled={!hasInk(strokes) || busy}
          style={styles.flex}
        />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { gap: spacing.sm },
  // The pad is always white: the saved signature is dark ink on white, whatever the theme.
  pad: { height: 200, borderWidth: 1, borderRadius: radius.md, overflow: 'hidden', backgroundColor: '#FFFFFF' },
  line: { position: 'absolute', left: spacing.lg, right: spacing.lg, bottom: 44, borderBottomWidth: 1 },
  row: { flexDirection: 'row', gap: spacing.sm },
  flex: { flex: 1 },
});
