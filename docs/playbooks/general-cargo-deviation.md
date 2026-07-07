---
id: SOP-CCM-4.1
title: General Cargo Condition Deviation
severity: warning
product_categories: [general, insulin, vaccine, biologic]
incident_types: [deviation, general, unknown]
min_excursion_seconds: 0
---

# SOP-CCM-4.1 — General Cargo Condition Deviation

## Scope
Fallback procedure for any cargo condition deviation that does not match a more specific
product or incident playbook. Ensures every incident has a defined response even when the
product category or excursion type is unknown.

## Immediate actions
1. Halt delivery and secure the shipment until the deviation is characterised.
2. Record the current cargo temperature, GPS position, and timestamp.
3. Document the chain-of-custody and preserve the telemetry trace.

## Escalation
- Escalate to QA and, where product handling is regulated, to pharmacovigilance.
- Identify the product category and switch to the specific excursion SOP as soon as it
  is known.

## Disposition
- Do not release the shipment until a responsible reviewer has assessed the deviation and
  recorded a decision on the incident ticket.
