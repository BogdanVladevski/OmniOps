# OmniOps: Developer Workspace Setup Playbook

This playbook outlines the exact technical sequence required to spin up the entire OmniOps distributed architecture on a new development machine (desktop or laptop).

---

## Software Dependencies Checklist

Ensure the following runtimes and tools are installed on your workstation before spinning up the repository:

1. **Docker Desktop** (Required for containerized infrastructure stack)
2. **.NET 9.0 SDK** (or newer)
3. **Java Development Kit (JDK 17 or 21)**
4. **Node.js (LTS)**
5. **VS Code Extensions:**
   * C# Dev Kit
   * Extension Pack for Java
   * Docker

---

## 1. Hydrating Environment Files (`.env`)

Because environmental files are explicitly blocked via `.gitignore` to protect credentials, you must manually initialize them when moving to a new development computer.

### Root Environment File
Create a `.env` file at the absolute root of the repository (`OmniOps/.env`):

```text
DB_CONNECTION_STRING="Host=localhost;Database=OmniOps;Username=YOUR_USERNAME;Password=YOUR_PASSWORD"