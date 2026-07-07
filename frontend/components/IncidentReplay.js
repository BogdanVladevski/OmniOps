import React, { useEffect, useMemo, useRef, useState } from 'react';
import {
  View,
  Text,
  StyleSheet,
  Modal,
  Pressable,
  ActivityIndicator,
  PanResponder,
} from 'react-native';
import MapView, { Marker, Polyline, PROVIDER_DEFAULT } from 'react-native-maps';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { buildReplayWindow, fetchShipmentReplay } from '../utils/replayApi';
import { useLayout } from '../utils/layout';

function isExcursion(temp, minSafe, maxSafe) {
  return temp < minSafe || temp > maxSafe;
}

function TemperatureChart({ points, minSafe, maxSafe, currentIndex }) {
  const chartHeight = 120;
  const temps = points.map((point) => point.engineTemperature);
  const minTemp = Math.min(minSafe, ...temps) - 2;
  const maxTemp = Math.max(maxSafe, ...temps) + 2;
  const range = Math.max(maxTemp - minTemp, 1);

  const toY = (temp) => chartHeight - ((temp - minTemp) / range) * chartHeight;

  return (
    <View style={styles.chartContainer}>
      <Text style={styles.chartTitle}>Cargo temperature (°C)</Text>
      <View style={[styles.chartPlot, { height: chartHeight }]}>
        <View
          style={[
            styles.safeBand,
            {
              top: toY(maxSafe),
              height: Math.max(toY(minSafe) - toY(maxSafe), 2),
            },
          ]}
        />
        <View style={[styles.safeLine, { top: toY(maxSafe) }]} />
        <View style={[styles.safeLine, { top: toY(minSafe) }]} />

        {points.map((point, index) => {
          const left = points.length === 1 ? '50%' : `${(index / (points.length - 1)) * 100}%`;
          const excursion = isExcursion(point.engineTemperature, minSafe, maxSafe);
          const active = index === currentIndex;

          return (
            <View
              key={point.id ?? `${point.timestamp}-${index}`}
              style={[
                styles.chartPoint,
                {
                  left,
                  bottom: toY(point.engineTemperature),
                  backgroundColor: excursion ? '#FF3B30' : '#34C759',
                  transform: [{ scale: active ? 1.35 : 1 }],
                },
              ]}
            />
          );
        })}
      </View>
      <View style={styles.chartLegend}>
        <Text style={styles.legendText}>Safe {minSafe}–{maxSafe}°C</Text>
        <Text style={styles.legendText}>Red = excursion</Text>
      </View>
    </View>
  );
}

