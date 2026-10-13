import { type Component, defineAsyncComponent } from "vue";

import { HomeWidgetSize, HomeWidgetType } from "@/enums/home-widget";

interface HomeWidgetDefinition {
  type: HomeWidgetType;
  label: string;
  description: string;
  category: "Files" | "Media" | "System" | "General";
  icon: string;
  sizes: HomeWidgetSize[];
  component: Component;
  optionsComponent?: Component;
}

const sizes = [HomeWidgetSize.Small, HomeWidgetSize.Medium, HomeWidgetSize.Large];

export const HOME_WIDGET_REGISTRY: Record<HomeWidgetType, HomeWidgetDefinition> = {
  [HomeWidgetType.Greeting]: {
    type: HomeWidgetType.Greeting,
    category: "General",
    label: "Greeting",
    description: "Make yourself at home with a personal welcome.",
    icon: "i-heroicons-sparkles",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeGreetingWidget.vue")),
    optionsComponent: defineAsyncComponent(() => import("./HomeGreetingWidgetOptions.vue")),
  },
  [HomeWidgetType.Countdown]: {
    type: HomeWidgetType.Countdown,
    category: "General",
    label: "Countdown",
    description: "Count the days until a date that matters to you.",
    icon: "i-heroicons-calendar",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeCountdownWidget.vue")),
    optionsComponent: defineAsyncComponent(() => import("./HomeCountdownWidgetOptions.vue")),
  },
  [HomeWidgetType.FocusTimer]: {
    type: HomeWidgetType.FocusTimer,
    category: "General",
    label: "Focus timer",
    description: "Make room for focus, with short and long breaks.",
    icon: "i-heroicons-clock",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeFocusWidget.vue")),
    optionsComponent: defineAsyncComponent(() => import("./HomeFocusWidgetOptions.vue")),
  },
  [HomeWidgetType.DailyCover]: {
    type: HomeWidgetType.DailyCover,
    category: "General",
    label: "Daily cover",
    description: "A fresh piece of generated artwork for each day.",
    icon: "i-heroicons-photo",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeDailyCoverWidget.vue")),
    optionsComponent: defineAsyncComponent(() => import("./HomeDailyCoverWidgetOptions.vue")),
  },
  [HomeWidgetType.TranscodingJobs]: {
    type: HomeWidgetType.TranscodingJobs,
    category: "System",
    label: "Transcoding jobs",
    description: "Your running, queued and failed media encoding jobs.",
    icon: "i-heroicons-arrow-path",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeTranscodingJobsWidget.vue")),
  },
  [HomeWidgetType.LatestRecap]: {
    type: HomeWidgetType.LatestRecap,
    category: "Media",
    label: "Latest recap",
    description: "Your month in music, as a poster, numbers or a story preview.",
    icon: "i-heroicons-musical-note",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeLatestRecapWidget.vue")),
    optionsComponent: defineAsyncComponent(() => import("./HomeLatestRecapWidgetOptions.vue")),
  },
  [HomeWidgetType.Collections]: {
    type: HomeWidgetType.Collections,
    category: "Media",
    label: "Collections",
    description: "A collage of the automatic collections in your music library.",
    icon: "i-heroicons-book-open",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeCollectionsWidget.vue")),
  },
  [HomeWidgetType.Clock]: {
    type: HomeWidgetType.Clock,
    category: "General",
    label: "Clock",
    description: "The time and date, at home or in another time zone.",
    icon: "i-heroicons-clock",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeClockWidget.vue")),
    optionsComponent: defineAsyncComponent(() => import("./HomeClockWidgetOptions.vue")),
  },
  [HomeWidgetType.Storage]: {
    type: HomeWidgetType.Storage,
    category: "Files",
    label: "Storage",
    description: "Your usage and quota, with a breakdown of what takes space.",
    icon: "i-heroicons-archive-box",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeStorageWidget.vue")),
    optionsComponent: defineAsyncComponent(() => import("./HomeStorageWidgetOptions.vue")),
  },
  [HomeWidgetType.RecentFiles]: {
    type: HomeWidgetType.RecentFiles,
    category: "Files",
    label: "Recent files",
    description: "Files you recently created or updated.",
    icon: "i-heroicons-document-text",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeRecentFilesWidget.vue")),
    optionsComponent: defineAsyncComponent(() => import("./HomeRecentFilesWidgetOptions.vue")),
  },
  [HomeWidgetType.LibraryShortcuts]: {
    type: HomeWidgetType.LibraryShortcuts,
    category: "Files",
    label: "Library shortcuts",
    description: "Keep your file or media destinations within reach.",
    icon: "i-heroicons-book-open",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeLibraryWidget.vue")),
    optionsComponent: defineAsyncComponent(() => import("./HomeLibraryWidgetOptions.vue")),
  },
  [HomeWidgetType.QuickAccess]: {
    type: HomeWidgetType.QuickAccess,
    category: "Files",
    label: "Quick access",
    description: "Open your favorite folders in one click.",
    icon: "i-heroicons-folder",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeQuickAccessWidget.vue")),
    optionsComponent: defineAsyncComponent(() => import("./HomeQuickAccessWidgetOptions.vue")),
  },
  [HomeWidgetType.QuickUpload]: {
    type: HomeWidgetType.QuickUpload,
    category: "Files",
    label: "Quick upload",
    description: "Upload files, folders or ZIP archives to your library.",
    icon: "i-heroicons-arrow-up-tray",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeQuickUploadWidget.vue")),
    optionsComponent: defineAsyncComponent(() => import("./HomeQuickUploadWidgetOptions.vue")),
  },
  [HomeWidgetType.Player]: {
    type: HomeWidgetType.Player,
    category: "Media",
    label: "Player",
    description: "Control what is playing, right from your dashboard.",
    icon: "i-mdi-music-note-outline",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomePlayerWidget.vue")),
    optionsComponent: defineAsyncComponent(() => import("./HomePlayerWidgetOptions.vue")),
  },
  [HomeWidgetType.SystemStatus]: {
    type: HomeWidgetType.SystemStatus,
    category: "System",
    label: "System status",
    description: "Current service status and thirty days of incident history.",
    icon: "i-heroicons-check-circle",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeSystemStatusWidget.vue")),
    optionsComponent: defineAsyncComponent(() => import("./HomeSystemStatusWidgetOptions.vue")),
  },
  [HomeWidgetType.TopSongs]: {
    type: HomeWidgetType.TopSongs,
    category: "Media",
    label: "Top songs",
    description: "Your most played songs over the last seven days.",
    icon: "i-heroicons-musical-note",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeTopSongsWidget.vue")),
  },
  [HomeWidgetType.RecentActivity]: {
    type: HomeWidgetType.RecentActivity,
    category: "System",
    label: "Recent activity",
    description: "Recent actions on your files and account, together or grouped by day.",
    icon: "i-heroicons-clock",
    sizes,
    component: defineAsyncComponent(() => import("./widgets/HomeRecentActivityWidget.vue")),
    optionsComponent: defineAsyncComponent(() => import("./HomeRecentActivityWidgetOptions.vue")),
  },
};

export const homeWidgetDefinitions = Object.values(HOME_WIDGET_REGISTRY);
