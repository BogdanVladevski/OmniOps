// Sign-in calls. Each successful one stores the session, which switches the app to the right side for the role.
import { useMutation, useQuery } from '@tanstack/react-query';
import { api, unwrap } from '@/api/client';
import { queryClient } from '@/api/query-client';
import type { Session } from '@/api/types';
import { fromResponse, setSession } from '@/lib/session';

export async function startSession(call: Promise<Session>) {
  const session = await call;
  queryClient.clear(); // never show one company's cached data to another
  await setSession(fromResponse(session));
}

export const useSignInOptions = () =>
  useQuery({
    queryKey: ['sign-in-options'],
    queryFn: () => unwrap(api.GET('/api/v1/account/options')),
    staleTime: Infinity,
  });

/** A mutation that ends in a session. */
export const useSessionMutation = <T>(call: (input: T) => Promise<Session>) =>
  useMutation({ mutationFn: (input: T) => startSession(call(input)) });

export async function signOut() {
  const refreshToken = (await import('@/lib/session')).getSession()?.refreshToken;
  if (refreshToken) await api.POST('/api/v1/account/sign-out', { body: { refreshToken } }).catch(() => {});
  queryClient.clear();
  await setSession(null);
}
