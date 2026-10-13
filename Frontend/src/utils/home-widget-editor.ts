import type { HomeWidget, HomeWidgetOptions } from "@/types/home";

import { HomePlayerStyle, HomeWidgetType } from "@/enums/home-widget";
import { HOME_RECENT_FILES_STYLES, HOME_STORAGE_STYLES } from "@/utils/home-file-widgets";
import {
  HOME_COUNTDOWN_STYLES,
  HOME_DAILY_COVER_STYLES,
  HOME_FOCUS_STYLES,
} from "@/utils/home-general";
import { HOME_GREETING_STYLES } from "@/utils/home-greeting";
import { HOME_LIBRARY_STYLES } from "@/utils/home-library";
import { HOME_RECAP_STYLES } from "@/utils/home-media";
import { HOME_QUICK_ACCESS_STYLES, HOME_QUICK_UPLOAD_STYLES } from "@/utils/home-quick-widgets";
import { HOME_ACTIVITY_STYLES, HOME_STATUS_STYLES } from "@/utils/home-status-activity";
import { HOME_CLOCK_STYLES } from "@/utils/home-widget-options";

export type HomeOptionsSection = "all" | "style" | "options";

export const HOME_PLAYER_STYLES = [
  {
    label: "Vinyl",
    description: "A record that turns with playback.",
    value: HomePlayerStyle.Vinyl,
  },
  {
    label: "Traditional",
    description: "Square artwork and familiar controls.",
    value: HomePlayerStyle.Traditional,
  },
];

const styles: Partial<
  Record<
    HomeWidgetType,
    {
      key: keyof HomeWidgetOptions;
      choices: readonly { value: number; label: string; description: string }[];
    }
  >
> = {
  [HomeWidgetType.Greeting]: { key: "greetingStyle", choices: HOME_GREETING_STYLES },
  [HomeWidgetType.Clock]: { key: "clockStyle", choices: HOME_CLOCK_STYLES },
  [HomeWidgetType.LibraryShortcuts]: { key: "libraryStyle", choices: HOME_LIBRARY_STYLES },
  [HomeWidgetType.Countdown]: { key: "countdownStyle", choices: HOME_COUNTDOWN_STYLES },
  [HomeWidgetType.FocusTimer]: { key: "focusStyle", choices: HOME_FOCUS_STYLES },
  [HomeWidgetType.DailyCover]: { key: "dailyCoverStyle", choices: HOME_DAILY_COVER_STYLES },
  [HomeWidgetType.Storage]: { key: "storageStyle", choices: HOME_STORAGE_STYLES },
  [HomeWidgetType.RecentFiles]: { key: "recentFilesStyle", choices: HOME_RECENT_FILES_STYLES },
  [HomeWidgetType.QuickAccess]: { key: "quickAccessStyle", choices: HOME_QUICK_ACCESS_STYLES },
  [HomeWidgetType.QuickUpload]: { key: "quickUploadStyle", choices: HOME_QUICK_UPLOAD_STYLES },
  [HomeWidgetType.Player]: { key: "playerStyle", choices: HOME_PLAYER_STYLES },
  [HomeWidgetType.SystemStatus]: { key: "statusStyle", choices: HOME_STATUS_STYLES },
  [HomeWidgetType.RecentActivity]: { key: "activityStyle", choices: HOME_ACTIVITY_STYLES },
  [HomeWidgetType.LatestRecap]: { key: "recapStyle", choices: HOME_RECAP_STYLES },
};

export const homeWidgetHasStyles = (type: HomeWidgetType) => Boolean(styles[type]);

export const homeWidgetStyleChoices = (type: HomeWidgetType) => styles[type];

export const homeWidgetStyleLabel = (widget: HomeWidget) => {
  const definition = styles[widget.type];

  if (!definition) return "Default";

  return (
    definition.choices.find((choice) => choice.value === widget.options[definition.key])?.label ??
    "Default"
  );
};
