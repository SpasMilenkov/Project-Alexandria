# Alexandria — Frontend

The frontend for Project Alexandria, a self-hosted file management platform. Built with Vue 3, TypeScript, and Vite.

---

## Tech Stack

| Tool                                               | Purpose                                              |
| -------------------------------------------------- | ---------------------------------------------------- |
| [Vue 3](https://vuejs.org/)                        | UI framework (Composition API + `<script setup>`)    |
| [TypeScript](https://www.typescriptlang.org/)      | Type safety across the entire codebase               |
| [Vite](https://vite.dev/)                          | Dev server and bundler                               |
| [Nuxt UI v4](https://ui.nuxt.com/)                 | Component library built on top of Tailwind CSS       |
| [Tailwind CSS v4](https://tailwindcss.com/)        | Utility-first styling                                |
| [Pinia](https://pinia.vuejs.org/)                  | Global state management                              |
| [@pinia/colada](https://pinia-colada.esm.dev/)     | Async data fetching and caching layer                |
| [Vue Router](https://router.vuejs.org/)            | Client-side routing                                  |
| [Axios](https://axios-http.com/)                   | HTTP client                                          |
| [Zod](https://zod.dev/)                            | Runtime schema validation                            |
| [Iconify](https://iconify.design/)                 | Icon sets (Heroicons, Lucide, MDI, Material Symbols) |
| [Serwist](https://serwist.pages.dev/)              | Service worker / PWA support                         |
| [Vitest](https://vitest.dev/)                      | Unit testing (jsdom environment)                     |
| [Playwright](https://playwright.dev/)              | End-to-end testing                                   |
| [oxlint](https://oxc.rs/docs/guide/usage/linter)   | Fast linter (TypeScript + Vue plugins)               |
| [oxfmt](https://oxc.rs/docs/guide/usage/formatter) | Opinionated code formatter                           |

---

## Prerequisites

- **Node.js** `^20.19.0` or `>=22.12.0`
- **pnpm** `10.32.1+` — this project uses pnpm workspaces

Install pnpm if you don't have it:

```sh
npm install -g pnpm
```

---

## Getting Started

```sh
# Install dependencies
pnpm install

# Start the dev server with hot-reload at http://localhost:5173
pnpm dev
```

> The dev server expects the backend API to be running at `http://localhost:5000`.
> See the root `README.md` for instructions on spinning up all infrastructure services.

---

## Scripts

### `pnpm dev`

Starts the Vite development server at `http://localhost:5173` with hot module replacement (HMR).
Vue DevTools and the Serwist service worker are both active in dev mode.

---

### `pnpm build`

Runs a full production build. This is the command you want before deploying or building a Docker image.

Internally it runs two tasks **in parallel**:

| Step       | Command           | What it does                                                                                             |
| ---------- | ----------------- | -------------------------------------------------------------------------------------------------------- |
| Type check | `vue-tsc --build` | Validates all TypeScript and `.vue` files across all tsconfig references (`app`, `node`, `vitest`, `sw`) |
| Bundle     | `vite build`      | Compiles, tree-shakes, and minifies everything into `dist/`                                              |

The build will **fail** if there are any type errors. Fix type errors before shipping.

---

### `pnpm build-only`

Runs **only** the Vite bundler step, skipping type checking. Useful when you want a fast build and have already confirmed types are clean.

```sh
pnpm build-only
# Pass extra Vite CLI flags
pnpm build-only -- --mode staging
```

---

### `pnpm type-check`

Runs `vue-tsc --build` across all TypeScript project references without emitting any output files. Use this to validate types in CI or before committing.

```sh
pnpm type-check
```

---

### `pnpm preview`

Serves the contents of `dist/` locally using Vite's preview server. Run this after `pnpm build` to verify the production bundle before deploying.

```sh
pnpm build && pnpm preview
# Preview server starts at http://localhost:4173
```

---

### `pnpm test:unit`

Runs unit tests with [Vitest](https://vitest.dev/) in a `jsdom` environment. Tests live in `src/__tests__/`.

```sh
# Run all unit tests
pnpm test:unit

# Watch mode (re-runs on file changes)
pnpm test:unit --watch

# Run with coverage report
pnpm test:unit --coverage

# Run a specific test file
pnpm test:unit src/__tests__/example.spec.ts
```

Vitest inherits the Vite config (plugins, aliases, etc.) so you can import components exactly as they appear in the app.

---

### `pnpm test:e2e`

Runs end-to-end tests with [Playwright](https://playwright.dev/). Tests live in `e2e/` and run against **Chromium**, **Firefox**, and **WebKit** by default.

**First-time setup — install browsers:**

```sh
pnpm exec playwright install
```

**Running tests:**

```sh
# Run all E2E tests (dev server starts automatically)
pnpm test:e2e

# Run only on a specific browser
pnpm test:e2e -- --project=chromium
pnpm test:e2e -- --project=firefox
pnpm test:e2e -- --project=webkit

# Run a specific test file
pnpm test:e2e -- e2e/example.spec.ts

# Run in debug/headed mode
pnpm test:e2e -- --debug

# Open the Playwright UI (interactive runner)
pnpm exec playwright test --ui
```

**Viewing HTML reports** after a run:

```sh
pnpm exec playwright show-report
```

---

### `pnpm lint`

Lints the entire project using [oxlint](https://oxc.rs/docs/guide/usage/linter) with TypeScript and Vue plugins enabled. Rules are configured in `.oxlintrc.json`.

```sh
pnpm lint
```

Key rules enforced:

- `no-console` and `no-debugger` are **errors**
- `correctness` and `suspicious` categories are **errors**
- `pedantic` and `style` categories are **warnings**
- Files are capped at **500 lines**, functions at **100 lines**

---

### `pnpm lint:fix`

Same as `pnpm lint` but automatically fixes all auto-fixable violations in place.

```sh
pnpm lint:fix
```

---

### `pnpm fmt`

Formats all source files using [oxfmt](https://oxc.rs/docs/guide/usage/formatter). Formatting options live in `.oxfmtrc.json`.

```sh
pnpm fmt
```

---

### `pnpm fmt:check`

Checks formatting without writing any changes. Exits with a non-zero code if any file is out of format. Useful for CI.

```sh
pnpm fmt:check
```

---

## Project Structure

```
Frontend/
├── e2e/                   # Playwright end-to-end tests
├── plugins/               # Custom Vite plugins (e.g. Iconify subsetting)
├── public/                # Static assets served as-is
└── src/
    ├── api/               # Axios API clients per resource
    ├── assets/            # Images, fonts, global CSS
    ├── components/        # Reusable Vue components
    ├── composables/       # Shared Composition API logic
    ├── enums/             # TypeScript enums
    ├── icons/             # Custom icon definitions
    ├── layouts/           # Page layout wrappers
    ├── mutations/         # @pinia/colada mutation hooks
    ├── queries/           # @pinia/colada query hooks
    ├── router/            # Vue Router configuration
    ├── schemas/           # Zod schemas for API responses and forms
    ├── stores/            # Pinia stores (global state)
    ├── types/             # Shared TypeScript types and interfaces
    ├── utils/             # Pure utility/helper functions
    ├── views/             # Route-level page components
    ├── workers/           # Web Worker scripts
    ├── __tests__/         # Vitest unit tests
    ├── App.vue            # Root application component
    ├── main.ts            # Application entry point
    └── sw.ts              # Service worker (Serwist)
```

---

## TypeScript Configuration

The project uses multiple `tsconfig` references to keep compilation scopes separate:

| Config                 | Scope                            |
| ---------------------- | -------------------------------- |
| `tsconfig.app.json`    | Main application source (`src/`) |
| `tsconfig.node.json`   | Vite config and build tooling    |
| `tsconfig.vitest.json` | Vitest test files                |
| `tsconfig.sw.json`     | Service worker (`src/sw.ts`)     |

`tsconfig.json` is the root reference file that ties them all together.

---

## Docker

The frontend ships as a static Nginx container. The `Dockerfile` builds the production bundle and serves it via `nginx.conf`.

```sh
# Build the image
docker build -t alexandria-frontend .

# Or let docker compose handle it from the repo root
docker compose up --build frontend
```
