import type { HomeEditorTarget, HomeLayout, HomeSettings, HomeWidget } from "@/types/home";

import { HomeShortcutGroup, HomeWidgetSize, HomeWidgetType } from "@/enums/home-widget";
import {
  homeSpatialOrder,
  homeWidgetFootprint,
  placeHomeWidget,
  projectHomeLayout,
} from "@/utils/home-grid";

export const MAX_HOME_WIDGETS = 32;

export const HOME_SIZES = [
  { label: "Small", value: HomeWidgetSize.Small },
  { label: "Medium", value: HomeWidgetSize.Medium },
  { label: "Large", value: HomeWidgetSize.Large },
];

export const cloneHomeLayout = (layout: HomeLayout): HomeLayout => ({
  columns: layout.columns,
  widgets: layout.widgets.map((widget) => ({ ...widget, options: { ...widget.options } })),
});

export const cloneHomeSettings = (settings: HomeSettings): HomeSettings => ({
  ...settings,
  desktop: cloneHomeLayout(settings.desktop),
  mobile: settings.mobile ? cloneHomeLayout(settings.mobile) : null,
});

export const createHomeWidget = (type: HomeWidgetType, instanceId: string): HomeWidget => ({
  instanceId,
  type,
  size: HomeWidgetSize.Medium,
  x: 0,
  y: 0,
  width: 6,
  height: 4,
  options: { timeZone: "local", shortcutGroup: HomeShortcutGroup.Files },
});

export const defaultHomeLayout = (columns: HomeLayout["columns"] = 12): HomeLayout => ({
  columns,
  widgets: [
    {
      ...createHomeWidget(HomeWidgetType.Clock, "default-clock"),
      ...homeWidgetFootprint(HomeWidgetSize.Medium, columns),
    },
    {
      ...createHomeWidget(HomeWidgetType.LibraryShortcuts, "default-library"),
      size: HomeWidgetSize.Large,
      y: 4,
      ...homeWidgetFootprint(HomeWidgetSize.Large, columns),
    },
  ],
});

export const setSeparateMobileLayout = (settings: HomeSettings, enabled: boolean) => {
  if (enabled && !settings.mobile) settings.mobile = projectHomeLayout(settings.desktop, 4);

  settings.useSeparateMobileLayout = enabled;
};

export const selectedHomeLayout = (
  settings: HomeSettings,
  target: HomeEditorTarget,
): HomeLayout => {
  if (target === "mobile" && settings.useSeparateMobileLayout && settings.mobile) {
    return settings.mobile;
  }

  return settings.desktop;
};

export const moveHomeWidget = (layout: HomeLayout, fromId: string, toIndex: number) => {
  const fromIndex = layout.widgets.findIndex((widget) => widget.instanceId === fromId);

  if (fromIndex < 0 || toIndex < 0 || toIndex >= layout.widgets.length || fromIndex === toIndex)
    return;

  const target = layout.widgets[toIndex]!;
  const moving = layout.widgets[fromIndex]!;
  const candidate = {
    ...moving,
    x: Math.min(target.x, layout.columns - moving.width),
    y: target.y,
  };
  if (fromIndex < toIndex) candidate.y += target.height;
  if (!placeHomeWidget(layout, candidate)) return;

  layout.widgets.sort(homeSpatialOrder);
};
