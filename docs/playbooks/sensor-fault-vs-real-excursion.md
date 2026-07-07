---
id: SOP-CCM-9.1
title: Distinguishing Sensor Fault from a Real Excursion
severity: warning
product_categories: [insulin, vaccine, biologic]
incident_types: [sensor-fault, diagnostic, data-quality]
min_excursion_seconds: 0
---

# SOP-CCM-9.1 — Sensor Fault versus Real Excursion

## Scope
Applies before committing a batch to quarantine when a single reading or an implausible
telemetry pattern suggests the alert may be a sensor artefact rather than a genuine
cargo-temperature excursion.

## Diagnostic checks
1. Look for physically implausible signatures: instantaneous jumps of many degrees
   between consecutive readings, flat-lined values, or readings outside the sensor's
   rated range. These indicate a fault, not real cargo behaviour.
2. Cross-check against a second sensor, the reefer's own probe, or a manual reading.
   A true excursion shows a coherent trend; a fault typically disagrees with peers.
3. Confirm telemetry continuity — a data gap followed by a spike can be a
   reconnection artefact rather than a real event.

## Actions
- If a fault is confirmed, tag the reading as suspect, keep the batch compliant, and
  raise a maintenance ticket for the sensor. Do not discard product on faulty data.
- If the signature is consistent with a real excursion, proceed to the appropriate
  product excursion SOP and begin quarantine assessment.

## Disposition
- Record the diagnostic reasoning on the incident ticket regardless of outcome, so the
  decision to treat data as real or faulty is auditable.
