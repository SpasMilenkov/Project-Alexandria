import type { HomeLayout } from "@/types/home";

import { HomeWidgetSize, HomeWidgetType } from "@/enums/home-widget";

const minimums: Record<HomeWidgetType, { desktop: number; mobile: number; height: number }> = {
  [HomeWidgetType.Clock]: { desktop: 3, mobile: 2, height: 2 },
  [HomeWidgetType.LibraryShortcuts]: { desktop: 3, mobile: 2, height: 2 },
};

export const homeWidgetMinimum = (type: HomeWidgetType, columns: HomeLayout["columns"]) => {
  const minimum = minimums[type];

  return {
    width: columns === 4 ? minimum.mobile : minimum.desktop,
    height: minimum.height,
  };
};

export const homeWidgetDensity = (
  width: number,
  height: number,
  columns: HomeLayout["columns"],
) => {
  const desktopWidth = (width * 12) / columns;
  const area = desktopWidth * height;

  if (area < 24) return HomeWidgetSize.Small;
  if (area >= 72 && desktopWidth >= 6 && height >= 4) return HomeWidgetSize.Large;

  return HomeWidgetSize.Medium;
};
