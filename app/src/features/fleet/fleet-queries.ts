import { useQuery } from '@tanstack/react-query';
import { api, unwrap } from '@/api/client';
import { keys, queryClient } from '@/api/query-client';

export const useVehicles = () =>
  useQuery({ queryKey: keys.vehicles, queryFn: () => unwrap(api.GET('/api/v1/vehicles')) });
export const useDrivers = () => useQuery({ queryKey: keys.drivers, queryFn: () => unwrap(api.GET('/api/v1/drivers')) });

/** Trucks and drivers also appear in the new-job form and the setup checklist. */
export function fleetChanged() {
  void queryClient.invalidateQueries({ queryKey: keys.vehicles });
  void queryClient.invalidateQueries({ queryKey: keys.drivers });
  void queryClient.invalidateQueries({ queryKey: keys.jobOptions });
}
