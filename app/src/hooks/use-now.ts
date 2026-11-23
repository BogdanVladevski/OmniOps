import { useEffect, useState } from 'react';

/** The current time, refreshed every `everyMs`, so time-based wording ("overdue", "waiting 1 h") stays true. */
export function useNow(everyMs = 30_000): number {
  const [now, setNow] = useState(() => Date.now());
  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), everyMs);
    return () => clearInterval(timer);
  }, [everyMs]);
  return now;
}
