import type {
  HomeActivityStyle,
  HomeClockHourFormat,
  HomeClockStyle,
  HomeCountdownStyle,
  HomeDailyCoverStyle,
  HomeFocusStyle,
  HomeGreetingColorMode,
  HomeGreetingDecoration,
  HomeGreetingStyle,
  HomeHorizontalAlignment,
  HomeLibraryDestination,
  HomeLibraryStyle,
  HomePlayerStyle,
  HomeQuickAccessStyle,
  HomeQuickUploadStyle,
  HomeRecapPeriod,
  HomeRecapStyle,
  HomeRecentFilesSource,
  HomeRecentFilesStyle,
  HomeShortcutGroup,
  HomeStatusStyle,
  HomeStorageStyle,
  HomeVerticalAlignment,
  HomeWidgetSize,
  HomeWidgetType,
} from "@/enums/home-widget";
import type { WrappedPalette } from "@/utils/wrapped-palette.utils";

export interface HomeClockOptions {
  timeZone: string;
  clockStyle: HomeClockStyle;
  hourFormat: HomeClockHourFormat;
  showSeconds: boolean;
  horizontalAlignment: HomeHorizontalAlignment;
  verticalAlignment: HomeVerticalAlignment;
  textAlignment: HomeHorizontalAlignment;
  textVerticalAlignment: HomeVerticalAlignment;
}

export interface HomeStorageOptions {
  storageStyle: HomeStorageStyle;
}

export interface HomeGreetingOptions {
  greetingStyle: HomeGreetingStyle;
  greetingDecoration: HomeGreetingDecoration;
  greetingColorMode: HomeGreetingColorMode;
  greetingAlignment: HomeHorizontalAlignment;
  greetingShowName: boolean;
  greetingShowDate: boolean;
  greetingHeading: string | null;
  greetingSubtitle: string;
}

export interface HomeRecentFilesOptions {
  recentFilesStyle: HomeRecentFilesStyle;
  recentFilesSource: HomeRecentFilesSource;
}

export interface HomeLibraryShortcut {
  destination: HomeLibraryDestination;
  label: string | null;
}

export interface HomeLibraryOptions {
  libraryStyle: HomeLibraryStyle;
  shortcutGroup: HomeShortcutGroup;
  shortcuts: HomeLibraryShortcut[];
  featuredDestination: HomeLibraryDestination | null;
}

export interface HomeQuickAccessOptions {
  quickAccessStyle: HomeQuickAccessStyle;
}

export interface HomeQuickUploadOptions {
  quickUploadStyle: HomeQuickUploadStyle;
}

export interface HomePlayerOptions {
  playerStyle: HomePlayerStyle;
}

export interface HomeStatusOptions {
  statusStyle: HomeStatusStyle;
}

export interface HomeActivityOptions {
  activityStyle: HomeActivityStyle;
}

export interface HomeRecapOptions {
  recapStyle: HomeRecapStyle;
  recapPeriod: HomeRecapPeriod;
  recapPalette: WrappedPalette | null;
}

export interface HomeCountdownOptions {
  countdownStyle: HomeCountdownStyle;
  countdownLabel: string;
  countdownTarget: string | null;
}

export interface HomeFocusOptions {
  focusStyle: HomeFocusStyle;
  focusMinutes: number;
  shortBreakMinutes: number;
  longBreakMinutes: number;
  focusSessions: number;
}

export interface HomeDailyCoverOptions {
  dailyCoverStyle: HomeDailyCoverStyle;
}

export interface HomeWidgetOptions
  extends
    HomeClockOptions,
    HomeStorageOptions,
    HomeRecentFilesOptions,
    HomeLibraryOptions,
    HomeQuickAccessOptions,
    HomeQuickUploadOptions,
    HomePlayerOptions,
    HomeStatusOptions,
    HomeActivityOptions,
    HomeRecapOptions,
    HomeCountdownOptions,
    HomeFocusOptions,
    HomeDailyCoverOptions,
    HomeGreetingOptions {}

export interface HomeWidget {
  instanceId: string;
  type: HomeWidgetType;
  size: HomeWidgetSize;
  x: number;
  y: number;
  width: number;
  height: number;
  showTitle: boolean | null;
  options: HomeWidgetOptions;
}

export interface HomeLayout {
  columns: 4 | 12;
  widgets: HomeWidget[];
}

export interface HomeSettings {
  initialized: boolean;
  useSeparateMobileLayout: boolean;
  showWidgetTitles: boolean;
  showHeaderGreeting: boolean;
  desktop: HomeLayout;
  mobile: HomeLayout | null;
}

export type HomeEditorTarget = "desktop" | "mobile";
