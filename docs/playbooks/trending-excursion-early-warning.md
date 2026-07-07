---
id: SOP-CCM-6.1
title: Trending Toward Excursion — Early Warning
severity: warning
product_categories: [insulin, vaccine, biologic]
incident_types: [trend, early-warning, in-range-drift]
min_excursion_seconds: 0
---

# SOP-CCM-6.1 — Early-Warning Response for Trending Excursions

## Scope
Applies when cargo temperature is still within the labelled safe range but is moving
toward a limit faster than the shipment's normal variation — a predictive Warning that
precedes an actual breach. The goal is to intervene before an excursion is recorded.

## Immediate actions
1. Treat as an **actionable Warning**, not a breach. The batch is still compliant, but
   the trend indicates a developing failure.
2. Check the reefer/setpoint, door status, and ambient conditions for the vehicle.
   A rapid rise often precedes a compressor or door-seal failure.
3. Increase telemetry attention on this vehicle and pre-stage the excursion response
   so escalation to SOP-CCM-8.2 is immediate if the limit is crossed.

## Escalation
- Notify the driver/operator to verify reefer operation and close any open access.
- No QA quarantine is required yet; log the Warning so the trend is auditable.

## Disposition
- If temperature stabilises back inside a safe margin, close the Warning with a note.
- If the trend continues to a breach, escalate immediately to the excursion SOP for the
  product category and begin the quarantine-pending workflow.
