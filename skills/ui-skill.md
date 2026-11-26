# UI skill: component styling and theming

This project uses **Nuxt UI v4**, **Tailwind CSS v4**, and **Vue 3** with a custom theming layer. When writing or editing components, follow these rules exactly.

## Settings components
Settings fields write to the Pinia store immediately via computed setters.
Persistence to the server is handled by a `useDebounceFn` watcher (600ms) 
calling the appropriate mutation, never with an explicit save button.
The store is always hydrated from the server response, not the outbound payload.



## Background image
`store.backgroundImage` is a runtime-only blob URL or null, created after an
authorized `cache: "no-store"` image fetch. Never treat it as base64 or a stable
asset URL. Never persist it; it is revoked on replacement, deletion and session
teardown. Use `store.backgroundImageKey` to check if the user has an image
configured. `store.hasBackgroundImage` reflects key presence, not the runtime URL.
There is no service-worker intercept URL or image cache.

## Surface model
The background can be a user-uploaded image composited with a color overlay.
Page-plane surfaces (cards, bars, sheets) are semi-transparent so the user's
chosen environment shows through. Overlay surfaces (modals, drawers) are
intentionally solid: translucency over a dimmed scrim is perceptually invisible
and only costs legibility.

## Theming system

The app has a `useTheme()` composable (`composables/useTheme.ts`) that owns all DOM side effects. It uses VueUse's `useDark()` as a reactive source inside a `watchEffect` to apply:

- **Accent color** via `--ui-primary` CSS variable (RGB values, no `rgb()` wrapper in the variable itself)
- **Background color** via `document.body.style.backgroundColor` and `--ui-bg`
- **Background image** via a `linear-gradient` overlay composited over a user-uploaded image
- **Glass effects** via `--frost-blur` (every `.frosted-glass` blur), `--glass-opacity`
  (every `.glass-surface` tint), plus `frost-off` / `frost-off-mobile` /
  `transparency-off` classes on `<html>` that force fully solid surfaces.
  The store holds the preferences; only `useTheme()` writes them to the DOM.

**Never read `document.documentElement.classList.contains("dark")` directly.** Always get dark mode state from `useDark()` (VueUse). If a component needs `isDark`, import `useTheme` and destructure it: `const { isDark } = useTheme()`.

The `useSettingsStore` (Pinia) is **pure state only**, no DOM writes, no watchers, no theme application logic lives there.

## Color usage

- Use `color="primary"` for all accent-colored Nuxt UI components (`UButton`, `UBadge`, `UAlert`, etc.). This automatically tracks the user's chosen accent color via `--ui-primary`.
- Never hardcode accent colors like `color="emerald"` or `color="orange"` on interactive elements.
- For neutral chrome (headers, sidebars, cards) use `color="neutral"`.
- For destructive actions use `color="error"`.
- Decorative icon accents can use `class="text-primary"`.
- **The primary (accent) color is for accent/active states only**, never use it as a fill color for non-interactive elements or decorative backgrounds. The accent is user-chosen (default emerald), so never design around one specific hue.

## Text color and opacity (accessibility-critical)

**Opacity modifiers (`/NN`) are for backgrounds, borders, and dividers only. Never apply an opacity suffix to a text color class.** This is a hard rule, not a style preference.

- Do NOT write `text-gray-500/50`, `text-white/60`, `text-black/40`, or any `text-{color}/{opacity}` class, ever.
- "Muted" or "secondary" text always means a distinct solid gray token, never a lighter alpha of the base color. Use:
  - Primary text: `text-gray-900 dark:text-gray-100`
  - Secondary/muted text (labels, descriptions, subheadings): `text-gray-600 dark:text-gray-400`
  - Tertiary text (timestamps, metadata, icon-only hints): `text-gray-500 dark:text-gray-500`
