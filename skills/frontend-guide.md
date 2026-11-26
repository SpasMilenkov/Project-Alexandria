# Frontend guide

Frontend conventions for Project Alexandria. `skills/ui-skill.md` governs everything visual;
`AGENTS.md` keeps the short list of rules that bite most often. This file holds the full detail.

## Source map

| Path | What it is |
|---|---|
| `Frontend/src/api` | One typed module per domain (`client.ts` holds the fetch client and session refresh, `errors.ts` holds `ApiError`) |
| `Frontend/src/enums` | Numeric TS enums mirroring backend enum ordinals |
| `Frontend/src/queries` · `mutations` | Pinia Colada query/mutation definitions |
| `Frontend/src/features/<feature>` | Domain logic per feature (home, wrapped, media, playlists, player, explorer, monitoring...). Logic only; components stay in `components/` |
| `Frontend/src/utils` | Generic, framework-free helpers only (dates, counts and plurals, sizes, colors, random, math, `files/` MIME + icons). Feature-specific code goes in `features/` |
| `Frontend/src/__tests__/<feature>` | Unit specs grouped like `features/`; `stores/` mirrors `src/stores` |
| `Frontend/src/components/dashboard/admin/monitoring` | Shared monitoring components (calendar, strip, feed) |

## Structure & data layer

- Domain modules: `src/api/<domain>.ts` (typed interfaces + one exported object of
  promise-returning functions using `api`), `src/queries/<domain>.ts`
  (`defineQueryOptions` + keys + `staleTime`; pass params via getter:
  `useQuery(() => someQuery({ ... }))`), `src/mutations/<domain>.ts`
  (`defineMutation(() => { const queryCache = useQueryCache(); return useMutation({...}) })`,
  invalidating related query keys by key-prefix in `onSettled`).
- Configure `VITE_API_BASE_URL` for the API prefix (normally `/api`); `client.ts` currently
  falls back to `http://localhost:5000` if unset. `api.get<T>()` and friends resolve to the
  body directly and retry once after `/auth/refresh` on 401. `params` values are sent as single
  values and `Date` becomes ISO; flat arrays repeat the key (`?ids=1&ids=2`) and objects throw.
  `responseType: "blob"` returns a successful body as a `Blob` for downloads.
- Failed requests reject with `ApiError` (`src/api/errors.ts`): read `err.status` and `err.data`
  for HTTP failures and `err.kind` (`http`, `network`, `timeout`) for the failure type. Use
  `isApiError` instead of duck typing. File uploads with progress use `XMLHttpRequest` in
  `fileApi.uploadToS3`, because `fetch` cannot report upload progress.
- `staleTime` does not schedule polling, and the installed Colada (1.4.8) has no
  `refetchInterval`; only the retry plugin is registered. Live displays poll with
  `useLiveRefresh(refetch, ms)` (pauses while the tab is hidden). Trailing windows take their
  "now" from `useRollingNow()` so query keys stay stable and still advance; pair rolling keys
  with `placeholderData: (prev) => prev` and gate spinners on `isPending` (or
  `data === undefined`), never `isLoading`, which is true on every background refetch.
- Numeric enums live in `src/enums/*` and re-export from the barrel. Never hand-write string
  unions for backend enums.
- Transient auth 401/403s during token refresh are suppressed from UI error states via the
  shared `isAuthError(err: unknown)` helper pattern — copy it, don't reinvent casts.

## Components & UI

- `skills/ui-skill.md` governs everything visual (semi-transparent surfaces, solid text tokens,
  primary color discipline, spacing grid, empty states, mobile overrides). Non-negotiable.
- Additional hard rules learned the hard way:
  - **Icons**: `UIcon` with Lucide (`i-lucide-*`) by default. Nuxt UI's `icon.clientBundle`
    scan in `vite.config.ts` bundles every icon name it finds in `src/**/*.{vue,ts}`, and the CSP
    blocks the Iconify API, so a name must appear as a full literal somewhere in source; one
    built at runtime renders blank. In dev the scan runs at startup and again only when a
    watched file is edited (Nuxt UI uses Vite's legacy `handleHotUpdate`, which Vite 8 skips for
    created files), so a new icon in a file created while the dev server runs renders blank
    until that file is saved again or the server restarts. Production builds are unaffected. MDI stays only where Lucide has no equivalent (brand and
    language logos, `file-pdf-box`, `file-swap-outline`, `music-note-off`, `download-off-outline`);
    the favicon keeps Heroicons solid glyphs (`features/site-icon/places.ts`). Lucide has no
    filled variants: add the `icon-solid` utility for active or transport states (play, pause,
    skip, a set favourite); it fills single-shape glyphs only, so never use it on icons with an
    inner mark like `circle-check`.
  - **Charts**: with `maintainAspectRatio: false` the canvas MUST sit inside a fixed-height,
    `position: relative` wrapper (e.g. `relative h-56`) or it feedback-loops into unbounded page
    growth. Skeletons should match that height.
  - **Loading states**: per-component centered spinner gate using
    `components/common/BlockSpinner.vue` (`<BlockSpinner :size="…" />`), not ad-hoc
    `i-mdi-loading animate-spin` icons. The maintainer prefers this over skeleton blocks
    (no render/reflow flicker). Gate it on `isPending`, never `isLoading`.
  - **No nested ternary operators anywhere**, backend included. Use if/else chains, early
    returns, or record lookups. Also: `const name = () => {}` arrow style; no em-dash comment
    separators.
  - Interactive rows: prefer `role="button"` divs with keyboard handlers when nesting action
    buttons inside clickable rows (nested `<button>` elements are illegal DOM).
- Router pages declare `meta.layout` (`default` | `dashboard` | `onboarding`);
  `App.vue` resolves the layout through a computed so special routes can upgrade conditionally
  (public status uses dashboard chrome when authenticated).
