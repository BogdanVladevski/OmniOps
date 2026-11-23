// The API, typed from its OpenAPI contract. Signs every request, renews the session once on 401, and turns
// problem details into errors with the message the server wrote for people.
import createClient from 'openapi-fetch';
import { fromResponse, getSession, setSession } from '@/lib/session';
import type { paths } from './schema';

export const API_URL = (process.env.EXPO_PUBLIC_API_URL ?? 'http://localhost:5031').replace(/\/$/, '');

/** An error the server answered with (as opposed to no connection). */
export class ApiError extends Error {
  constructor(
    message: string,
    readonly status: number,
  ) {
    super(message);
    this.name = 'ApiError';
  }
}

let refreshing: Promise<boolean> | null = null;

/** Renews the session once (single flight). Signs out if it can no longer be renewed. */
export function refreshSession(): Promise<boolean> {
  const session = getSession();
  if (!session?.refreshToken) return Promise.resolve(false);
  refreshing ??= (async () => {
    try {
      const response = await fetch(`${API_URL}/api/v1/account/refresh`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken: session.refreshToken }),
      });
      if (response.status === 401) {
        await setSession(null);
        return false;
      }
      if (!response.ok) return false; // offline or server trouble: keep the session and try later
      await setSession(fromResponse(await response.json()));
      return true;
    } catch {
      return false;
    } finally {
      refreshing = null;
    }
  })();
  return refreshing;
}

export const authHeader = (): Record<string, string> => {
  const token = getSession()?.accessToken;
  return token ? { Authorization: `Bearer ${token}` } : {};
};

/** fetch with the sign-in attached; access tokens are short-lived, so a 401 renews once and retries. */
export async function authorizedFetch(input: Request | string, init?: RequestInit, retried = false): Promise<Response> {
  const request = new Request(input, init);
  const sent = request.clone();
  Object.entries(authHeader()).forEach(([k, v]) => sent.headers.set(k, v));
  const response = await fetch(sent);
  if (response.status === 401 && !retried && getSession()?.refreshToken && (await refreshSession())) {
    return authorizedFetch(request, undefined, true);
  }
  return response;
}

export const api = createClient<paths>({ baseUrl: API_URL, fetch: (request) => authorizedFetch(request) });

/** The human message from a problem-details body, or a generic one. */
export function problemMessage(status: number, body: unknown): string {
  if (body && typeof body === 'object' && 'title' in body && typeof body.title === 'string') return body.title;
  if (status === 0) return 'No connection.';
  if (status >= 500) return 'Something went wrong on our side. Please try again.';
  return `Request failed (${status}).`;
}

/** Awaits an openapi-fetch call: returns the data or throws an ApiError with the server's message. */
export async function unwrap<T>(call: Promise<{ data?: T; error?: unknown; response: Response }>): Promise<T> {
  const { data, error, response } = await call;
  if (!response.ok) throw new ApiError(problemMessage(response.status, error), response.status);
  return data as T;
}

/** A signed-in file (e.g. a job photo) as a local URL; browsers can't send the token with <img>. */
export async function fileObjectUrl(path: string): Promise<string> {
  const response = await authorizedFetch(`${API_URL}${path}`);
  if (!response.ok) throw new ApiError('Could not load the file.', response.status);
  return URL.createObjectURL(await response.blob());
}
