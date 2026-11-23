import { useMutation, useQuery } from '@tanstack/react-query';
import { api, unwrap } from '@/api/client';
import { keys, queryClient } from '@/api/query-client';
import type { Customer } from '@/api/types';

export const useCustomers = () =>
  useQuery({ queryKey: keys.customers, queryFn: () => unwrap(api.GET('/api/v1/customers')) });

export const useCustomer = (customerId: string | null) =>
  useQuery({
    queryKey: keys.customer(customerId ?? ''),
    queryFn: () => unwrap(api.GET('/api/v1/customers/{customerId}', { params: { path: { customerId: customerId! } } })),
    enabled: !!customerId,
  });

/** After a change the server returns the customer; list and detail refresh from it. */
function changed(customer: Customer) {
  void queryClient.invalidateQueries({ queryKey: keys.customers });
  void queryClient.invalidateQueries({ queryKey: keys.customer(customer.id) });
}

export type CustomerForm = {
  name: string;
  email: string;
  phone: string;
  address: string;
  taxId: string;
  paymentTermsDays: string;
  emailUpdates: boolean;
};

export const useSaveCustomer = (customerId: string) =>
  useMutation({
    mutationFn: (form: CustomerForm) =>
      unwrap(
        api.PUT('/api/v1/customers/{customerId}', {
          params: { path: { customerId } },
          body: {
            name: form.name.trim(),
            email: form.email.trim() || null,
            phone: form.phone.trim() || null,
            address: form.address.trim() || null,
            taxId: form.taxId.trim() || null,
            paymentTermsDays: form.paymentTermsDays.trim() ? Number.parseInt(form.paymentTermsDays, 10) : null,
            emailUpdates: form.emailUpdates,
          },
        }),
      ),
    onSuccess: changed,
  });

export const usePortalLink = (customerId: string) => ({
  create: useMutation({
    mutationFn: () => unwrap(api.POST('/api/v1/customers/{customerId}/portal', { params: { path: { customerId } } })),
    onSuccess: changed,
  }),
  disable: useMutation({
    mutationFn: () => unwrap(api.DELETE('/api/v1/customers/{customerId}/portal', { params: { path: { customerId } } })),
    onSuccess: changed,
  }),
});
