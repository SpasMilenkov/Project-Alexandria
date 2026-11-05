import type { RouteRecordNameGeneric } from "vue-router";

export type SidebarSectionId = "library" | "media" | "admin";

export interface SidebarItem {
  label: string;
  icon: string;
  to: string;
  activeRoutes: string[];
}

export interface SidebarSection {
  id: SidebarSectionId;
  label: string;
  collapsible: boolean;
  items: SidebarItem[];
}

export interface SidebarOptions {
  isAdmin: boolean;
  streamingEnabled: boolean;
}

const LIBRARY_ITEMS: SidebarItem[] = [
  { label: "Home", icon: "i-lucide-house", to: "/home", activeRoutes: ["home"] },
  { label: "Files", icon: "i-lucide-folder", to: "/dashboard", activeRoutes: ["dashboard"] },
  { label: "Tags", icon: "i-lucide-tag", to: "/dashboard/tags", activeRoutes: ["tags"] },
  {
    label: "Access history",
    icon: "i-lucide-clock",
    to: "/access-history",
    activeRoutes: ["access-history"],
  },
  { label: "Trash", icon: "i-lucide-trash-2", to: "/dashboard/trash", activeRoutes: ["trash"] },
];

const MEDIA_ITEMS: SidebarItem[] = [
  {
    label: "Overview",
    icon: "i-lucide-circle-play",
    to: "/media",
    activeRoutes: ["media-overview"],
  },
  {
    label: "Music",
    icon: "i-lucide-music",
    to: "/streaming/music",
    activeRoutes: ["music", "track-details"],
  },
  { label: "Videos", icon: "i-lucide-video", to: "/streaming/videos", activeRoutes: ["videos"] },
  {
    label: "Playlists",
    icon: "i-lucide-list-music",
    to: "/streaming/playlists",
    activeRoutes: ["playlists", "playlist-details"],
  },
  {
    label: "Listening stats",
    icon: "i-lucide-chart-line",
    to: "/stats",
    activeRoutes: ["stats-hub", "timeline", "wrapped", "wrapped-gallery"],
  },
  {
    label: "Play history",
    icon: "i-lucide-history",
    to: "/streaming/history",
    activeRoutes: ["history"],
  },
  {
    label: "Conversions",
    icon: "i-lucide-layers",
    to: "/streaming/jobs",
    activeRoutes: ["transpilation-jobs"],
  },
];

const ADMIN_ITEMS: SidebarItem[] = [
  {
    label: "Overview",
    icon: "i-lucide-layout-dashboard",
    to: "/dashboard/admin",
    activeRoutes: ["admin-dashboard"],
  },
  {
    label: "Users",
    icon: "i-lucide-users",
    to: "/dashboard/admin/user-registry",
    activeRoutes: ["user-registry"],
  },
  {
    label: "Storage",
    icon: "i-lucide-archive",
    to: "/dashboard/admin/storage",
    activeRoutes: ["admin-storage"],
  },
  {
    label: "System vitals",
    icon: "i-lucide-activity",
    to: "/dashboard/admin/service-status",
    activeRoutes: ["service-status"],
  },
  {
    label: "Incidents",
    icon: "i-lucide-calendar-clock",
    to: "/dashboard/admin/incident-history",
    activeRoutes: ["incident-history"],
  },
  {
    label: "Integrations",
    icon: "i-lucide-plug",
    to: "/dashboard/admin/integrations",
    activeRoutes: [
      "integrations-dashboard",
      "audio-analysis",
      "transpilation-dashboard",
      "playlist-dashboard",
      "previews-dashboard",
      "lyrics-dashboard",
    ],
  },
  {
    label: "Configuration",
    icon: "i-lucide-sliders-horizontal",
    to: "/dashboard/admin/system-configuration",
    activeRoutes: ["system-configuration"],
  },
];

export const buildSidebarSections = ({ isAdmin, streamingEnabled }: SidebarOptions) => {
  const sections: SidebarSection[] = [
    { id: "library", label: "Library", collapsible: false, items: LIBRARY_ITEMS },
  ];

  if (streamingEnabled) {
    sections.push({ id: "media", label: "Media", collapsible: true, items: MEDIA_ITEMS });
  }

  if (isAdmin) {
    sections.push({ id: "admin", label: "Admin", collapsible: true, items: ADMIN_ITEMS });
  }

  return sections;
};

export const isSidebarItemActive = (item: SidebarItem, routeName: RouteRecordNameGeneric) => {
  if (typeof routeName !== "string") return false;

  return item.activeRoutes.includes(routeName);
};
