---
id: SOP-CCM-7.3
title: Temperature Excursion Response — Insulin and Injectable Biologics
severity: critical
product_categories: [insulin, biologic]
incident_types: [excursion, warm-excursion]
min_excursion_seconds: 0
---

# SOP-CCM-7.3 — Temperature Excursion Response for Insulin and Injectable Biologics

## Scope
Applies to any in-transit shipment of insulin analogues (glargine, aspart, lispro)
and comparable injectable biologics where the recorded cargo temperature leaves the
labelled storage range at any point during transport.

## Immediate actions (first 5 minutes)
1. Treat the shipment as **quarantine-pending**. Do not deliver, transfer, or
   commingle with compliant stock until QA releases it.
2. Record the excursion start time, peak temperature, and duration from the
   telemetry trace. Insulin stability is duration-sensitive: brief single-degree
   excursions differ materially from sustained breaches.
3. Verify the reading against a second sensor or manual probe where available to
   rule out sensor fault (see SOP-CCM-9.1) before escalating as a true excursion.

## Escalation
- Notify the QA on-call and the receiving pharmacy that a temperature deviation
  has occurred and the batch is under assessment.
- For insulin, cumulative time above 30 °C or any freezing event triggers a
  mandatory stability review — do not rely on visual inspection alone.

## Disposition
- QA performs a stability assessment against the manufacturer's excursion table
  using total time-out-of-range, not just peak temperature.
- Document the chain-of-custody, telemetry export, and QA decision on the incident
  ticket before the batch is released or destroyed.
