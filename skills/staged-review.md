# Staged review

How uncommitted work reaches the maintainer for review: one chunk at a time, staged in the
index, each with a suggested commit message. The maintainer reviews the staged diff and makes
every commit. Agents only stage and unstage, and only when asked to run this workflow.

## Rules

- **Staging is the only git mutation allowed here.** Stage with `git add -- <paths>`,
  `git apply --cached` or `git update-index --cacheinfo`, and unstage with
  `git restore --staged -- <paths>`. Never commit, push,
  stash, `reset --hard`, `checkout --` or plain `git restore`: those touch the working tree or
  history.
- **Name every path.** Never `git add -A`, `git add .` or a bare directory. Interactive
  `git add -p` isn't available to agents; partial files go through a patch (below).
- **Never unstage what you didn't stage** without asking. Staged work already in the index at
  the start may be the maintainer's own selection.
- **One chunk in the index at a time.** The next chunk is staged only after the maintainer
  says the current one is committed, or asks for the next one on top.

## 1. Survey

Run `git status --short`, `git diff --stat` and `git diff --cached --stat`, then report:

- what is already staged, and which of those files also carry unstaged edits (`MM`, `AM`);
- unstaged and untracked work, grouped by area;
- changes that look unrelated to the series (another area, a stray config, `backups/`, which
  stays out on purpose) and stay out of every chunk unless the maintainer says otherwise.

If the index isn't empty, ask before going further: keep it as the first chunk, or unstage
it and plan from scratch.

## 2. Plan the chunks

Propose an ordered list. Each chunk is one reviewable unit that becomes one commit:

- one purpose, with a conventional subject: `type(scope): summary` (feat, fix, perf, refactor,
  test, docs, chore). Follow the format even when recent subjects drifted from it;
- tests travel with the code they cover, not in a chunk of their own;
- dependencies come first: backend model → migration → repository → service → endpoint, then
  frontend api → queries/mutations → features → composables → components → views;
- a pure move or refactor stays apart from behaviour changes, so its diff reads as a move;
- a file whose changes belong to two chunks is split by hunk.

For each chunk list its files (marking any split by hunk), its size, and the suggested subject.
Wait for approval of the plan before staging anything.

## 3. Stage one chunk

- Whole files: `git add -- <path> <path> …`.
- Part of a file, when the chunk's hunks stand apart: write them as a patch in the scratchpad
  (from `git diff -- <path>`, trimmed to the wanted hunks), check it with
  `git apply --cached --check <patch>`, then apply it with `git apply --cached <patch>`.
- Part of a file, when chunks share a hunk (one import block serving two features): write the
  file as this chunk should commit it to the scratchpad, starting from `git show HEAD:<path>`
  and adding only this chunk's changes. Stage that exact content with
  `git update-index --cacheinfo 100644,$(git hash-object -w <scratch file>),<path>`. Never
  write anything that isn't in the working tree already: the prepared version only leaves
  later chunks' lines out.
- The working tree is never touched.
- Then confirm with `git diff --cached --stat` that the index holds exactly the planned paths,
  with `git diff --cached -- <split file>` that each split file holds only its part, and with
  `git diff -- <split file>` that the rest is still waiting in the working tree.

### Standalone check

The working tree holds every later chunk, so its checks can't show that a chunk builds alone.
For a chunk with split files, offer to check it: export the index with
`git checkout-index -a --prefix=<scratchpad>/index-<chunk>/`, link the real `node_modules` into
that copy's `Frontend/`, and run the usual gates there (`dotnet build` on the copy's
`Backend/AlexandriaBackend.slnx`, `pnpm type-check` in its `Frontend/`). It's heavy, so run it
only after a yes and on its own.

## 4. Hand over

Report, then stop and wait:

- the chunk's name and its staged files with line counts;
- the suggested commit message: the subject line, then a short body on *why*, wrapped at 72;
- anything worth a closer look: a risky hunk, a behaviour change hidden in a refactor, a file
  split by hunk;
- what was verified. Checks run against the working tree, which holds later chunks too, so a
  green build doesn't prove the chunk builds alone. Say that rather than claim it, unless the
  standalone check ran.

## 5. After review

- **Committed** (or "next"): survey the index again, then stage the next chunk.
- **Changes asked for**: fix them in the working tree, restage the affected files or hunks,
  and report the chunk again, saying what changed.
- **Drop a file or hunk**: `git restore --staged -- <path>`, or apply the reverse patch with
  `git apply --cached --reverse <patch>`. The edit itself stays in the working tree.
- **Plan change** (merge, split or reorder chunks): restate the remaining plan and wait for a yes.
