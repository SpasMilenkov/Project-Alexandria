# Agent workflow

How feature work is planned and how parallel agents share the repository.

## Living-doc workflow

Feature work follows the **living-doc** workflow:

1. Propose phases; on approval create/update `plans/<epic>-plan.md` following the house format
   (title block with *"Living document"* banner → mandatory ui-skill step → numbered locked
   decisions `D1…` → phase status table → per-phase `- [ ]` checklists → dated update log).
   Existing examples: active `plans/integrations-audio-analysis-ui-plan.md`,
   archived `completed/monitoring-closeout-plan.md`.
   If a plan named in code, chat history, or an older doc is missing from `plans/`,
   look in `completed/` before assuming it was deleted — finished epics are archived there.
2. Implement **one phase at a time**, then stop: flip checkboxes, update the status table, add
   a dated update-log entry, report completion, and wait for the go-ahead.
   Claude Opus models may instead implement an approved plan in one pass (see `AGENTS.md`).
3. Verify each phase before reporting: backend `dotnet build` + targeted then full unit suite;
   frontend `pnpm type-check` (touched files clean) + `build-only`.
4. Scope changes mid-phase go back through the user, never get absorbed silently.

## Parallel work in worktrees

Several agents may work at once, so never switch branches in a checkout someone else is using.
When a task belongs on another branch, create its worktree with
`scripts/new-worktree.sh <branch>` (add `--dry-run` first to preview), never with a bare
`git worktree add`. The script places it at `../<branch>`, copies the gitignored config
(`appsettings*.json`, `.env` files, local stack config) and links the shared `plans/`,
`completed/` and Essentia models. `skills/` is tracked, so a branch that has it gets its own
copy, and the script links it only for older branches that predate it. Then work, build and
test only inside that worktree. Edits to the linked folders land in the main checkout and are
visible to every agent. Never delete through those links. Removing a worktree is the maintainer's call (`git worktree remove`),
and the no-commit/no-push rule still applies.
