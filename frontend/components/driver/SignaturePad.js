// The receiver signs with a finger. Strokes are drawn live and turned into an image on save.
import React, { useMemo, useRef, useState } from 'react';
import { PanResponder, StyleSheet, Text, View } from 'react-native';
import Svg, { Path } from 'react-native-svg';
import { useTheme } from '../../contexts/ThemeContext';
import { radius, spacing, typography } from '../../theme/tokens';
import { hasInk } from '../../utils/signature';
import { Button } from '../ui/kit';

const toPath = (stroke) =>
  stroke.length === 1
    ? `M${stroke[0].x} ${stroke[0].y} l0.1 0`
    : stroke.map((p, i) => `${i ? 'L' : 'M'}${p.x.toFixed(1)} ${p.y.toFixed(1)}`).join(' ');

export default function SignaturePad({ hint, clearLabel, saveLabel, onSave, busy }) {
  const { colors } = useTheme();
  const [strokes, setStrokes] = useState([]);
  const size = useRef({ width: 1, height: 1 });
  const current = useRef(null);

  const point = (e) => ({ x: e.nativeEvent.locationX, y: e.nativeEvent.locationY });
  const responder = useMemo(() => PanResponder.create({
    onStartShouldSetPanResponder: () => true,
    onMoveShouldSetPanResponder: () => true,
    // Signing must not scroll the page.
    onPanResponderTerminationRequest: () => false,
    onPanResponderGrant: (e) => {
      current.current = [point(e)];
      setStrokes((s) => [...s, current.current]);
    },
    onPanResponderMove: (e) => {
      const stroke = current.current;
      if (!stroke) return;
      stroke.push(point(e));
      setStrokes((s) => [...s.slice(0, -1), [...stroke]]);
    },
    onPanResponderRelease: () => { current.current = null; },
  }), []);

  return (
    <View style={styles.wrap}>
      {hint ? <Text style={[typography.body, { color: colors.textSecondary }]}>{hint}</Text> : null}
      <View
        style={[styles.pad, { borderColor: colors.borderStrong ?? colors.border, backgroundColor: '#FFFFFF' }]}
        onLayout={(e) => { size.current = e.nativeEvent.layout; }}
        {...responder.panHandlers}
        accessibilityLabel="Signature box"
      >
        <Svg width="100%" height="100%" pointerEvents="none">
          {strokes.map((s, i) => (
            <Path key={i} d={toPath(s)} stroke="#14181D" strokeWidth={2.5} strokeLinecap="round" strokeLinejoin="round" fill="none" />
          ))}
        </Svg>
        <View pointerEvents="none" style={[styles.line, { borderColor: colors.border }]} />
      </View>
      <View style={styles.row}>
        <Button label={clearLabel} variant="secondary" onPress={() => setStrokes([])} disabled={!strokes.length || busy} style={styles.flex} />
        <Button
          label={saveLabel}
          onPress={() => onSave(strokes, size.current.width, size.current.height)}
          disabled={!hasInk(strokes) || busy}
          style={styles.flex}
        />
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  wrap: { gap: spacing.sm },
  pad: { height: 200, borderWidth: 1, borderRadius: radius.md, overflow: 'hidden' },
  line: { position: 'absolute', left: spacing.lg, right: spacing.lg, bottom: 44, borderBottomWidth: 1 },
  row: { flexDirection: 'row', gap: spacing.sm },
  flex: { flex: 1 },
});
