# Verification baseline

Read before running Playwright or deciding whether a failing check is new. The merge gates
themselves are listed in `AGENTS.md`.

## Playwright

Playwright notes: without `CI` the config reuses whatever already serves port 5173 (usually
the maintainer's dev server, i.e. the working tree, not what you meant to test). With `CI=1`
it serves `dist/` through `vite preview` on 4173, so run `pnpm build-only` first, but `CI=1`
also forces one worker and two retries. For a full run use
`CI=1 npx playwright test --project=chromium --workers=4 --retries=0` (about 5 minutes
instead of 15). Animation and layout-measuring specs get flaky above four workers.

## Running the checks

`pnpm <script>` can start a dependency status check that tries to install packages. To avoid
that, call the binaries directly from `Frontend/`: `./node_modules/.bin/vue-tsc --noEmit -p
tsconfig.app.json`, `./node_modules/.bin/oxlint`, `./node_modules/.bin/vitest run`.

## Known-failing baseline (do not fix unrelated failures)

The maintainer accepts the following existing failures as a baseline for unrelated work.
This is not an assertion that the behavior is correct or permission to weaken tests or
authorization checks. Compare failing test identities and failure causes, not just counts;
verify **zero new regressions**. Fix a baseline failure when it is in the authorized task
scope.

**Keep this record current.** Whenever a run of any check below disagrees with this file
(a listed failure is gone, a new pre-existing failure shows up, the counts moved), update
the entry in the same task, with the date of the run. A stale baseline makes every later
agent misjudge what is new. Record only what you actually ran; leave an entry's old date
in place when you did not re-run that check.

- `pnpm type-check`: clean as of 2026-11-26. Any error is a regression.
- `pnpm lint` (oxlint), observed 2026-11-26: 19 errors and 258 warnings repo-wide (unused
  imports/params, shadowing, statement counts, sort rules, `preserve-caught-error`). Capture
  the actual pre-change output when using this gate. The rule is zero new findings in
  touched files, not zero total findings.
- `pnpm test:unit`, observed 2026-11-26: 1729 tests in 178 tracked files, one known failure:
  - `src/__tests__/home/landing.spec.ts` "uses Home for an unusable return URL:
    \"/missing-page\"". The `not-found` catch-all route (added 2026-11-14) matches every
    path, so `getHomeLandingDestination` in `features/home/landing.ts` no longer treats an
    unknown path as unusable and returns it instead of `/home`. A real bug, not a stale test.
  - The maintainer's checkout also holds git-ignored scratch specs under
    `Frontend/coverage/roundtrip/` that vitest picks up (29 tests, 2 failing in
    `note-link-roundtrip.spec.ts`). They are not part of the suite; leave them out of counts
    and verdicts.
- `vue-tsc -p tsconfig.vitest.json` is not a gate and reports 83 errors as of 2026-11-26,
  mostly in app code; do not use it as evidence either way.
- Backend `dotnet test Backend/Alexandria.Tests.Unit`: fully green as of 2026-11-26 (2974
  tests). Any failure is a regression.
- Builder `Builder.Tests`: 1034 passed, 15 skipped as of 2026-11-26. The skips are expected.
- `pnpm test:e2e` (Chromium), observed 2026-11-02 (not re-run since): 265 of 268 pass. The
  three failures are real or unresolved, not stale tests, so do not weaken them:
  - `home/widgets/library-reorder` touch reorder on a phone: inside the mobile editing drawer the
    widget inspector `fieldset` reports overflow but ignores `scrollTop`, so the drag's edge
    auto-scroll cannot reveal the lower shortcuts (the drawer's outer container scrolls).
  - `home/editor/resize` touch corner resizing on a phone: touching a resize handle dismisses the
    non-modal editing drawer, which drops the canvas `.has-sheet` 72dvh bottom padding; when
    the canvas was scrolled near the end its `scrollTop` is clamped mid-gesture and the
    resize delta loses about one row.
  - `home/clock/text-position` clock style 3: the clock face overflows its widget area by
    about 6px for one regular shape. Fails consistently, including before the 2026-11-01
    dashboard frame redesign.
- After large renames/removals, stale strings can persist in `bin/`/`obj/` DLLs — a clean
  rebuild flushes them; don't grep binaries as evidence of source state.