export default function IncidentReplay({ visible, alert, shipmentId, onClose, apiBaseUrl, apiToken }) {
  const insets = useSafeAreaInsets();
  const { bodySize } = useLayout();
  const mapRef = useRef(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState(null);
  const [replay, setReplay] = useState(null);
  const [currentIndex, setCurrentIndex] = useState(0);
  const [playing, setPlaying] = useState(false);

  const points = replay?.points ?? [];
  const currentPoint = points[currentIndex] ?? null;

  useEffect(() => {
    if (!visible || !shipmentId || !apiBaseUrl || !alert?.generatedAt) {
      return undefined;
    }

    let cancelled = false;

    const loadReplay = async () => {
      setLoading(true);
      setError(null);
      setReplay(null);
      setCurrentIndex(0);
      setPlaying(false);

      try {
        const { fromUtc, toUtc } = buildReplayWindow(alert.generatedAt);
        const payload = await fetchShipmentReplay({
          apiBaseUrl,
          apiToken,
          shipmentId,
          fromUtc,
          toUtc,
        });

        if (!cancelled) {
          setReplay(payload);
        }
      } catch (loadError) {
        if (!cancelled) {
          setError(loadError.message ?? 'Failed to load incident replay.');
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    loadReplay();

    return () => {
      cancelled = true;
    };
  }, [visible, shipmentId, apiBaseUrl, apiToken, alert?.generatedAt]);

  useEffect(() => {
    if (!playing || points.length <= 1) {
      return undefined;
    }

    const timer = setInterval(() => {
      setCurrentIndex((prev) => (prev >= points.length - 1 ? 0 : prev + 1));
    }, 900);

    return () => clearInterval(timer);
  }, [playing, points.length]);

  useEffect(() => {
    if (!currentPoint || !mapRef.current) {
      return;
    }

    mapRef.current.animateToRegion(
      {
        latitude: currentPoint.latitude,
        longitude: currentPoint.longitude,
        latitudeDelta: 0.02,
        longitudeDelta: 0.02,
      },
      500,
    );
  }, [currentPoint]);

  const pathCoordinates = useMemo(
    () => points.map((point) => ({ latitude: point.latitude, longitude: point.longitude })),
    [points],
  );

  const scrubResponder = useMemo(
    () => PanResponder.create({
      onStartShouldSetPanResponder: () => points.length > 1,
      onMoveShouldSetPanResponder: () => points.length > 1,
      onPanResponderGrant: (event) => {
        const width = event.nativeEvent.target?.offsetWidth ?? 1;
        const ratio = Math.min(Math.max(event.nativeEvent.locationX / width, 0), 1);
        const nextIndex = Math.round(ratio * (points.length - 1));
        setCurrentIndex(nextIndex);
        setPlaying(false);
      },
      onPanResponderMove: (event, gestureState) => {
        const width = Math.max(gestureState.moveX, 1);
        const ratio = Math.min(Math.max(event.nativeEvent.locationX / width, 0), 1);
        const nextIndex = Math.round(ratio * (points.length - 1));
        setCurrentIndex(nextIndex);
      },
    }),
    [points.length],
  );

  const step = (delta) => {
    setPlaying(false);
    setCurrentIndex((prev) => Math.min(Math.max(prev + delta, 0), Math.max(points.length - 1, 0)));
  };

  return (
    <Modal visible={visible} animationType="slide" onRequestClose={onClose}>
      <View style={[styles.container, { paddingTop: insets.top + 8, paddingBottom: insets.bottom + 12 }]}>
        <View style={styles.header}>
          <View style={styles.headerText}>
            <Text style={styles.title}>Incident Replay</Text>
            <Text style={[styles.subtitle, { fontSize: bodySize }]}>
              {replay?.productName ?? alert?.vehicleId} · {replay?.batchNumber ?? 'loading shipment'}
            </Text>
          </View>
          <Pressable onPress={onClose} style={styles.closeChip}>
            <Text style={styles.closeChipText}>Close</Text>
          </Pressable>
        </View>

        {loading ? (
          <View style={styles.centered}>
            <ActivityIndicator size="large" color="#007AFF" />
            <Text style={styles.statusText}>Loading telemetry window…</Text>
          </View>
        ) : error ? (
          <View style={styles.centered}>
            <Text style={styles.errorText}>{error}</Text>
          </View>
        ) : points.length === 0 ? (
          <View style={styles.centered}>
            <Text style={styles.statusText}>
              No telemetry stored for this incident window. Simulate packets first, then trigger an alert.
            </Text>
          </View>
        ) : (
          <>
            <View style={styles.mapWrap}>
              <MapView
                ref={mapRef}
                style={styles.map}
                provider={PROVIDER_DEFAULT}
                initialRegion={{
                  latitude: points[0].latitude,
                  longitude: points[0].longitude,
                  latitudeDelta: 0.05,
                  longitudeDelta: 0.05,
                }}
              >
                {pathCoordinates.length > 1 ? (
                  <Polyline coordinates={pathCoordinates} strokeColor="#007AFF" strokeWidth={3} />
                ) : null}
                {currentPoint ? (
                  <Marker
                    coordinate={{ latitude: currentPoint.latitude, longitude: currentPoint.longitude }}
                    title={replay?.vehicleId}
                    description={`${currentPoint.engineTemperature.toFixed(1)}°C`}
                    pinColor="#FF3B30"
                  />
                ) : null}
              </MapView>
            </View>

            <TemperatureChart
              points={points}
              minSafe={replay.minSafeTempCelsius}
              maxSafe={replay.maxSafeTempCelsius}
              currentIndex={currentIndex}
            />

            <View style={styles.scrubberBlock}>
              <Text style={styles.timestamp}>
                {currentPoint ? new Date(currentPoint.timestamp).toLocaleString() : '—'}
              </Text>
              <View style={styles.scrubberTrack} {...scrubResponder.panHandlers}>
                <View
                  style={[
                    styles.scrubberFill,
                    { width: `${points.length <= 1 ? 100 : (currentIndex / (points.length - 1)) * 100}%` },
                  ]}
                />
              </View>
              <View style={styles.controls}>
                <Pressable style={styles.controlButton} onPress={() => step(-1)}>
                  <Text style={styles.controlButtonText}>◀</Text>
                </Pressable>
                <Pressable style={styles.playButton} onPress={() => setPlaying((prev) => !prev)}>
                  <Text style={styles.playButtonText}>{playing ? 'Pause' : 'Play'}</Text>
                </Pressable>
                <Pressable style={styles.controlButton} onPress={() => step(1)}>
                  <Text style={styles.controlButtonText}>▶</Text>
                </Pressable>
              </View>
              <Text style={styles.frameCounter}>
                Frame {currentIndex + 1} / {points.length}
              </Text>
            </View>
          </>
        )}
      </View>
    </Modal>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F2F2F7',
    paddingHorizontal: 16,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: 12,
  },
  headerText: { flex: 1, paddingRight: 12 },
  title: { fontSize: 22, fontWeight: '700', color: '#1C1C1E' },
  subtitle: { color: '#636366', marginTop: 4 },
  closeChip: {
    backgroundColor: '#E5E5EA',
    borderRadius: 16,
    paddingHorizontal: 12,
    paddingVertical: 8,
  },
  closeChipText: { fontWeight: '600', color: '#1C1C1E' },
  centered: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: 24,
  },
  statusText: { color: '#636366', textAlign: 'center', lineHeight: 22, marginTop: 12 },
  errorText: { color: '#FF3B30', textAlign: 'center', lineHeight: 22 },
  mapWrap: {
    height: 260,
    borderRadius: 16,
    overflow: 'hidden',
    marginBottom: 12,
  },
  map: { flex: 1 },
  chartContainer: {
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    padding: 14,
    marginBottom: 12,
  },
  chartTitle: { fontWeight: '700', color: '#1C1C1E', marginBottom: 8 },
  chartPlot: {
    position: 'relative',
    backgroundColor: '#F8F8FA',
    borderRadius: 10,
    overflow: 'hidden',
  },
  safeBand: {
    position: 'absolute',
    left: 0,
    right: 0,
    backgroundColor: 'rgba(52, 199, 89, 0.18)',
  },
  safeLine: {
    position: 'absolute',
    left: 0,
    right: 0,
    height: 1,
    backgroundColor: '#34C759',
  },
  chartPoint: {
    position: 'absolute',
    width: 10,
    height: 10,
    borderRadius: 5,
    marginLeft: -5,
    marginBottom: -5,
  },
  chartLegend: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    marginTop: 8,
  },
  legendText: { fontSize: 12, color: '#8E8E93' },
  scrubberBlock: {
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
    padding: 14,
  },
  timestamp: { fontSize: 13, color: '#636366', marginBottom: 8 },
  scrubberTrack: {
    height: 8,
    borderRadius: 4,
    backgroundColor: '#E5E5EA',
    overflow: 'hidden',
    marginBottom: 12,
  },
  scrubberFill: {
    height: '100%',
    backgroundColor: '#007AFF',
  },
  controls: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 12,
    marginBottom: 8,
  },
  controlButton: {
    width: 44,
    height: 44,
    borderRadius: 22,
    backgroundColor: '#E5E5EA',
    alignItems: 'center',
    justifyContent: 'center',
  },
  controlButtonText: { fontSize: 16, fontWeight: '700', color: '#1C1C1E' },
  playButton: {
    backgroundColor: '#007AFF',
    borderRadius: 12,
    paddingHorizontal: 20,
    paddingVertical: 10,
  },
  playButtonText: { color: '#FFFFFF', fontWeight: '700' },
  frameCounter: { textAlign: 'center', color: '#8E8E93', fontSize: 12 },
});
