# Contributing

## Workflow

- Branch from `main`: `feat/…`, `fix/…`, `chore/…`, `docs/…`. One concern per branch.
- Commit messages follow [Conventional Commits](https://www.conventionalcommits.org/) (`feat(app): …`, `fix: …`).
- Open a pull request into `main`. CI must pass: backend build (warnings are errors), unit and integration tests, and the
  app's lint, type check, format check and tests.
- Every bug fix comes with a test that fails without the fix.

## Backend conventions

- A feature is one file (or a few) under `src/OmniOps.Application/Features/<Area>/`: the command or query, its
  validator, its handler and its DTOs. The endpoint mapping goes in `src/OmniOps.Api/Endpoints/`.
- Every command and query declares who may send it: `[RequiresPolicy(Policies.…)]`, or `[Anonymous]` with a reason.
  `AuthorizationConventionTests` enforces this.
- Company-owned entities get the tenant query filter. Reading across companies (`AcrossCompanies()`) is only for
  public links and background work, and must establish the company itself.
- Side effects (email, live notifications) happen after the database commit, never before.
- Rules that need no I/O belong in `OmniOps.Domain`, with unit tests.
- Never edit an existing migration. Add a new one:

  ```bash
  dotnet ef migrations add <Name> --project src/OmniOps.Infrastructure --startup-project src/OmniOps.Api \
    --output-dir Persistence/Migrations
  ```

- Changing the API changes the contract: building `OmniOps.Api` regenerates `docs/api/omniops-api.json`. Run
  `npm run api:types` in `app/` and commit both files.

## App conventions

- TypeScript, strict. Screens live in `app/src/app` (Expo Router); everything else is grouped by feature in
  `app/src/features`.
- Server data goes through TanStack Query, with keys from `src/api/query-client.ts`. Don't copy server data into local
  state.
- Driver-facing text lives in `src/i18n/*.ts`, and every language has the same keys (a test checks this). Office text is
  English for now.
- Design: neutral greys, colour only for status, hairline borders, components from `src/components/ui`. No hardcoded
  colours.

## Code style

`.editorconfig` covers C#. In `app/`, run `npm run format` (Prettier) and `npm run lint` (ESLint with the React Compiler
rules).
