import type { HomeWidget, HomeWidgetOptions } from "@/types/home";

import {
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
  HomeWidgetType,
} from "@/enums/home-widget";

export const isHomeClockFaceOnly = (widget: HomeWidget) =>
  widget.type === HomeWidgetType.Clock && widget.width <= 2 && widget.height <= 2;

export const cloneHomeWidgetOptions = (options: HomeWidgetOptions): HomeWidgetOptions => ({
  ...options,
  shortcuts: options.shortcuts.map((shortcut) => ({ ...shortcut })),
  recapPalette: options.recapPalette ? [...options.recapPalette] : null,
});

export const defaultHomeWidgetOptions = (): HomeWidgetOptions => ({
  greetingStyle: HomeGreetingStyle.Minimal,
  greetingDecoration: HomeGreetingDecoration.WaveEmoji,
  greetingColorMode: HomeGreetingColorMode.Monochrome,
  greetingAlignment: HomeHorizontalAlignment.Left,
  greetingShowName: true,
  greetingShowDate: false,
  greetingHeading: null,
  greetingSubtitle: "Everything in your library, arranged your way.",
  countdownStyle: HomeCountdownStyle.BigNumber,
  countdownLabel: "Countdown",
  countdownTarget: null,
  focusStyle: HomeFocusStyle.Ring,
  focusMinutes: 25,
  shortBreakMinutes: 5,
  longBreakMinutes: 15,
  focusSessions: 4,
  dailyCoverStyle: HomeDailyCoverStyle.Riso,
  storageStyle: HomeStorageStyle.Meter,
  recentFilesStyle: HomeRecentFilesStyle.List,
  recentFilesSource: HomeRecentFilesSource.Created,
  quickAccessStyle: HomeQuickAccessStyle.Tiles,
  quickUploadStyle: HomeQuickUploadStyle.DropZone,
  playerStyle: HomePlayerStyle.Vinyl,
  statusStyle: HomeStatusStyle.Beacon,
  activityStyle: HomeActivityStyle.Feed,
  recapStyle: HomeRecapStyle.Poster,
  recapPeriod: HomeRecapPeriod.CurrentMonth,
  recapPalette: null,
  timeZone: "local",
  libraryStyle: HomeLibraryStyle.Launchpad,
  shortcutGroup: HomeShortcutGroup.Files,
  shortcuts: [],
  featuredDestination: null,
  clockStyle: HomeClockStyle.Minimal,
  hourFormat: HomeClockHourFormat.DeviceDefault,
  showSeconds: false,
  horizontalAlignment: HomeHorizontalAlignment.Left,
  verticalAlignment: HomeVerticalAlignment.Middle,
  textAlignment: HomeHorizontalAlignment.Left,
  textVerticalAlignment: HomeVerticalAlignment.Bottom,
});

export const HOME_CLOCK_STYLES = [
  {
    label: "Quiet",
    description: "Clear digits, a greeting and the date.",
    value: HomeClockStyle.Minimal,
  },
  {
    label: "Watch",
    description: "A classic dial with a sweeping seconds hand.",
    value: HomeClockStyle.Analog,
  },
  {
    label: "Tile",
    description: "Softened digits in a rounded face.",
    value: HomeClockStyle.Material,
  },
  {
    label: "Flip",
    description: "Split-flap digits with a gentle flip when they change.",
    value: HomeClockStyle.Flip,
  },
  {
    label: "Rings",
    description: "Hour, minute and second rings in the file-type palette.",
    value: HomeClockStyle.Rings,
  },
  {
    label: "Words",
    description: "A sentence rounded to five minutes, with exact digits below.",
    value: HomeClockStyle.Words,
  },
];

export const HOME_CLOCK_HOUR_FORMATS = [
  { label: "Device default", value: HomeClockHourFormat.DeviceDefault },
  { label: "12-hour", value: HomeClockHourFormat.TwelveHour },
  { label: "24-hour", value: HomeClockHourFormat.TwentyFourHour },
];

export const HOME_TEXT_ALIGNMENTS = [
  { label: "Left", value: HomeHorizontalAlignment.Left },
  { label: "Center", value: HomeHorizontalAlignment.Center },
  { label: "Right", value: HomeHorizontalAlignment.Right },
];

const verticalAlignments = [
  { label: "Top", value: HomeVerticalAlignment.Top },
  { label: "Middle", value: HomeVerticalAlignment.Middle },
  { label: "Bottom", value: HomeVerticalAlignment.Bottom },
];

export const HOME_CLOCK_ANCHORS = verticalAlignments.flatMap((vertical) =>
  HOME_TEXT_ALIGNMENTS.map((horizontal) => ({
    label: `${vertical.label.toLowerCase()} ${horizontal.label.toLowerCase()}`,
    horizontal: horizontal.value,
    vertical: vertical.value,
  })),
);
