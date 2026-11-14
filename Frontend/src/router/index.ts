import { createRouter, createWebHistory } from "vue-router";

import { isStaleChunkError, reloadForStaleChunk } from "@/features/app-errors/stale-chunks";
import { getSignInDestination } from "@/features/home/landing";
import { useAuthStore } from "@/stores/auth";
import { logger } from "@/utils/logger";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      component: () => import("@/views/IndexView.vue"),
      meta: { layout: "default", requiresAuth: false, siteIcon: "home", title: "Alexandria" },
      name: "index",
      path: "/",
    },
    {
      component: () => import("@/views/AuthView.vue"),
      meta: {
        guestOnly: true,
        layout: "default",
        requiresAuth: false,
        siteIcon: "home",
        title: "Sign in",
      },
      name: "auth",
      path: "/auth",
    },
    {
      component: () => import("@/views/SharedLinkView.vue"),
      meta: { layout: "default", requiresAuth: false, siteIcon: "share", title: "Shared link" },
      name: "shared-link",
      path: "/share/:token",
    },
    {
      component: () => import("@/views/PublicStatusView.vue"),
      meta: { layout: "default", requiresAuth: false, siteIcon: "status", title: "Status" },
      name: "public-status",
      path: "/status",
    },
    {
      component: () => import("@/views/dashboard/DashboardView.vue"),
      meta: { layout: "dashboard", requiresAuth: true, siteIcon: "files", title: "Files" },
      name: "dashboard",
      path: "/dashboard/:dirId?",
      props: true,
    },
    {
      component: () => import("@/views/dashboard/HomeView.vue"),
      meta: { layout: "dashboard", requiresAuth: true, siteIcon: "home", title: "Home" },
      name: "home",
      path: "/home",
    },
    {
      component: () => import("@/views/dashboard/SettingsView.vue"),
      meta: { layout: "dashboard", requiresAuth: true, siteIcon: "settings", title: "Settings" },
      name: "settings",
      path: "/settings",
    },
    {
      component: () => import("@/views/dashboard/MyAccount.vue"),
      meta: { layout: "dashboard", requiresAuth: true, siteIcon: "account", title: "My account" },
      name: "account",
      path: "/account",
    },
    {
      component: () => import("@/views/dashboard/TagsAndCategoriesView.vue"),
      meta: { layout: "dashboard", requiresAuth: true, siteIcon: "tags", title: "Tags" },
      name: "tags",
      path: "/dashboard/tags",
    },
    {
      component: () => import("@/views/dashboard/AccessHistory.vue"),
      meta: {
        layout: "dashboard",
        requiresAuth: true,
        siteIcon: "history",
        title: "Access history",
      },
      name: "access-history",
      path: "/access-history",
    },
    {
      component: () => import("@/views/dashboard/DeletedItems.vue"),
      meta: { layout: "dashboard", requiresAuth: true, siteIcon: "trash", title: "Trash" },
      name: "trash",
      path: "/dashboard/trash",
    },
    {
      component: () => import("@/views/dashboard/MyStorage.vue"),
      meta: { layout: "dashboard", requiresAuth: true, siteIcon: "storage", title: "My storage" },
      name: "my-storage",
      path: "/my-storage",
    },
    {
      component: () => import("@/views/dashboard/admin/AdminDashboard.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: true,
        requiresAuth: true,
        siteIcon: "admin",
        title: "Admin",
      },
      name: "admin-dashboard",
      path: "/dashboard/admin",
    },
    {
      component: () => import("@/views/dashboard/admin/UserRegistry.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: true,
        requiresAuth: true,
        siteIcon: "admin",
        title: "User registry",
      },
      name: "user-registry",
      path: "/dashboard/admin/user-registry",
    },
    {
      component: () => import("@/views/dashboard/admin/AccessSecurity.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: true,
        requiresAuth: true,
        siteIcon: "admin",
        title: "Access & security",
      },
      name: "admin-activity",
      path: "/dashboard/admin/activity",
    },
    {
      component: () => import("@/views/dashboard/admin/ActivityChronicles.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: true,
        requiresAuth: true,
        siteIcon: "admin",
        title: "Full log",
      },
      name: "admin-activity-log",
      path: "/dashboard/admin/activity/log",
    },
    {
      component: () => import("@/views/dashboard/admin/StorageOverview.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: true,
        requiresAuth: true,
        siteIcon: "admin",
        title: "Storage overview",
      },
      name: "admin-storage",
      path: "/dashboard/admin/storage",
    },
    {
      component: () => import("@/views/dashboard/admin/ServiceStatus.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: true,
        requiresAuth: true,
        siteIcon: "admin",
        title: "Service status",
      },
      name: "service-status",
      path: "/dashboard/admin/service-status",
    },
    {
      component: () => import("@/views/dashboard/admin/IncidentHistory.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: true,
        requiresAuth: true,
        siteIcon: "admin",
        title: "Incident history",
      },
      name: "incident-history",
      path: "/dashboard/admin/incident-history",
    },
    {
      component: () => import("@/views/dashboard/admin/SystemConfiguration.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: true,
        requiresAuth: true,
        siteIcon: "admin",
        title: "System configuration",
      },
      name: "system-configuration",
      path: "/dashboard/admin/system-configuration",
    },
    {
      component: () => import("@/views/dashboard/admin/integrations/IntegrationsDashboard.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: true,
        requiresAuth: true,
        siteIcon: "admin",
        title: "Integrations",
      },
      name: "integrations-dashboard",
      path: "/dashboard/admin/integrations",
    },
    {
      component: () => import("@/views/dashboard/admin/integrations/AudioAnalysis.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: true,
        requiresAuth: true,
        siteIcon: "admin",
        title: "Audio analysis",
      },
      name: "audio-analysis",
      path: "/dashboard/admin/integrations/audio-analysis",
    },
    {
      component: () => import("@/views/dashboard/admin/integrations/Transpilation.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: true,
        requiresAuth: true,
        siteIcon: "admin",
        title: "Transcoding",
      },
      name: "transpilation-dashboard",
      path: "/dashboard/admin/integrations/transpilation",
    },
    {
      component: () => import("@/views/dashboard/admin/integrations/Playlist.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: true,
        requiresAuth: true,
        siteIcon: "admin",
        title: "Playlist generation",
      },
      name: "playlist-dashboard",
      path: "/dashboard/admin/integrations/playlist",
    },
    {
      component: () => import("@/views/dashboard/admin/integrations/Previews.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: true,
        requiresAuth: true,
        siteIcon: "admin",
        title: "Previews",
      },
      name: "previews-dashboard",
      path: "/dashboard/admin/integrations/previews",
    },
    {
      // A bookmarked link lands on the hub when lyrics fetching is turned off
      beforeEnter: () =>
        import.meta.env.VITE_LYRICS_ENABLED === "true" ? true : { name: "integrations-dashboard" },
      component: () => import("@/views/dashboard/admin/integrations/Lyrics.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: true,
        requiresAuth: true,
        siteIcon: "admin",
        title: "Lyrics",
      },
      name: "lyrics-dashboard",
      path: "/dashboard/admin/integrations/lyrics",
    },
    {
      component: () => import("@/views/onboarding/SetPasswordView.vue"),
      meta: {
        layout: "onboarding",
        requiresAdmin: false,
        requiresAuth: true,
        siteIcon: "home",
        title: "Set your password",
      },
      name: "set-password",
      path: "/onboarding/set-password",
    },
    {
      component: () => import("@/views/onboarding/SetupProfileView.vue"),
      meta: {
        layout: "onboarding",
        requiresAdmin: false,
        requiresAuth: true,
        siteIcon: "home",
        title: "Set up your profile",
      },
      name: "setup-profile",
      path: "/onboarding/setup-profile",
    },
    {
      component: () => import("@/views/onboarding/TourView.vue"),
      meta: {
        layout: "onboarding",
        requiresAdmin: false,
        requiresAuth: true,
        siteIcon: "home",
        title: "Welcome tour",
      },
      name: "tour",
      path: "/onboarding/tour",
    },
    {
      component: () => import("@/views/dashboard/GalleryView.vue"),
      meta: { layout: "dashboard", requiresAuth: true, siteIcon: "photos", title: "Photos" },
      name: "gallery",
      path: "/gallery",
    },
    {
      component: () => import("@/views/dashboard/streaming/MediaOverviewView.vue"),
      meta: { layout: "dashboard", requiresAuth: true, siteIcon: "music", title: "Media" },
      name: "media-overview",
      path: "/media",
    },
    {
      component: () => import("@/views/dashboard/streaming/MusicView.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: false,
        requiresAuth: true,
        siteIcon: "music",
        title: "Music",
      },
      name: "music",
      path: "/streaming/music",
    },
    {
      component: () => import("@/views/dashboard/streaming/StreamHistoryView.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: false,
        requiresAuth: true,
        siteIcon: "music",
        title: "Listening history",
      },
      name: "history",
      path: "/streaming/history",
    },
    {
      component: () => import("@/views/dashboard/streaming/TranspilationJobsView.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: false,
        requiresAuth: true,
        siteIcon: "jobs",
        title: "Transcoding jobs",
      },
      name: "transpilation-jobs",
      path: "/streaming/jobs",
    },
    {
      component: () => import("@/views/dashboard/streaming/PlaylistView.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: false,
        requiresAuth: true,
        siteIcon: "music",
        title: "Playlists",
      },
      name: "playlists",
      path: "/streaming/playlists",
    },
    {
      component: () => import("@/views/dashboard/streaming/PlaylistDetailsView.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: false,
        requiresAuth: true,
        siteIcon: "music",
        title: "Playlist",
      },
      name: "playlist-details",
      path: "/streaming/playlists/:id",
    },

    {
      component: () => import("@/views/dashboard/streaming/wrapped/StatsHubView.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: false,
        requiresAuth: true,
        siteIcon: "stats",
        title: "Stats",
      },
      name: "stats-hub",
      path: "/stats",
    },

    {
      meta: {
        layout: "dashboard",
        requiresAdmin: false,
        requiresAuth: true,
        siteIcon: "stats",
        title: "Timeline",
      },
      name: "timeline",
      path: "/stats/timeline",

      redirect: (to) => ({
        path: "/stats",
        query: { ...to.query, view: "timeline" },
        hash: to.hash,
      }),
    },

    {
      component: () => import("@/views/dashboard/streaming/wrapped/WrappedView.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: false,
        requiresAuth: true,
        siteIcon: "wrapped",
        title: "Wrapped",
      },
      name: "wrapped",
      path: "/stats/wrapped/:year?",
    },
    ...(import.meta.env.DEV
      ? [
          {
            component: () => import("@/views/dashboard/streaming/wrapped/WrappedGalleryView.vue"),
            meta: {
              layout: "dashboard" as const,
              requiresAdmin: false,
              requiresAuth: false,
              siteIcon: "wrapped" as const,
              title: "Wrapped gallery",
            },
            name: "wrapped-gallery",
            path: "/stats/wrapped-gallery",
          },
        ]
      : []),
    {
      component: () => import("@/views/dashboard/streaming/TrackDetailsView.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: false,
        requiresAuth: true,
        siteIcon: "music",
        title: "Track details",
      },
      name: "track-details",
      path: "/streaming/track/:fileId",
    },
    {
      component: () => import("@/views/dashboard/streaming/VideoView.vue"),
      meta: {
        layout: "dashboard",
        requiresAdmin: false,
        requiresAuth: true,
        siteIcon: "videos",
        title: "Videos",
      },
      name: "videos",
      path: "/streaming/videos",
    },
    {
      component: () => import("@/views/NotFoundView.vue"),
      meta: { layout: "default", requiresAuth: false, siteIcon: "home", title: "Page not found" },
      name: "not-found",
      path: "/:pathMatch(.*)*",
    },
  ],
});

