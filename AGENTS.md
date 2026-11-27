# AGENTS.md — Project Alexandria

Guidance for AI coding agents (and humans) working in this repository. Read this file first,
then the guide for the area you are touching:

| Read before... | File |
|---|---|
| any UI or component work (mandatory) | `skills/ui-skill.md` |
| backend work: serialization, dates, endpoints, tests, project map | `skills/backend-guide.md` |
| frontend work: data layer, polling, components, layouts | `skills/frontend-guide.md` |
| running Playwright, or judging any failing check | `skills/verification-baseline.md` |
| monitoring, incidents, jobs, service routing, domain quirks | `skills/monitoring-domain.md` |
| feature work (plan format) or work on another branch (worktrees) | `skills/agent-workflow.md` |
| staging work in chunks for review, with suggested commits | `skills/staged-review.md` |

Alexandria is a self-hosted file/media platform (versioning, previews, streaming, monitoring).
Pre-alpha, single maintainer, local-network scale.

- `Backend/` — .NET 10 API (`Alexandria.Api`, FastEndpoints), EF persistence/repositories,
  domain services (`Alexandria.Services.*`) and six RabbitMQ workers (`Alexandria.Workers.*`).
  Solution: `Backend/AlexandriaBackend.slnx`.
- `Frontend/` — Vue 3 + Nuxt UI v4 + Tailwind CSS v4 + Pinia Colada SPA (Vite, pnpm).
- `Builder/` — standalone .NET 10 setup TUI. Solution: `Builder/AlexandriaBuilder.slnx`.
- `plans/` holds active living docs; finished ones are archived in `completed/`, so look there
  before assuming a referenced plan was deleted. `audit/` reports describe past snapshots.
  All three are the maintainer's local working folders and are not in the repository, so a
  fresh clone has none of them.

---

## Commands & merge gates

Backend (from repo root):

```bash
dotnet build Backend/AlexandriaBackend.slnx          # must be 0 errors
dotnet test Backend/Alexandria.Tests.Unit            # full unit suite (must be green)
dotnet test Backend/Alexandria.Tests.Unit --filter "FullyQualifiedName~Monitoring"
dotnet test Backend/Alexandria.Tests.Integration     # needs a running Docker engine
```

Frontend (from `Frontend/`):

```bash
pnpm install            # first time only
pnpm type-check         # vue-tsc
pnpm lint               # oxlint
pnpm build-only         # production bundle (vite)
pnpm test:unit          # vitest
pnpm test:e2e           # playwright (read skills/verification-baseline.md first)
```

- Backend gates: **build 0 errors** and a **green unit suite**.
- Frontend gates: `type-check` clean on touched files, successful `build-only`, and `pnpm lint`
  with **zero new findings in touched files** (new files completely clean). Pre-existing
  findings on untouched lines stay untouched.
- Some checks fail on a known baseline. Before calling a failure new or old, compare test
  identities and causes against `skills/verification-baseline.md`. Never weaken a test or an
  authorization check to get green.
- Keep `skills/verification-baseline.md` current: when a check you ran shows a baseline entry
  is gone, changed, or missing, update that entry with the run's date in the same task.

---

## Rules for every task

- Never commit, push, stash-pop carelessly, or open PRs — **the maintainer handles all VCS
  mutations personally.** Read-only git commands are fine; ask before anything that changes
  git state. The one exception is staging and unstaging when asked to run
  `skills/staged-review.md`.
- Never switch branches in a checkout someone else is using. Work for another branch goes in a
  worktree made with `scripts/new-worktree.sh <branch>`, never a bare `git worktree add`.
- Do not delete or refactor code outside the declared task scope; flag surprises instead.
- Feature work follows the living-doc workflow: propose phases, record them in
  `plans/<epic>-plan.md`, implement **one phase at a time**, verify, then stop and wait for the
  go-ahead. Scope changes mid-phase go back through the user. Details: `skills/agent-workflow.md`.
  **Exception for Claude Opus models:** an approved plan may be implemented in one pass, with
  no phase split and no stop between steps. Verify once at the end; scope changes still go
  back through the user.
- **No nested ternaries** anywhere, any language. Use if/else chains, early returns, or record
  lookups.
- Frontend uses `const name = () => {}` arrow functions instead of function declarations.
- Comments explain *why*, not what, and stay minimal and self-contained: no em-dash
  separators, no references to plans, phases, or decision IDs (D1, #4, ...), and no
  line-by-line narration.

### Readability and blank lines (GPT models)

GPT-based agents must use blank lines to make logical sections easy to scan:

- Separate imports from declarations, setup/state from derived values, and individual
  functions, computed blocks, watchers, lifecycle hooks, and test cases.
- Inside functions, separate preparation, validation/early exits, processing, and the final
  result. Keep short groups of related statements together; do not add a blank line after
  every statement.
- In Vue templates, separate distinct sibling UI sections; keep tightly related labels,
  values, and small controls grouped. Leave a blank line between top-level `<template>`,
  `<script>`, and `<style>` blocks.
- Preserve this spacing when editing existing code.

---

## Rules that most often bite

Backend:

- **Enum-typed DTO fields serialize as numbers.** Never add `JsonStringEnumConverter`; the
  frontend mirrors them as numeric TS enums in `Frontend/src/enums/`. Some explicit contracts
  (health status, enrichment status names) are strings, so check the DTO.
- **Never call the one-argument `JsonSerializer.Serialize(x)`.** Pass explicit options, and
  serialize polymorphic DTOs through the abstract base type.
- Request DTOs take **`DateTimeOffset`/`DateTimeOffset?`** for dates and handlers convert with
  `.UtcDateTime`; `timestamp with time zone` rejects `Local`/`Unspecified` kinds.
- `CancellationToken ct` is always the last parameter.
- Business-rule conflicts use `AddError(...)` + `Send.ErrorsAsync(statusCode, ct)`, never a
  custom result shape.

Frontend:

- Icons are `UIcon` (never `@iconify/vue`'s `<Icon>`), Lucide first, written as full
  `i-lucide-*` literals in a `.vue`/`.ts` file under `src/` so the build-time bundle scan sees
  them; a name assembled at runtime is never bundled and renders blank. Other sets only where
  Lucide has no equivalent (brand marks, language logos, a few MDI glyphs).
- Loading states use `components/common/BlockSpinner.vue`, gated on `isPending`, never
  `isLoading` (true on every background refetch).
- A chart with `maintainAspectRatio: false` must sit in a fixed-height `relative` wrapper, or
  the page grows without bound.
- Backend enums are numeric enums from `src/enums/*`, never hand-written string unions.
