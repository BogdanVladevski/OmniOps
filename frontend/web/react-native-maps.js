// Web implementation of the react-native-maps API subset OmniOps uses, backed by Leaflet.
// Selected by metro.config.js for web builds only; iOS/Android use the real react-native-maps.
//
// Tiles: OpenStreetMap standard tiles (no key) and Esri World Imagery for satellite. Both are fine for
// development and demos; production traffic needs a tile plan (OSM's public servers forbid heavy use).
import 'leaflet/dist/leaflet.css';
import React, { forwardRef, useImperativeHandle, useState } from 'react';
import { View } from 'react-native';
import L from 'leaflet';
import {
  Circle as LCircle,
  MapContainer,
  Marker as LMarker,
  Polygon as LPolygon,
  Polyline as LPolyline,
  TileLayer,
  Tooltip,
} from 'react-leaflet';

const ATTR_OSM = '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>';
const TILES = {
  street: { url: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png', attribution: ATTR_OSM },
  satellite: {
    url: 'https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}',
    attribution: 'Imagery &copy; Esri',
  },
  labels: {
    url: 'https://server.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}',
    attribution: 'Labels &copy; Esri',
  },
};

// Dark mode: OSM has no free dark style, so dim and invert the street tiles with CSS.
if (typeof document !== 'undefined' && !document.getElementById('omniops-map-css')) {
  const el = document.createElement('style');
  el.id = 'omniops-map-css';
  el.textContent = '.omniops-dark-tiles{filter:invert(1) hue-rotate(180deg) brightness(.9) contrast(.9) saturate(.6)}'
    + '.omniops-light-tiles{filter:saturate(.55) contrast(.95)}';
  document.head.appendChild(el);
}

const toLatLng = (c) => [c.latitude, c.longitude];
const zoomFor = (region) =>
  region?.longitudeDelta ? Math.max(2, Math.min(18, Math.round(Math.log2(360 / region.longitudeDelta)))) : 12;

const MapView = forwardRef(function MapView(
  { style, initialRegion, mapType = 'standard', userInterfaceStyle, children },
  ref,
) {
  const [map, setMap] = useState(null);

  useImperativeHandle(ref, () => ({
    animateToRegion(region, duration = 800) {
      map?.flyTo([region.latitude, region.longitude], zoomFor(region), { duration: duration / 1000 });
    },
    animateCamera({ center, zoom } = {}) {
      if (center) map?.flyTo([center.latitude, center.longitude], zoom ?? map.getZoom());
    },
    fitToCoordinates(coords = [], { edgePadding } = {}) {
      if (!map || coords.length === 0) return;
      // Per-side padding, so a floating panel on one side does not shrink the map on every side.
      const p = { top: 40, left: 40, right: 40, bottom: 40, ...(edgePadding ?? {}) };
      map.fitBounds(L.latLngBounds(coords.map(toLatLng)), {
        paddingTopLeft: [p.left, p.top],
        paddingBottomRight: [p.right, p.bottom],
        maxZoom: 13,
      });
    },
  }), [map]);

  const center = initialRegion ? [initialRegion.latitude, initialRegion.longitude] : [41.9965, 21.4314];
  const imagery = mapType === 'satellite' || mapType === 'hybrid';
  const base = imagery ? TILES.satellite : TILES.street;
  const tileClass = imagery ? undefined : userInterfaceStyle === 'dark' ? 'omniops-dark-tiles' : 'omniops-light-tiles';

  return (
    <View style={style}>
      <MapContainer
        ref={setMap}
        center={center}
        zoom={zoomFor(initialRegion)}
        zoomControl={false}
        attributionControl
        style={{ height: '100%', width: '100%', background: userInterfaceStyle === 'dark' ? '#1b1d21' : '#eef0f2' }}
      >
        <TileLayer key={`${base.url}|${tileClass}`} url={base.url} attribution={base.attribution} className={tileClass} />
        {mapType === 'hybrid' ? <TileLayer url={TILES.labels.url} attribution={TILES.labels.attribution} /> : null}
        {children}
      </MapContainer>
    </View>
  );
});

function pinIcon(color = '#1f6feb', selected = false) {
  const size = selected ? 18 : 14;
  return L.divIcon({
    className: '',
    iconSize: [size, size],
    iconAnchor: [size / 2, size / 2],
    html: `<div style="width:${size}px;height:${size}px;border-radius:50%;background:${color};border:2.5px solid #fff;box-shadow:0 1px 4px rgba(0,0,0,.35)"></div>`,
  });
}

export function Marker({ coordinate, title, description, pinColor, onPress, opacity = 1 }) {
  if (!coordinate) return null;
  return (
    <LMarker
      position={toLatLng(coordinate)}
      icon={pinIcon(pinColor, opacity >= 1)}
      opacity={opacity}
      eventHandlers={onPress ? { click: () => onPress() } : undefined}
    >
      {title ? (
        <Tooltip direction="top" offset={[0, -8]}>
          <strong>{title}</strong>
          {description ? <div>{description}</div> : null}
        </Tooltip>
      ) : null}
    </LMarker>
  );
}

export function Circle({ center, radius, strokeColor, fillColor, strokeWidth = 1.5 }) {
  if (!center) return null;
  return (
    <LCircle
      center={toLatLng(center)}
      radius={radius}
      pathOptions={{ color: strokeColor, fillColor, fillOpacity: 1, weight: strokeWidth }}
    />
  );
}

export function Polygon({ coordinates = [], strokeColor, fillColor, strokeWidth = 1.5 }) {
  return (
    <LPolygon
      positions={coordinates.map(toLatLng)}
      pathOptions={{ color: strokeColor, fillColor, fillOpacity: 1, weight: strokeWidth }}
    />
  );
}

export function Polyline({ coordinates = [], strokeColor = '#1f6feb', strokeWidth = 3 }) {
  return <LPolyline positions={coordinates.map(toLatLng)} pathOptions={{ color: strokeColor, weight: strokeWidth }} />;
}

export const Callout = () => null;
export const PROVIDER_DEFAULT = null;
export const PROVIDER_GOOGLE = 'google';

export default MapView;
