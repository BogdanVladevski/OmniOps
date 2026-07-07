import React, { useEffect, useRef } from 'react';
import { View, Animated, StyleSheet } from 'react-native';
import { useTheme } from '../../contexts/ThemeContext';

function Bone({ style, color }) {
  const opacity = useRef(new Animated.Value(0.4)).current;

  useEffect(() => {
    const loop = Animated.loop(
      Animated.sequence([
        Animated.timing(opacity, { toValue: 1, duration: 700, useNativeDriver: true }),
        Animated.timing(opacity, { toValue: 0.4, duration: 700, useNativeDriver: true }),
      ]),
    );
    loop.start();
    return () => loop.stop();
  }, [opacity]);

  return <Animated.View style={[style, { backgroundColor: color, opacity }]} />;
}

export function SkeletonCard({ lines = 3 }) {
  const { colors } = useTheme();
  return (
    <View style={[styles.card, { backgroundColor: colors.card }]}>
      <Bone style={styles.title} color={colors.skeleton} />
      {Array.from({ length: lines }).map((_, i) => (
        <Bone key={i} style={[styles.line, i === lines - 1 && styles.lineShort]} color={colors.skeleton} />
      ))}
    </View>
  );
}

export function SkeletonKpiGrid() {
  const { colors } = useTheme();
  return (
    <View style={styles.grid}>
      {[0, 1, 2, 3].map((i) => (
        <View key={i} style={[styles.kpi, { backgroundColor: colors.card }]}>
          <Bone style={styles.kpiLabel} color={colors.skeleton} />
          <Bone style={styles.kpiValue} color={colors.skeleton} />
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  card: { borderRadius: 14, padding: 16, marginBottom: 12 },
  title: { height: 18, width: '45%', borderRadius: 6, marginBottom: 12 },
  line: { height: 12, width: '90%', borderRadius: 4, marginBottom: 8 },
  lineShort: { width: '60%' },
  grid: { flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'space-between', marginBottom: 16 },
  kpi: { width: '48%', borderRadius: 16, padding: 16, marginBottom: 12 },
  kpiLabel: { height: 12, width: '50%', borderRadius: 4, marginBottom: 8 },
  kpiValue: { height: 22, width: '70%', borderRadius: 6 },
});
