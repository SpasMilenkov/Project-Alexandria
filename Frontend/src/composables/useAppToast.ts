import type { ToastProps } from "@nuxt/ui";

import { h } from "vue";

import { describeToastError, isRetriableError } from "@/features/toast/errors";
import { TOAST_SOURCES, type ToastSource } from "@/features/toast/sources";
import { type ToastLevel, useSettingsStore } from "@/stores/settings";

type Severity = "error" | "info" | "success" | "warning";

type ToastActions = NonNullable<ToastProps["actions"]>;

export interface AppToastOptions {
  actions?: ToastActions;
  description?: string;
  duration?: number;
  // Toasts sharing an id collapse into one instead of stacking; defaults to source + severity + title.
  id?: string;
  source?: ToastSource;
}

export interface AppToastErrorOptions extends AppToastOptions {
  retry?: () => unknown;
}

export interface TrackedToast {
  dismiss: () => void;
  fail: (title: string, cause?: unknown, options?: AppToastErrorOptions) => void;
  succeed: (title: string, options?: string | AppToastOptions) => void;
  warn: (title: string, options?: string | AppToastOptions) => void;
}

// Errors linger so there is time to read the cause; confirmations get out of the way quickly.
const DURATIONS: Record<Severity, number> = {
  error: 8000,
  info: 4000,
  success: 4000,
  warning: 6000,
};

const VISIBLE_AT: Record<Severity, ToastLevel[]> = {
  error: ["all", "errors-only"],
  info: ["all"],
  success: ["all"],
  warning: ["all", "errors-only"],
};

// Avatar sizes are set per toast: a theme-level avatarSize merges with the default into an invalid size.
const TILE_SIZE = "lg" as const;

// Literal class names so Tailwind generates them; the default 10% wash disappears on light backgrounds.
const TILE_CLASSES: Record<Severity | "neutral", string> = {
  error: "bg-error/15",
  info: "bg-info/15",
  neutral: "bg-gray-500/15",
  success: "bg-success/15",
  warning: "bg-warning/15",
};

const normalizeOptions = <T extends AppToastOptions>(options?: string | T): T => {
  if (typeof options === "string") return { description: options } as T;

  return options ?? ({} as T);
};

const withActionDefaults = (actions?: ToastActions): ToastActions | undefined =>
  actions?.map((action) => ({ color: "neutral", variant: "outline", ...action }));

const renderTitle = (label: string, title: string) => () =>
  h("span", { class: "flex min-w-0 flex-col" }, [
    h(
      "span",
      { class: "text-xs font-medium text-gray-500 dark:text-gray-500", "data-toast-source": "" },
      label,
    ),
    h("span", { class: "mt-0.5 wrap-break-word" }, title),
  ]);

const buildToast = (
  severity: Severity,
  title: string,
  source: ToastSource,
  options: AppToastOptions,
) => {
  const meta = TOAST_SOURCES[source];

  return {
    actions: withActionDefaults(options.actions),
    avatar: {
      color: severity,
      icon: meta.icon,
      size: TILE_SIZE,
      ui: { root: TILE_CLASSES[severity] },
    },
    color: severity,
    description: options.description,
    duration: options.duration ?? DURATIONS[severity],
    title: renderTitle(meta.label, title),
  };
};

const retryAction = (retry: () => unknown): ToastActions[number] => ({
  icon: "i-lucide-rotate-ccw",
  label: "Retry",
  onClick: () => {
    void retry();
  },
});

const createTrackId = (source: ToastSource) =>
  `${source}:track:${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;

export const useAppToast = (defaultSource: ToastSource = "general") => {
  const toast = useToast();
  const settings = useSettingsStore();

  const isVisible = (severity: Severity) => VISIBLE_AT[severity].includes(settings.toastLevel);

  const resolveErrorOptions = (cause: unknown, options: AppToastErrorOptions): AppToastOptions => {
    const description = options.description ?? describeToastError(cause);
    const actions = [...(options.actions ?? [])];

    if (options.retry && isRetriableError(cause)) actions.push(retryAction(options.retry));

    return { ...options, actions, description };
  };

  const show = (severity: Severity, title: string, options: AppToastOptions) => {
    if (!isVisible(severity)) return;

    const source = options.source ?? defaultSource;
    const id = options.id ?? `${source}:${severity}:${title}`;

    toast.add({ ...buildToast(severity, title, source, options), id });
  };

  const track = (title: string, options: string | AppToastOptions = {}): TrackedToast => {
    const pendingOptions = normalizeOptions(options);
    const source = pendingOptions.source ?? defaultSource;
    const id = pendingOptions.id ?? createTrackId(source);

    // Progress is only worth showing to people who also want the success confirmation.
    let isShown = isVisible("success");

    if (isShown) {
      toast.add({
        avatar: {
          color: "neutral",
          icon: "i-lucide-loader-circle",
          size: TILE_SIZE,
          ui: { icon: "motion-safe:animate-spin", root: TILE_CLASSES.neutral },
        },
        color: "neutral",
        description: pendingOptions.description,
        duration: 0,
        id,
        title: renderTitle(TOAST_SOURCES[source].label, title),
      });
    }

    const settle = (severity: Severity, settledTitle: string, settledOptions: AppToastOptions) => {
      if (!isVisible(severity)) {
        if (isShown) toast.remove(id);
        isShown = false;
        return;
      }

      const body = buildToast(
        severity,
        settledTitle,
        settledOptions.source ?? source,
        settledOptions,
      );

      if (isShown) {
        toast.update(id, body);
        return;
      }

      toast.add({ ...body, id });
      isShown = true;
    };

    return {
      dismiss: () => {
        if (isShown) toast.remove(id);
        isShown = false;
      },
      fail: (failedTitle, cause, failedOptions = {}) =>
        settle("error", failedTitle, resolveErrorOptions(cause, failedOptions)),
      succeed: (doneTitle, doneOptions) =>
        settle("success", doneTitle, normalizeOptions(doneOptions)),
      warn: (warnTitle, warnOptions) => settle("warning", warnTitle, normalizeOptions(warnOptions)),
    };
  };

  return {
    // `cause` is either a ready description or whatever was thrown; thrown errors become a readable cause line.
    error(title: string, cause?: unknown, options: AppToastErrorOptions = {}) {
      show("error", title, resolveErrorOptions(cause, options));
    },

    info(title: string, options?: string | AppToastOptions) {
      show("info", title, normalizeOptions(options));
    },

    success(title: string, options?: string | AppToastOptions) {
      show("success", title, normalizeOptions(options));
    },

    track,

    warning(title: string, options?: string | AppToastOptions) {
      show("warning", title, normalizeOptions(options));
    },
  };
};

export type AppToast = ReturnType<typeof useAppToast>;
