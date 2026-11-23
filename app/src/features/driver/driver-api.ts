// What the driver's phone sends. JSON calls go through the typed client; photos are multipart.
import { Platform } from 'react-native';
import { API_URL, ApiError, api, authorizedFetch, problemMessage, unwrap } from '@/api/client';
import type { DocumentKind, Job, JobDocument, LocationPoint } from '@/api/types';

export interface Coords {
  latitude: number;
  longitude: number;
}

export interface UploadExtra {
  clientId?: string;
  capturedAt?: string;
  note?: string | null;
  temperatureCelsius?: number | null;
  coords?: Coords | null;
}

export const driverApi = {
  jobs: () => unwrap(api.GET('/api/v1/driver/jobs')) as Promise<Job[]>,

  start: (jobId: string, startedAt?: string) =>
    unwrap(
      api.POST('/api/v1/driver/jobs/{jobId}/start', {
        params: { path: { jobId } },
        body: { startedAt: startedAt ?? null },
      }),
    ),

  arrived: (
    jobId: string,
    body: { stop: 'pickup' | 'delivery'; arrivedAt?: string; latitude?: number | null; longitude?: number | null },
  ) =>
    unwrap(
      api.POST('/api/v1/driver/jobs/{jobId}/arrived', {
        params: { path: { jobId } },
        body: { arrivedAt: null, latitude: null, longitude: null, ...body },
      }),
    ),

  sendLocations: (jobId: string, points: LocationPoint[]) =>
    unwrap(api.POST('/api/v1/driver/jobs/{jobId}/locations', { params: { path: { jobId } }, body: { points } })),

  deliver: (
    jobId: string,
    body: {
      recipientName: string;
      note?: string | null;
      latitude?: number | null;
      longitude?: number | null;
      deliveredAt?: string;
    },
  ) =>
    unwrap(
      api.POST('/api/v1/driver/jobs/{jobId}/deliver', {
        params: { path: { jobId } },
        body: { note: null, latitude: null, longitude: null, deliveredAt: null, ...body },
      }),
    ),

  /** One photo for a job; `clientId` makes a resend safe, `capturedAt` is when it was taken. */
  async upload(jobId: string, kind: DocumentKind, photoUri: string, extra: UploadExtra = {}): Promise<JobDocument> {
    const form = new FormData();
    if (Platform.OS === 'web') {
      form.append('file', await (await fetch(photoUri)).blob(), `${kind}.jpg`);
    } else {
      // React Native's FormData takes a file reference object.
      form.append('file', { uri: photoUri, name: `${kind}.jpg`, type: 'image/jpeg' } as unknown as Blob);
    }
    form.append('kind', kind);
    const fields: Record<string, string | number | null | undefined> = {
      clientId: extra.clientId,
      capturedAt: extra.capturedAt,
      note: extra.note,
      temperatureCelsius: extra.temperatureCelsius,
      latitude: extra.coords?.latitude,
      longitude: extra.coords?.longitude,
    };
    Object.entries(fields).forEach(([name, value]) => value != null && form.append(name, String(value)));

    const response = await authorizedFetch(`${API_URL}/api/v1/driver/jobs/${jobId}/documents`, {
      method: 'POST',
      body: form,
    });
    const body = await response.json().catch(() => null);
    if (!response.ok) throw new ApiError(problemMessage(response.status, body), response.status);
    return body as JobDocument;
  },
};
