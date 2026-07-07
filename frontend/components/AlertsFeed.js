import React, { useState } from 'react';
import { View, Text, StyleSheet, ScrollView, Pressable, Modal } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';
import { useFleet } from '../contexts/FleetContext';
import { useLayout } from '../utils/layout';
import { resolveShipmentIdForVehicle } from '../utils/fleetConfig';
import Screen from './Screen';
import IncidentReplay from './IncidentReplay';

function alertBorderColor(alertType) {
  if (alertType === 'GeofenceBreach') return '#5856D6';
  if (alertType === 'Overspeed' || alertType === 'HarshBraking') return '#FF9500';
  if (alertType === 'TemperatureExcursion') return '#FF3B30';
  return '#FF3B30';
}

function alertIcon(alertType) {
  if (alertType === 'GeofenceBreach') return '⬡';
  if (alertType === 'playbook') return '🧊';
  return '⚠';
}

export default function AlertsFeed() {
  const { alerts, vehicles } = useFleet();
  const [selectedAlert, setSelectedAlert] = useState(null);
  const [replayAlert, setReplayAlert] = useState(null);
  const insets = useSafeAreaInsets();
  const { horizontalPadding, titleSize, bodySize, cardPadding } = useLayout();

  const apiBaseUrl = process.env.EXPO_PUBLIC_API_URL;
  const apiToken = process.env.EXPO_PUBLIC_API_TOKEN;

  const openReplay = (alert) => {
    setSelectedAlert(null);
    setReplayAlert(alert);
  };

  const replayShipmentId = replayAlert
    ? resolveShipmentIdForVehicle(replayAlert.vehicleId, vehicles)
    : null;

  return (
    <Screen style={styles.screen}>
      <View style={[styles.container, { paddingHorizontal: horizontalPadding }]}>
        <Text style={[styles.heading, { fontSize: titleSize }]}>Incident Alerts</Text>
        <Text style={[styles.subheading, { fontSize: bodySize }]}>
          Cold-chain excursions and incident response protocols stream here in real time.
        </Text>

        {alerts.length === 0 ? (
          <View style={[styles.emptyState, { padding: cardPadding + 4 }]}>
            <Text style={styles.emptyTitle}>No incidents yet</Text>
            <Text style={[styles.emptyBody, { fontSize: bodySize }]}>
              Simulate telemetry to trigger cargo temperature excursion detection. Alerts appear here when a shipment breaches its safe temperature range.
            </Text>
          </View>
        ) : (
          <ScrollView contentContainerStyle={styles.list} showsVerticalScrollIndicator={false}>
            {alerts.map((alert) => (
              <Pressable
                key={alert.id}
                style={[styles.alertCard, { padding: cardPadding, borderLeftColor: alertBorderColor(alert.alertType) }]}
                onPress={() => setSelectedAlert(alert)}
              >
                <Text style={styles.alertVehicle}>
                  {alertIcon(alert.alertType)} {alert.vehicleId}
                  {alert.alertType !== 'playbook' ? ` · ${alert.alertType}` : ''}
                </Text>
                {alert.title && alert.alertType !== 'playbook' ? (
                  <Text style={styles.alertTitle}>{alert.title}</Text>
                ) : null}
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
            <Pressable
              style={styles.replayButton}
              onPress={() => openReplay(selectedAlert)}
            >
              <Text style={styles.replayButtonText}>Replay Incident</Text>
            </Pressable>
            <Pressable style={styles.closeButton} onPress={() => setSelectedAlert(null)}>
              <Text style={styles.closeButtonText}>Close</Text>
            </Pressable>
          </View>
        </View>
      </Modal>

      <IncidentReplay
        visible={!!replayAlert}
        alert={replayAlert}
        shipmentId={replayShipmentId}
        apiBaseUrl={apiBaseUrl}
        apiToken={apiToken}
        onClose={() => setReplayAlert(null)}
      />
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
  },
  alertVehicle: { fontSize: 16, fontWeight: '700', color: '#1C1C1E', marginBottom: 4 },
  alertTitle: { fontSize: 14, fontWeight: '600', color: '#3A3A3C', marginBottom: 4 },
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
  replayButton: {
    backgroundColor: '#FF9500',
    borderRadius: 12,
    paddingVertical: 12,
    alignItems: 'center',
    marginBottom: 10,
  },
  replayButtonText: { color: '#FFFFFF', fontWeight: '700', fontSize: 16 },
  closeButton: {
    backgroundColor: '#007AFF',
    borderRadius: 12,
    paddingVertical: 12,
    alignItems: 'center',
  },
  closeButtonText: { color: '#FFFFFF', fontWeight: '700', fontSize: 16 },
});
