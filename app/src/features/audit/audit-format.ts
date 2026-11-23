// The audit trail in plain words: "Invited someone to the team" instead of "InviteTeammate · AccountLink".
import type { AuditEntry } from '@/api/types';

const TEXT: Record<string, string> = {
  'Create:Job': 'Created a job',
  'Start:Job': 'Driver started a job',
  'Arrived:Job': 'Driver arrived',
  'Deliver:Job': 'Delivery confirmed',
  'Issue:Invoice': 'Issued an invoice',
  'Email:Invoice': 'Emailed an invoice',
  'MarkSent:Invoice': 'Marked an invoice sent',
  'MarkPaid:Invoice': 'Marked an invoice paid',
  'Update:BillingProfile': 'Changed invoice settings',
  'UpdateCompany:Organization': 'Renamed the company',
  'CreatePairingCode:Driver': "Created a code for a driver's phone",
  'InviteTeammate:AccountLink': 'Invited someone to the team',
  'RevokeInvitation:AccountLink': 'Withdrew an invitation',
  'AcceptInvitation:UserAccount': 'Joined the team',
  'ChangeTeammateRole:UserAccount': "Changed someone's role",
  'RemoveTeammate:UserAccount': 'Removed someone from the team',
  'CreatePasswordResetLink:UserAccount': 'Made a password reset link',
  'ResetPassword:UserAccount': 'Reset a password',
  'ChangePassword:UserAccount': 'Changed their password',
};

/** Unknown actions fall back to "Action · Thing" so nothing is hidden. */
export function describeAudit(entry: Pick<AuditEntry, 'action' | 'entityType' | 'details'>): {
  title: string;
  detail: string | null;
} {
  const title = TEXT[`${entry.action}:${entry.entityType}`] ?? `${entry.action} · ${entry.entityType}`;
  let detail = entry.details ?? null;
  if (entry.action === 'Arrived' && detail) detail = detail === 'pickup' ? 'At the pickup' : 'At the customer';
  if (entry.action === 'ChangeTeammateRole' && detail) detail = `Now ${detail.toLowerCase()}`;
  if (entry.action === 'InviteTeammate' && detail)
    detail = detail.replace(/ as (\w+)$/, (_m, role: string) => ` as ${role.toLowerCase()}`);
  return { title, detail };
}
