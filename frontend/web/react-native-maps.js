// Web stand-in for react-native-maps, which only supports iOS/Android.
// Selected by metro.config.js for web builds only; native builds use the real library.
// Renders a map placeholder with one pill per marker so the rest of the app is usable in a browser.
import React, { forwardRef, useImperativeHandle } from 'react';
import { StyleSheet, Text, View } from 'react-native';

const MapView = forwardRef(function MapView({ style, children }, ref) {
  // Native-only camera methods become no-ops on web.
  useImperativeHandle(ref, () => ({
    animateToRegion() {},
    animateCamera() {},
    fitToCoordinates() {},
  }));

  return (
    <View style={[styles.map, style]}>
      <Text style={styles.caption}>Map preview (interactive map is available in the mobile app)</Text>
      <View style={styles.markers}>{children}</View>
    </View>
  );
});

export function Marker({ title, description, pinColor, coordinate }) {
  return (
    <View style={[styles.marker, { borderColor: pinColor || '#4b8bf4' }]}>
      <View style={[styles.dot, { backgroundColor: pinColor || '#4b8bf4' }]} />
      <View>
        <Text style={styles.markerTitle}>{title}</Text>
        {description ? <Text style={styles.markerText}>{description}</Text> : null}
        {coordinate ? (
          <Text style={styles.markerText}>
            {coordinate.latitude.toFixed(4)}, {coordinate.longitude.toFixed(4)}
          </Text>
        ) : null}
      </View>
    </View>
  );
}

const Noop = () => null;
export const Circle = Noop;
export const Polygon = Noop;
export const Polyline = Noop;
export const Callout = Noop;
export const PROVIDER_DEFAULT = null;
export const PROVIDER_GOOGLE = 'google';

export default MapView;

const styles = StyleSheet.create({
  map: { backgroundColor: '#1c2733', padding: 16, justifyContent: 'flex-start' },
  caption: { color: '#9fb3c8', fontSize: 12, marginBottom: 12 },
  markers: { flexDirection: 'row', flexWrap: 'wrap', gap: 8 },
  marker: {
    flexDirection: 'row', alignItems: 'center', gap: 8, borderWidth: 1, borderRadius: 10,
    paddingVertical: 6, paddingHorizontal: 10, backgroundColor: '#243447',
  },
  dot: { width: 10, height: 10, borderRadius: 5 },
  markerTitle: { color: '#ffffff', fontWeight: '600', fontSize: 13 },
  markerText: { color: '#9fb3c8', fontSize: 11 },
});
