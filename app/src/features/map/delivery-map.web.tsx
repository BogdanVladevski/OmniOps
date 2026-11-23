// The web: Leaflet with OpenStreetMap tiles (street) and Esri imagery (satellite). Development and demos only: OSM's
// public tile servers forbid production traffic, so a paid tile plan comes with launch.
import 'leaflet/dist/leaflet.css';
import L from 'leaflet';
import { useEffect, useState } from 'react';
import { StyleSheet, View } from 'react-native';
import { MapContainer, Marker, Polyline, TileLayer, Tooltip } from 'react-leaflet';
import { INITIAL_REGION, type Coord, type DeliveryMapProps } from './map-types';

const TILES = {
  street: {
    url: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
  },
  satellite: {
    url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
    attribution: 'Imagery &copy; Esri',
  },
};

// OSM has no free dark style: dim and invert the street tiles instead.
if (typeof document !== 'undefined' && !document.getElementById('omniops-map-css')) {
  const style = document.createElement('style');
  style.id = 'omniops-map-css';
  style.textContent =
    '.omniops-dark-tiles{filter:invert(1) hue-rotate(180deg) brightness(.9) contrast(.9) saturate(.6)}' +
    '.omniops-light-tiles{filter:saturate(.55) contrast(.95)}';
  document.head.appendChild(style);
}

const toLatLng = (c: Coord): L.LatLngTuple => [c.latitude, c.longitude];

function dot(color: string, emphasis: boolean) {
  const size = emphasis ? 18 : 14;
  return L.divIcon({
    className: '',
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
    html: `<div style="width:${size}px;height:${size}px;border-radius:50%;background:${color};border:2.5px solid #fff;box-shadow:0 1px 4px rgba(0,0,0,.35)"></div>`,
  });
}

export function DeliveryMap({ pins, lines = [], satellite, dark, focus }: DeliveryMapProps) {
  const [map, setMap] = useState<L.Map | null>(null);

  useEffect(() => {
    if (!map || !focus || focus.coords.length === 0) return;
    if (focus.coords.length === 1) {
      map.flyTo(toLatLng(focus.coords[0]!), 11, { duration: 0.6 });
      return;
    }
    // Per-side padding: a floating panel on one side shouldn't shrink the map on every side.
    const p = focus.padding;
    map.fitBounds(L.latLngBounds(focus.coords.map(toLatLng)), {
      paddingTopLeft: [p.left, p.top],
      paddingBottomRight: [p.right, p.bottom],
      maxZoom: 13,
    });
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [map, focus?.key]);

  const tiles = satellite ? TILES.satellite : TILES.street;
  const tileClass = satellite ? undefined : dark ? 'omniops-dark-tiles' : 'omniops-light-tiles';
  return (
    <View style={StyleSheet.absoluteFill}>
      <MapContainer
        ref={setMap}
        center={toLatLng(INITIAL_REGION)}
        zoom={8}
        zoomControl={false}
        style={{ height: '100%', width: '100%', background: dark ? '#1b1d21' : '#eef0f2' }}
      >
        <TileLayer
          key={`${tiles.url}|${tileClass}`}
          url={tiles.url}
          attribution={tiles.attribution}
          className={tileClass}
        />
        {lines.map((line) => (
          <Polyline
            key={line.id}
            positions={line.points.map(toLatLng)}
            pathOptions={{ color: line.color, weight: 2, dashArray: line.dashed ? '6 6' : undefined }}
          />
        ))}
        {pins.map((pin) => (
          <Marker
            key={pin.id}
            position={toLatLng(pin)}
            icon={dot(pin.color, pin.emphasis !== false)}
            opacity={pin.emphasis === false ? 0.6 : 1}
            eventHandlers={pin.onPress ? { click: pin.onPress } : undefined}
          >
            <Tooltip direction="top" offset={[0, -8]}>
              <strong>{pin.title}</strong>
              {pin.description ? <div>{pin.description}</div> : null}
            </Tooltip>
          </Marker>
        ))}
      </MapContainer>
    </View>
  );
}