// Tabs opened before a deploy still reference the old build's chunks, which the deploy removed.
router.onError((error, to) => {
  if (isStaleChunkError(error) && reloadForStaleChunk(router.resolve(to).href)) return;

  logger.error("Navigation failed", error);
});

// Navigation Guard
router.beforeEach(async (to) => {
  const authStore = useAuthStore();

  // Guest pages and the landing redirect a signed-in visitor, so they need a verified
  // session too; otherwise a fresh page load leaves that visitor on the sign-in page.
  // The not-found page waits as well so it opens in the right shell instead of swapping.
  const dependsOnSession =
    to.meta.requiresAuth ||
    to.meta.requiresAdmin ||
    to.meta.guestOnly ||
    to.name === "index" ||
    to.name === "not-found";

  if (dependsOnSession && authStore.isAuthenticated && !authStore.isSessionVerified) {
    await authStore.ensureSessionVerified();
  }

  const { isAdmin, isAuthenticated, isSessionVerified } = authStore;

  if (to.meta.requiresAuth && (!isAuthenticated || !isSessionVerified)) {
    return { path: "/auth", query: { redirect: to.fullPath } };
  } else if (isSessionVerified && (to.meta.guestOnly || to.name === "index")) {
    return getSignInDestination(router, authStore.user?.onboardingStep, to.query.redirect);
  } else if (to.meta.requiresAdmin && !isAdmin) {
    return { path: "/home" };
  }
});

export default router;
