import type { HomeLibraryOptions, HomeLibraryShortcut } from "@/types/home";

import { HomeLibraryDestination, HomeLibraryStyle, HomeShortcutGroup } from "@/enums/home-widget";

export interface HomeLibraryLink {
  destination: HomeLibraryDestination;
  label: string;
  description: string;
  to: string;
  icon: string;
  media: boolean;
}

export const HOME_LIBRARY_STYLES = [
  {
    value: HomeLibraryStyle.Launchpad,
    label: "Launchpad",
    description: "Roomy tiles that keep your library within reach.",
  },
  {
    value: HomeLibraryStyle.Directory,
    label: "Directory",
    description: "Quiet navigation rows with room for the details.",
  },
  {
    value: HomeLibraryStyle.Spotlight,
    label: "Spotlight",
    description: "A featured destination with your other shortcuts alongside it.",
  },
  {
    value: HomeLibraryStyle.Icons,
    label: "Icons",
    description: "Small square icons with a destination tooltip on hover or focus.",
  },
];

export const HOME_LIBRARY_DESTINATIONS: HomeLibraryLink[] = [
  {
    destination: HomeLibraryDestination.FileExplorer,
    label: "File Explorer",
    description: "Browse your files and folders",
    to: "/dashboard",
    icon: "i-heroicons-folder",
    media: false,
  },
  {
    destination: HomeLibraryDestination.Tags,
    label: "Tags and Categories",
    description: "Find a collection by its tags",
    to: "/dashboard/tags",
    icon: "i-heroicons-tag",
    media: false,
  },
  {
    destination: HomeLibraryDestination.AccessHistory,
    label: "Access History",
    description: "See your recent file activity",
    to: "/access-history",
    icon: "i-heroicons-clock",
    media: false,
  },
  {
    destination: HomeLibraryDestination.Storage,
    label: "Your Storage",
    description: "Review your usage and quota",
    to: "/my-storage",
    icon: "i-heroicons-archive-box",
    media: false,
  },
  {
    destination: HomeLibraryDestination.Trash,
    label: "Trash",
    description: "Restore or remove deleted files",
    to: "/dashboard/trash",
    icon: "i-heroicons-trash",
    media: false,
  },
  {
    destination: HomeLibraryDestination.MediaOverview,
    label: "Media Overview",
    description: "Explore your media library",
    to: "/media",
    icon: "i-heroicons-film",
    media: true,
  },
  {
    destination: HomeLibraryDestination.Music,
    label: "Music",
    description: "Listen to your music library",
    to: "/streaming/music",
    icon: "i-heroicons-musical-note",
    media: true,
  },
  {
    destination: HomeLibraryDestination.Videos,
    label: "Videos",
    description: "Watch your video library",
    to: "/streaming/videos",
    icon: "i-heroicons-film",
    media: true,
  },
  {
    destination: HomeLibraryDestination.Playlists,
    label: "Playlists",
    description: "Explore your collections",
    to: "/streaming/playlists",
    icon: "i-heroicons-list-bullet",
    media: true,
  },
  {
    destination: HomeLibraryDestination.PlaybackHistory,
    label: "Playback History",
    description: "Return to something you played",
    to: "/streaming/history",
    icon: "i-heroicons-clock",
    media: true,
  },
  {
    destination: HomeLibraryDestination.EncodingJobs,
    label: "Encoding Jobs",
    description: "Follow your media conversions",
    to: "/streaming/jobs",
    icon: "i-heroicons-arrow-path",
    media: true,
  },
  {
    destination: HomeLibraryDestination.YourListening,
    label: "Your listening",
    description: "Timeline, recaps and Wrapped",
    to: "/stats",
    icon: "i-heroicons-chart-bar",
    media: true,
  },
  {
    destination: HomeLibraryDestination.Timeline,
    label: "Timeline",
    description: "Browse your daily listening history",
    to: "/stats#timeline",
    icon: "i-heroicons-calendar",
    media: true,
  },
  {
    destination: HomeLibraryDestination.Recaps,
    label: "Recaps",
    description: "Revisit your listening highlights",
    to: "/stats#recaps",
    icon: "i-heroicons-chart-bar",
    media: true,
  },
];

const presets: Partial<Record<HomeShortcutGroup, HomeLibraryDestination[]>> = {
  [HomeShortcutGroup.Files]: [
    HomeLibraryDestination.FileExplorer,
    HomeLibraryDestination.Tags,
    HomeLibraryDestination.AccessHistory,
    HomeLibraryDestination.Storage,
  ],
  [HomeShortcutGroup.Media]: [
    HomeLibraryDestination.Music,
    HomeLibraryDestination.Videos,
    HomeLibraryDestination.Playlists,
    HomeLibraryDestination.YourListening,
  ],
  [HomeShortcutGroup.Mixed]: [
    HomeLibraryDestination.FileExplorer,
    HomeLibraryDestination.Music,
    HomeLibraryDestination.Tags,
    HomeLibraryDestination.Playlists,
  ],
};

export const homeLibrarySelection = (options: HomeLibraryOptions): HomeLibraryShortcut[] => {
  if (options.shortcutGroup === HomeShortcutGroup.Custom)
    return options.shortcuts.map((shortcut) => ({ ...shortcut }));

  return (presets[options.shortcutGroup] ?? []).map((destination) => ({
    destination,
    label: null,
  }));
};

export const homeLibraryLinks = (options: HomeLibraryOptions, streamingEnabled: boolean) =>
  homeLibrarySelection(options).flatMap((shortcut): HomeLibraryLink[] => {
    const target = HOME_LIBRARY_DESTINATIONS.find(
      (item) => item.destination === shortcut.destination,
    );

    if (!target || (target.media && !streamingEnabled)) return [];

    return [{ ...target, label: shortcut.label?.trim() || target.label }];
  });

export const homeLibraryCustomChange = (
  options: HomeLibraryOptions,
  shortcuts: HomeLibraryShortcut[],
): Partial<HomeLibraryOptions> => ({
  shortcutGroup: HomeShortcutGroup.Custom,
  shortcuts: shortcuts.map((shortcut) => ({ ...shortcut })),
  featuredDestination: homeLibraryFeature(options.featuredDestination, shortcuts),
});

const homeLibraryFeature = (
  featured: HomeLibraryDestination | null,
  shortcuts: HomeLibraryShortcut[],
) => {
  if (shortcuts.some((shortcut) => shortcut.destination === featured)) return featured;

  return null;
};

export const homeLibraryPresetChange = (
  options: HomeLibraryOptions,
  shortcutGroup: HomeShortcutGroup,
): Partial<HomeLibraryOptions> => {
  const shortcuts = homeLibrarySelection(options);

  if (shortcutGroup === HomeShortcutGroup.Custom)
    return homeLibraryCustomChange(options, shortcuts);

  const selection = homeLibrarySelection({ ...options, shortcutGroup });

  return {
    shortcutGroup,
    shortcuts: [],
    featuredDestination: homeLibraryFeature(options.featuredDestination, selection),
  };
};

export const homeLibraryDisplayOrder = (
  links: HomeLibraryLink[],
  style: HomeLibraryStyle,
  featured: HomeLibraryDestination | null,
) => {
  if (style !== HomeLibraryStyle.Spotlight || featured === null) return links;

  const target = links.find((link) => link.destination === featured);

  if (!target) return links;

  return [target, ...links.filter((link) => link.destination !== featured)];
};
