// Address → map position, so the customer page can show a pin and an arrival estimate. OpenStreetMap's Nominatim is
// fine for a dispatcher creating a few jobs a day (its policy allows ~1 request/second); a paid geocoder comes with
// launch. A job works without coordinates, so every failure is just "not found".
export interface Place {
  latitude: number;
  longitude: number;
  label: string;
}

export async function geocode(address: string): Promise<Place | null> {
  const q = address.trim();
  if (!q) return null;
  try {
    const response = await fetch(
      `https://nominatim.openstreetmap.org/search?format=json&limit=1&q=${encodeURIComponent(q)}`,
      {
        headers: { Accept: 'application/json' },
      },
    );
    if (!response.ok) return null;
    const [hit] = (await response.json()) as { lat: string; lon: string; display_name: string }[];
    return hit ? { latitude: Number(hit.lat), longitude: Number(hit.lon), label: hit.display_name } : null;
  } catch {
    return null;
  }
}
