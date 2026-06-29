import React, { useEffect, useState, useRef } from 'react';
import { Text, View, StyleSheet, ActivityIndicator, Dimensions } from 'react-native';
import MapView, { Marker, PROVIDER_DEFAULT } from 'react-native-maps';
import * as SignalR from '@microsoft/signalr';

export default function VehicleTracker({ vehicleId = "Truck-001" }) {
  const [telemetry, setTelemetry] = useState(null);
  const [status, setStatus] = useState("Connecting to tracking engine...");
  const mapRef = useRef(null);

  useEffect(() => {
    //connecting to my backend API
    const connection = new SignalR.HubConnectionBuilder()
      .withUrl(`${process.env.EXPO_PUBLIC_API_URL}/api/stream/telemetry`) 
      .withAutomaticReconnect()
      .configureLogging(SignalR.LogLevel.Information)
      .build();

    connection.start()
      .then(() => {
        setStatus("Live Map Connected.");
        connection.invoke("WatchVehicle", vehicleId);
      })
      .catch(err => {
        setStatus("Map Link Offline.");
        console.error("SignalR Error: ", err);
      });

    //real time telemtry
    connection.on("ReceiveTelemetryUpdate", (data) => {
      setTelemetry(data);

      if (mapRef.current && data.latitude && data.longitude) {
        mapRef.current.animateToRegion({
          latitude: data.latitude,
          longitude: data.longitude,
          latitudeDelta: 0.015,
          longitudeDelta: 0.015,
        }, 1000); 
      }
    });

    return () => { connection.stop(); };
  }, [vehicleId]);

  const initialRegion = {
    latitude: 41.9965,
    longitude: 21.4314,
    latitudeDelta: 0.05,
    longitudeDelta: 0.05,
  };

  return (
    <View style={styles.container}>
      {/* The Native Map Layer */}
      <MapView
        ref={mapRef}
        style={styles.map}
        provider={PROVIDER_DEFAULT}
        initialRegion={initialRegion}
        showsUserLocation={true}
      >
        {telemetry && telemetry.latitude && telemetry.longitude && (
          <Marker
            coordinate={{ latitude: telemetry.latitude, longitude: telemetry.longitude }}
            title={vehicleId}
            description={`Speed: ${telemetry.speed} km/h | Fuel: ${telemetry.fuelLevel}%`}
            pinColor="#007AFF"
          />
        )}
      </MapView>

      {/* Floating HUD Telemetry Overlay Panel */}
      <View style={styles.overlayPanel}>
        <Text style={styles.title}>Target: {vehicleId}</Text>
        <Text style={styles.statusLabel}>{status}</Text>
        
        {telemetry ? (
          <View style={styles.statsGrid}>
            <View style={styles.statBox}>
              <Text style={styles.statLabel}>SPEED</Text>
              <Text style={styles.statValue}>{telemetry.speed} km/h</Text>
            </View>
            <View style={styles.statBox}>
              <Text style={styles.statLabel}>FUEL</Text>
              <Text style={styles.statValue}>{telemetry.fuelLevel}%</Text>
            </View>
            <View style={styles.statBox}>
              <Text style={styles.statLabel}>TEMP</Text>
              <Text style={styles.statValue}>{telemetry.engineTemperature}°C</Text>
            </View>
          </View>
        ) : (
          <ActivityIndicator size="small" color="#007AFF" style={{ marginTop: 10 }} />
        )}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    ...StyleSheet.absoluteFillObject,
    justifyContent: 'flex-end',
    alignItems: 'center',
  },
  map: {
    ...StyleSheet.absoluteFillObject,
  },
  overlayPanel: {
    position: 'absolute',
    bottom: 30,
    width: Dimensions.get('window').width * 0.92,
    backgroundColor: 'rgba(255, 255, 255, 0.95)',
    borderRadius: 20,
    padding: 20,
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.15,
    shadowRadius: 10,
    elevation: 8,
  },
  title: { fontSize: 20, fontWeight: '700', color: '#1C1C1E' },
  statusLabel: { fontSize: 13, color: '#8E8E93', marginBottom: 12 },
  statsGrid: { flexDirection: 'row', justifyContent: 'space-between', marginTop: 4 },
  statBox: { alignItems: 'center', flex: 1 },
  statLabel: { fontSize: 11, fontWeight: '600', color: '#8E8E93', marginBottom: 2 },
  statValue: { fontSize: 16, fontWeight: '700', color: '#1C1C1E' }
});