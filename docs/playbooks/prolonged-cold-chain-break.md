---
id: SOP-CCM-8.2
title: Prolonged Cold-Chain Break — Sustained Excursion
severity: critical
product_categories: [insulin, vaccine, biologic]
incident_types: [prolonged-excursion, sustained-breach]
min_excursion_seconds: 60
---

# SOP-CCM-8.2 — Prolonged Cold-Chain Break Response

## Scope
Applies when cargo temperature has been continuously outside the labelled safe range
for 60 seconds or longer, or where excursion duration is still growing. A sustained
break is materially more severe than a transient spike and is treated as a Critical
incident by default.

## Immediate actions
1. Declare a **Critical cold-chain incident**. Halt the delivery and secure the
   shipment bay; do not continue the route on the assumption conditions will recover.
2. Export the full telemetry window covering the entire excursion, not just the peak.
   Total time-out-of-range is the primary disposition input.
3. Arrange emergency cold storage at the nearest qualified facility for arrival.

## Escalation
- Page QA and pharmacovigilance immediately; a sustained breach frequently results in
  full or partial batch loss and must be tracked as a reportable deviation.
- Notify the shipment owner of the estimated value at risk so a replacement can be
  initiated in parallel with the disposition review.

## Disposition
- The batch is presumed compromised until QA proves otherwise against the cumulative
  excursion profile. Prefer conservative discard where stability data is unavailable.
- Capture root cause (reefer failure, door-open, sensor placement) for corrective action.
