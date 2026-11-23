export interface Coord {
  latitude: number;
  longitude: number;
}

export interface MapPin extends Coord {
  id: string;
  color: string;
  title: string;
  description?: string;
  /** Larger and fully opaque; others are drawn quieter. */
  emphasis?: boolean;
  onPress?: () => void;
}

export interface MapLine {
  id: string;
  points: Coord[];
  color: string;
  dashed?: boolean;
}

export interface MapFocus {
  /** Changing the key re-frames the map. */
  key: string;
  coords: Coord[];
  padding: { top: number; left: number; right: number; bottom: number };
}

export interface DeliveryMapProps {
  pins: MapPin[];
  lines?: MapLine[];
  satellite?: boolean;
  dark: boolean;
  focus?: MapFocus | null;
}

/** North Macedonia, where the first customers are. */
export const INITIAL_REGION = { latitude: 41.75, longitude: 21.45, latitudeDelta: 1.6, longitudeDelta: 1.6 };
