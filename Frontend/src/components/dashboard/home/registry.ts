import { type Component, defineAsyncComponent } from "vue";

import type { HomeWidgetOptions } from "@/types/home";

import { HomeShortcutGroup, HomeWidgetSize, HomeWidgetType } from "@/enums/home-widget";

interface HomeWidgetDefinition {
  type: HomeWidgetType;
  label: string;
  description: string;
  category: string;
  icon: string;
  sizes: HomeWidgetSize[];
  component: Component;
  defaultOptions: HomeWidgetOptions;
  optionControl: "time-zone" | "shortcut-group";
  access: "user";
}

export const HOME_WIDGET_REGISTRY: Record<HomeWidgetType, HomeWidgetDefinition> = {
  [HomeWidgetType.Clock]: {
    type: HomeWidgetType.Clock,
    label: "Clock",
    description: "The time and date, at home or in another time zone.",
    category: "General",
    icon: "i-heroicons-clock",
    sizes: [HomeWidgetSize.Small, HomeWidgetSize.Medium, HomeWidgetSize.Large],
    component: defineAsyncComponent(() => import("./widgets/HomeClockWidget.vue")),
    defaultOptions: { timeZone: "local", shortcutGroup: HomeShortcutGroup.Files },
    optionControl: "time-zone",
    access: "user",
  },
  [HomeWidgetType.LibraryShortcuts]: {
    type: HomeWidgetType.LibraryShortcuts,
    label: "Library shortcuts",
    description: "Keep your file or media destinations within reach.",
    category: "Library",
    icon: "i-heroicons-book-open",
    sizes: [HomeWidgetSize.Small, HomeWidgetSize.Medium, HomeWidgetSize.Large],
    component: defineAsyncComponent(() => import("./widgets/HomeLibraryWidget.vue")),
    defaultOptions: { timeZone: "local", shortcutGroup: HomeShortcutGroup.Files },
    optionControl: "shortcut-group",
    access: "user",
  },
};

export const homeWidgetDefinitions = Object.values(HOME_WIDGET_REGISTRY);
