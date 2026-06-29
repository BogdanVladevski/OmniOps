import React from 'react';
import { StyleSheet, View, StatusBar } from 'react-native';
import VehicleTracker from './components/VehicleTracker';

export default function App() {
  return (
    <View style={styles.container}>
      {/* Ensures the iPhone status bar icons (Time, Battery, Wi-Fi) stay legible over the map */}
      <StatusBar barStyle="dark-content" backgroundColor="transparent" translucent={true} />
      
      {/* Mounts your real-time tracking map component */}
      <VehicleTracker vehicleId="Truck-001" />
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#F2F2F7', 
  },
});