import type { HomeShortcutGroup, HomeWidgetSize, HomeWidgetType } from "@/enums/home-widget";

export interface HomeWidgetOptions {
  timeZone: string;
  shortcutGroup: HomeShortcutGroup;
}

export interface HomeWidget {
  instanceId: string;
  type: HomeWidgetType;
  size: HomeWidgetSize;
  x: number;
  y: number;
  width: number;
  height: number;
  options: HomeWidgetOptions;
}

export interface HomeLayout {
  columns: 4 | 12;
  widgets: HomeWidget[];
}

export interface HomeSettings {
  schemaVersion: number;
  useSeparateMobileLayout: boolean;
  desktop: HomeLayout;
  mobile: HomeLayout | null;
}

export type HomeEditorTarget = "desktop" | "mobile";