- These solid tokens must hold at minimum WCAG AA contrast (4.5:1 for body text, 3:1 for large text) against both the default surface and the semi-transparent card surfaces defined below. Opacity modifiers break this guarantee because they composite unpredictably with whatever background (including a user's custom image) sits behind them, opacity on backgrounds/borders is a deliberate compositing effect; opacity on text is just an accessibility bug.
- Every place elsewhere in this doc that says "muted" for text (empty states, list rows, log out link, timestamps) means the solid tokens above, not an opacity utility.

## Background & surface colors

The user may have any background preset (system, cream which is the default, parchment, newsprint, linen, midnight, charcoal, ink, cool) or a custom image. Components must not assume a white or dark background. Follow these rules:

- **Card/panel surfaces** use the shared glass classes so the background shows through tastefully: `frosted-glass glass-surface` (cards, bars, sheets) or `frosted-glass glass-surface-strong` (popover-level sheets over busy content). Never hand-write `bg-white/xx` or similar translucency on a frosted surface; Tailwind opacity utilities are precompiled and cannot follow the user's opacity setting.
- **Overlays are solid.** `UModal`/`UDrawer` content comes from the shared `utils/modal-ui.ts` preset (solid theme `bg-default`, widths per modal) — never invent a per-modal `:ui` content string, and never put glass classes on overlay content.
- **Onboarding is exempt from glass.** The layout and all onboarding views use solid surfaces (`bg-white dark:bg-neutral-900`, subtle `bg-neutral-50 dark:bg-neutral-800` accents) — first-run users have no background customization to show through, so translucency buys nothing and only risks legibility. Frost/transparency toggles do not apply there.
- **Borders** should be soft: `border-gray-200/70 dark:border-gray-700/70`.
- **Backdrop blur** (`frosted-glass`) is for page-plane floating elements (bars, popovers, sheets) to maintain legibility over a custom background. Blur over a flat dimmed scrim is a no-op, which is another reason overlays stay solid.
- Never use hard `bg-white` or `bg-gray-950` on full-bleed surfaces, these will clash with a tinted preset such as cream or with an image background.
- Glass is user-controlled (Settings > Appearance): frost on/off + strength + disable-on-mobile, transparency on/off + opacity, file thumbnails on/off. Never hardcode large blurs; avoid anything over 10px equivalent outside the shared variable. Off means fully solid surfaces.
- Opacity suffixes in this section apply only to `border-` classes and functional washes (status tints, accent washes, scrims). Surface translucency lives in `.glass-surface*`. Text sitting on these surfaces still uses the solid tokens from "Text color and opacity" above, never an opacity suffix of its own.

## Dark mode

- Always provide both light and dark variants for any custom color: `text-gray-700 dark:text-gray-300`.
- Do not use `@media (prefers-color-scheme)` directly, Nuxt UI and VueUse manage this via the `dark` class on `<html>`.
- When filtering or displaying mode-specific UI (e.g. only showing dark-mode presets in dark mode), consume `isDark` from `useTheme()`, never from the store or DOM.

## Component structure conventions

- Collapsible sections use `UCollapsible` with state persisted in the settings store (`isXxxSectionOpen`).
- Form controls are wrapped in `UFormField` with a `label` and `description`.
- Swatch/tile pickers follow this pattern: a grid of `<button>` elements, each with a colored `<span>` swatch, a label, and a ring + scale on the selected state (`ring-1 ring-primary scale-[1.03]`).
- Destructive/reset actions use `color="error" variant="outline"` and are placed at the top-right of their section, never inline with primary actions.
- File inputs are always `class="hidden"` and triggered by a `UButton` via a template ref.
- Overlays share `utils/modal-ui.ts` (content/body/drawer strings, solid by design); upload modals share `UploadModalShell` plus the `upload/` blocks (`UploadEmptyState`, `UploadFileTree`, `UploadProgressSummary`, `UploadModalFooter`) at `sm:max-w-2xl`. Per-modal steps stay in the modal; shared chrome stays in the blocks.
- Always use const name = () => {} instead of raw function declarations.
- Do not use em dash comment separators.
- Do not nest ternary operators.
- Avoid giant ternary operator chains in the template.

## Formatting

- Avoid heavy structure in conversational or settings UI, no gratuitous headers or bullet lists inside cards.
- Transitions on show/hide use Vue's `<Transition>` with `transition-all duration-200 ease-out` enter and `duration-150 ease-in` leave.
- Icon sizes follow the established scale: `w-3.5 h-3.5` for inline/xs, `w-4 h-4` for sm, `w-5 h-5` for base.
- Use `UIcon` with Lucide (`<UIcon name="i-lucide-..." />`) for every icon; filled states use the `icon-solid` class (see `skills/frontend-guide.md`).

---

## UI/UX Design Principles

These principles govern layout, spacing, and interaction design across the entire app. They take precedence over aesthetic preferences, consistency and clarity come first.

### Spacing system

Use an 8px base grid throughout. Tailwind spacing map:
- `gap-2` / `p-2` = 8px, tight inline spacing
- `gap-4` / `p-4` = 16px, default component padding
- `gap-6` / `p-6` = 24px, section separation
- `gap-8` / `p-8` = 32px, page-level breathing room

Never mix arbitrary spacing values (`p-[13px]`) with the scale. Stick to the grid.

### Layout width strategy

Desktop pages fill the content width. Never center a page in a `mx-auto max-w-*` column: it
leaves dead space on wide screens. Lay pages out as a full-width header bar followed by grids
that add columns as space grows, for example a fixed side column beside a fluid main area
(`lg:grid-cols-[20rem_minmax(0,1fr)]`) whose cards go two-up at `xl` (`xl:grid-cols-2`).
References: `views/dashboard/MyAccount.vue`, `views/dashboard/admin/AccessSecurity.vue`.

| Page type | Strategy |
|---|---|
| Files, photos, tag grids | Fluid auto-fill grid, `grid-cols-[repeat(auto-fill,minmax(120px,1fr))]` |
| Activity logs / lists | Full-width rows; filters or a summary move into a side column at `lg` |
| Settings / My account | Side column (navigation or profile) beside a fluid main column, cards two-up at `xl` |
| Storage / admin dashboards | Card grids that add columns at `lg` and `xl` |

Constrain width only for running text (a note body reads best near 65 characters) or for a
small form inside a card (`max-w-md`), never for the page itself.

The sidebar is resizable (13 to 20rem, default 14rem) and collapses to a 4rem icon rail, so
never assume its width. Add `px-6 py-5` to the content area and let each page control its own
internal layout.

### Information hierarchy

Every screen must have one clear primary action and a visual hierarchy of at most 3 levels:
1. **Primary**, filled `color="primary"` button, large heading
2. **Secondary**, outlined button, subheading, `text-gray-600 dark:text-gray-400` label
3. **Tertiary**, icon-only actions, timestamps, `text-gray-500 dark:text-gray-500` metadata

Never put two filled primary buttons side-by-side. Never put destructive actions at the same visual weight as constructive ones.

### Button system

Maintain one consistent button hierarchy:
- **Primary action** (Upload, Save): `variant="solid" color="primary"`
- **Secondary action** (New Folder, Cancel): `variant="outline" color="neutral"`
- **Destructive** (Delete, Reset): `variant="outline" color="error"`, top-right of section, never inline
- **Icon-only utilities** (sort, view toggle): `variant="ghost" color="neutral"` with tooltip

Dropdown arrows attached to a button (split buttons) must be visually grouped: same height, shared border, `rounded-l-none` on the arrow half.

Icon-only `square` `UButton`s need an explicit `justify-center` class. The theme base is `inline-flex items-center` with no horizontal centering and `square` adds none, so glyphs sit left without it.

### Toolbar density

Toolbars with more than 3 actions must be audited for necessity. Rules:
- On desktop: show primary action + one secondary, collapse the rest into a `⋯` overflow menu
- On mobile: show only the single most-used action; everything else goes in the overflow menu
- Sort controls and view toggles are secondary, they belong together in a visually grouped cluster, separated from primary actions by a divider

### Sidebar

The sidebar serves navigation only. Its sections are defined in
`features/navigation/sidebar.ts`; add new pages there, never as one-off links in the layout. It
must not contain:
- A storage widget in the nav list (usage lives in the footer user menu and on the My storage
  page)
- Account links mixed with primary nav (they live in the footer user menu)
- Log out as a prominent full-width button (the user menu holds it; on mobile it is a small
  muted text link at the very bottom, `text-gray-600 dark:text-gray-400`, no red fill)

Current grouping:
```
Library (always open, no label on mobile)
  Home, Files, Notes, Photos, Tags, Access history, Trash

Media (collapsible, only when streaming is enabled)
  Overview, Music, Videos, Playlists, Listening stats, Play history, Conversions

Admin (collapsible, admins only)
  Overview, Users, Storage, Access & security, System vitals, Incidents,
  Integrations, Configuration

Footer user menu (desktop popover)
  Storage usage ring and breakdown; Account, Settings, Report tiles; Log out

Account section (mobile, below the nav)
  My account, Settings, My storage, Report a problem; Log out link at the bottom
```

### Icon consistency

All icons within a single component must use the same style (all outlined OR all filled, never mixed). Across the app, use outlined icons for navigation and filled icons for active/selected states only. Never use two different icon libraries in the same view.

### File grid thumbnails

Grid tiles show backend image previews, not just file-type icons:

- Point `<img :src>` straight at `fileApi.getThumbnailUrlForVersion(fileId, versionId)` with `loading="lazy"`. Auth is cookie-based so no fetch layer is needed, and the version scope makes the URL permanently cacheable.
- Tiles are landscape 4:3 at 1.5x the grid icon size. The file-type icon renders as the placeholder until load and as the permanent fallback on error (documents 404 into the icon path). List view stays icon-only.
- Thumbnail state (URL + loaded/errored flags + reset on file change) lives in the `useFileThumbnail` composable; keep tile components thin. Mounting components that embed anonymous Nuxt UI children is impractical in specs (stubs cannot match them by name), so test the composable, not the tile.
- Users can switch back to pure icon tiles via Settings > Appearance ("File thumbnails").

### Color as communication

The accent (`primary`) communicates one thing: **this is selected or this is the main action**. Do not use it for:
- Decorative section headers
- Background fills on non-interactive elements
- Informational badges or labels

If you find yourself reaching for the accent to make something "pop", use font weight or size instead.

### Activity / list pages

Every list row must have at least two distinct visual zones:
- **Left**: icon + primary label
- **Right**: timestamp + badge/tag

Timestamps go in their own right-aligned column, not inline within the label text, styled `text-gray-500 dark:text-gray-500`. Action type badges should use `variant="subtle"` with a distinct color per action type (Create = green, Delete = red, Login = blue, etc.).

Use subtle row separation (`divide-y divide-gray-100/50 dark:divide-gray-800/50`) rather than cards for dense list views.

### Empty states

Every list/grid view must have an empty state. Empty state anatomy:
- Centered icon (`text-gray-400 dark:text-gray-600`, `w-12 h-12`)
- Short heading (`text-gray-900 dark:text-gray-100`)
- One-line explanation (`text-gray-600 dark:text-gray-400`, smaller)
- Optional CTA button if there's a relevant action

### Mobile-specific rules

On viewports below `md` (768px), apply these overrides:

- **File Explorer**: default to list view (`flex-col` rows), not grid. Let user opt into grid.
- **Tabs**: replace tab bar with bottom navigation (`fixed bottom-0`), 4 items max.
- **Toolbars**: collapse to single primary action + `⋯` overflow.
- **Tag cards**: horizontal layout, swatch left (`w-10 h-10 rounded-lg`), name + metadata right.
- **Settings color swatches**: wrap into rows that fill the available width
  (`flex flex-wrap gap-3`), circular tiles with ring + scale on the selected state.
- **Sidebar**: drawer pattern (already implemented) is correct. Keep it.
- **Don't show raw values** like `rgb(239 68 68)` to users, show color name or swatch only.

### Data display: show human values not raw values

- Colors: show a swatch + friendly name, not the raw `rgb()` string
- Dates: show relative time ("1 week ago") for recent items, absolute for older ones
- File sizes: always humanized (`4.23 MB`, not `4432897 bytes`)
- Storage percentages: show both used % and remaining GB, users care about "how much is left"
