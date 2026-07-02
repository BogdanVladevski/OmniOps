import React, { useState } from 'react';
import { View, Text, StyleSheet, ScrollView, Pressable, Modal } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useFleet } from '../contexts/FleetContext';
import { useLayout } from '../utils/layout';
import Screen from './Screen';

export default function AlertsFeed() {
  const { alerts } = useFleet();
  const [selectedAlert, setSelectedAlert] = useState(null);
  const insets = useSafeAreaInsets();
  const { horizontalPadding, titleSize, bodySize, cardPadding } = useLayout();

  return (
    <Screen style={styles.screen}>
      <View style={[styles.container, { paddingHorizontal: horizontalPadding }]}>
        <Text style={[styles.heading, { fontSize: titleSize }]}>Live Alerts</Text>
        <Text style={[styles.subheading, { fontSize: bodySize }]}>
          Anomaly playbook responses stream here in real time.
        </Text>

        {alerts.length === 0 ? (
          <View style={[styles.emptyState, { padding: cardPadding + 4 }]}>
            <Text style={styles.emptyTitle}>No alerts yet</Text>
            <Text style={[styles.emptyBody, { fontSize: bodySize }]}>
              Simulate telemetry with dropping fuel and rising engine temperature to trigger anomaly detection.
            </Text>
          </View>
        ) : (
          <ScrollView contentContainerStyle={styles.list} showsVerticalScrollIndicator={false}>
            {alerts.map((alert) => (
              <Pressable key={alert.id} style={[styles.alertCard, { padding: cardPadding }]} onPress={() => setSelectedAlert(alert)}>
                <Text style={styles.alertVehicle}>{alert.vehicleId}</Text>
                <Text style={[styles.alertPreview, { fontSize: bodySize }]} numberOfLines={3}>
                  {alert.instructions}
                </Text>
                <Text style={styles.alertTime}>{new Date(alert.generatedAt).toLocaleString()}</Text>
              </Pressable>
            ))}
          </ScrollView>
        )}
      </View>

      <Modal visible={!!selectedAlert} animationType="slide" transparent onRequestClose={() => setSelectedAlert(null)}>
        <View style={styles.modalBackdrop}>
          <View style={[styles.modalCard, { paddingBottom: Math.max(insets.bottom, 16) + 12 }]}>
            <Text style={styles.modalTitle}>{selectedAlert?.vehicleId}</Text>
            <ScrollView style={styles.modalBody}>
              <Text style={[styles.modalInstructions, { fontSize: bodySize }]}>{selectedAlert?.instructions}</Text>
            </ScrollView>
            <Pressable style={styles.closeButton} onPress={() => setSelectedAlert(null)}>
              <Text style={styles.closeButtonText}>Close</Text>
            </Pressable>
          </View>
        </View>
      </Modal>
    </Screen>
  );
}

const styles = StyleSheet.create({
  screen: { flex: 1 },
  container: { flex: 1, paddingTop: 8 },
  heading: { fontWeight: '700', color: '#1C1C1E' },
  subheading: { color: '#8E8E93', marginBottom: 16, marginTop: 4 },
  emptyState: {
    marginTop: 12,
    backgroundColor: '#FFFFFF',
    borderRadius: 16,
  },
  emptyTitle: { fontSize: 18, fontWeight: '700', color: '#1C1C1E', marginBottom: 8 },
  emptyBody: { color: '#636366', lineHeight: 20 },
  list: { paddingBottom: 24 },
  alertCard: {
    backgroundColor: '#FFFFFF',
    borderRadius: 14,
    marginBottom: 10,
    borderLeftWidth: 4,
    borderLeftColor: '#FF3B30',
  },
  alertVehicle: { fontSize: 16, fontWeight: '700', color: '#1C1C1E', marginBottom: 6 },
  alertPreview: { color: '#3A3A3C', lineHeight: 18 },
  alertTime: { fontSize: 12, color: '#8E8E93', marginTop: 8 },
  modalBackdrop: {
    flex: 1,
    backgroundColor: 'rgba(0,0,0,0.4)',
    justifyContent: 'flex-end',
  },
  modalCard: {
    backgroundColor: '#FFFFFF',
    borderTopLeftRadius: 20,
    borderTopRightRadius: 20,
    padding: 20,
    maxHeight: '85%',
  },
  modalTitle: { fontSize: 20, fontWeight: '700', marginBottom: 12 },
  modalBody: { marginBottom: 16, maxHeight: '70%' },
  modalInstructions: { color: '#1C1C1E', lineHeight: 22 },
  closeButton: {
    backgroundColor: '#007AFF',
    borderRadius: 12,
    paddingVertical: 12,
    alignItems: 'center',
  },
  closeButtonText: { color: '#FFFFFF', fontWeight: '700', fontSize: 16 },
});
