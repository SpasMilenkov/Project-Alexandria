import type {
  HomeEditorTarget,
  HomeLayout,
  HomeSettings,
  HomeWidget,
  HomeWidgetOptions,
} from "@/types/home";

import { HomeWidgetSize, HomeWidgetType } from "@/enums/home-widget";
import { homeWidgetFootprint, projectHomeLayout } from "@/utils/home-grid";
import { cloneHomeWidgetOptions, defaultHomeWidgetOptions } from "@/utils/home-widget-options";

export const MAX_HOME_WIDGETS = 32;

export const HOME_SIZES = [
  { label: "Small", value: HomeWidgetSize.Small },
  { label: "Medium", value: HomeWidgetSize.Medium },
  { label: "Large", value: HomeWidgetSize.Large },
];

export const cloneHomeLayout = (layout: HomeLayout): HomeLayout => ({
  columns: layout.columns,
  widgets: layout.widgets.map((widget) => ({
    ...widget,
    options: cloneHomeWidgetOptions(widget.options),
  })),
});

export const cloneHomeSettings = (settings: HomeSettings): HomeSettings => ({
  ...settings,
  desktop: cloneHomeLayout(settings.desktop),
  mobile: settings.mobile ? cloneHomeLayout(settings.mobile) : null,
});

export const createHomeWidget = (
  type: HomeWidgetType,
  instanceId: string,
  columns: HomeLayout["columns"] = 12,
): HomeWidget => ({
  instanceId,
  type,
  size: HomeWidgetSize.Medium,
  x: 0,
  y: 0,
  showTitle: null,
  ...homeWidgetFootprint(HomeWidgetSize.Medium, columns),
  options: defaultHomeWidgetOptions(),
});

export const updateHomeWidgetOptions = (
  layout: HomeLayout,
  instanceId: string,
  patch: Partial<HomeWidgetOptions>,
) => {
  const widget = layout.widgets.find((item) => item.instanceId === instanceId);
  if (!widget) return false;

  widget.options = cloneHomeWidgetOptions({ ...widget.options, ...patch });

  return true;
};

export const defaultHomeLayout = (columns: HomeLayout["columns"] = 12): HomeLayout => ({
  columns,
  widgets: [
    {
      ...createHomeWidget(HomeWidgetType.Clock, crypto.randomUUID()),
      ...homeWidgetFootprint(HomeWidgetSize.Medium, columns),
    },
    {
      ...createHomeWidget(HomeWidgetType.LibraryShortcuts, crypto.randomUUID()),
      size: HomeWidgetSize.Large,
      y: 4,
      ...homeWidgetFootprint(HomeWidgetSize.Large, columns),
    },
  ],
});

export const setSeparateMobileLayout = (settings: HomeSettings, enabled: boolean) => {
  if (enabled && !settings.mobile) {
    settings.mobile = projectHomeLayout(settings.desktop, 4);
    settings.mobile.widgets.forEach((widget) => {
      widget.instanceId = crypto.randomUUID();
    });
  }

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
