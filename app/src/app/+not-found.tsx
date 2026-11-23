// Any address this person can't see (including "/" before signing in, or the office home for a driver) leads to their
// own starting point instead of an error page.
import { Redirect } from 'expo-router';
import { useSession } from '@/lib/use-session';

export default function NotFound() {
  const session = useSession();
  if (!session) return <Redirect href="/sign-in" />;
  return <Redirect href={session.account.role === 'Driver' ? '/driver' : '/'} />;
}
