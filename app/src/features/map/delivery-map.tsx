// Phones: the platform map (Apple Maps / Google Maps).
import { useEffect, useRef } from 'react';
import { StyleSheet } from 'react-native';
import MapView, { Marker, Polyline, PROVIDER_DEFAULT } from 'react-native-maps';
import { INITIAL_REGION, type DeliveryMapProps } from './map-types';

export function DeliveryMap({ pins, lines = [], satellite, dark, focus }: DeliveryMapProps) {
  const map = useRef<MapView>(null);

  useEffect(() => {
    if (!focus || focus.coords.length === 0) return;
    if (focus.coords.length === 1) {
      map.current?.animateToRegion({ ...focus.coords[0]!, latitudeDelta: 0.3, longitudeDelta: 0.3 }, 600);
    } else {
      map.current?.fitToCoordinates(focus.coords, { edgePadding: focus.padding, animated: true });
    }
    // Re-frame only when the focus key changes, not on every position update.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [focus?.key]);

  return (
    <MapView
      ref={map}
      style={StyleSheet.absoluteFill}
      provider={PROVIDER_DEFAULT}
      mapType={satellite ? 'hybrid' : 'standard'}
      userInterfaceStyle={dark ? 'dark' : 'light'}
      initialRegion={INITIAL_REGION}
    >
      {lines.map((line) => (
        <Polyline
          key={line.id}
          coordinates={line.points}
          strokeColor={line.color}
          strokeWidth={2}
          lineDashPattern={line.dashed ? [6, 6] : undefined}
        />
      ))}
      {pins.map((pin) => (
        <Marker
          key={pin.id}
          coordinate={pin}
          title={pin.title}
          description={pin.description}
          pinColor={pin.color}
          opacity={pin.emphasis === false ? 0.6 : 1}
          onPress={pin.onPress}
        />
      ))}
    </MapView>
  );
}
