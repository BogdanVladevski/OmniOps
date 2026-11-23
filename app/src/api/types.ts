// Named shapes from the API contract (generated: npm run api:types).
import type { components } from './schema';

type S = components['schemas'];

export type Account = S['AccountDto'];
export type Session = S['SessionDto'];
export type Job = S['JobDto'];
export type JobDetail = S['JobDetailDto'];
export type JobDocument = S['JobDocumentDto'];
export type Wait = S['WaitDto'];
export type JobOptions = S['JobOptionsDto'];
export type Vehicle = S['VehicleDto'];
export type Driver = S['DriverDto'];
export type Team = S['TeamDto'];
export type TeamMember = S['TeamMemberDto'];
export type TeamInvitation = S['TeamInvitationDto'];
export type BillingProfile = S['BillingProfileDto'];
export type InvoiceOverview = S['InvoiceOverviewDto'];
export type InvoiceSummary = S['InvoiceSummaryDto'];
export type InvoiceDetail = S['InvoiceDetailDto'];
export type UninvoicedJob = S['UninvoicedJobDto'];
export type SuggestedLine = S['SuggestedLineDto'];
export type AuditEntry = S['AuditEntryDto'];
export type LocationPoint = S['LocationPoint'];

export type JobStatus = 'Planned' | 'InProgress' | 'Delivered' | 'Cancelled';
export type Role = 'Owner' | 'Dispatcher' | 'Driver';
export type DocumentKind = 'DeliveryPhoto' | 'PickupDocument' | 'TemperaturePhoto' | 'Signature' | 'Exception';
