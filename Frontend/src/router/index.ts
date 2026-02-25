import { createRouter, createWebHistory } from "vue-router";
import { useAuthStore } from "@/stores/auth";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      component: () => import("@/views/IndexView.vue"),
      meta: { layout: "default", requiresAuth: false },
      name: "index",
      path: "/",
    },
    {
      component: () => import("@/views/AuthView.vue"),
      meta: { guestOnly: true, layout: "default", requiresAuth: false },
      name: "auth",
      path: "/auth",
    },
    {
      component: () => import("@/views/dashboard/DashboardView.vue"),
      meta: { layout: "dashboard", requiresAuth: true },
      name: "dashboard",
      path: "/dashboard/:dirId?",
      props: true,
    },
    {
      component: () => import("@/views/dashboard/SettingsView.vue"),
      meta: { layout: "dashboard", requiresAuth: true },
      name: "settings",
      path: "/settings",
    },
    {
      component: () => import("@/views/dashboard/MyAccount.vue"),
      meta: { layout: "dashboard", requiresAuth: true },
      name: "account",
      path: "/account",
    },
    {
      component: () => import("@/views/dashboard/TagsAndCategoriesView.vue"),
      meta: { layout: "dashboard", requiresAuth: true },
      name: "tags",
      path: "/dashboard/tags",
    },
    {
      component: () => import("@/views/dashboard/AccessHistory.vue"),
      meta: { layout: "dashboard", requiresAuth: true },
      name: "access-history",
      path: "/access-history",
    },
    {
      component: () => import("@/views/dashboard/DeletedItems.vue"),
      meta: { layout: "dashboard", requiresAuth: true },
      name: "trash",
      path: "/dashboard/trash",
    },
    {
      component: () => import("@/views/dashboard/MyStorage.vue"),
      meta: { layout: "dashboard", requiresAuth: true },
      name: "my-storage",
      path: "/my-storage",
    },
    {
      component: () => import("@/views/onboarding/WelcomeView.vue"),
      meta: { layout: "default", requiresAuth: false },
      name: "initial-greeting",
      path: "/initial-greeting",
    },
    {
      component: () => import("@/views/onboarding/SetupView.vue"),
      meta: { layout: "default", requiresAuth: false },
      name: "setup",
      path: "/setup",
    },
    {
      component: () => import("@/views/dashboard/admin/AdminDashboard.vue"),
      meta: { layout: "dashboard", requiresAdmin: true, requiresAuth: true },
      name: "admin-dashboard",
      path: "/dashboard/admin",
    },
    {
      component: () => import("@/views/dashboard/admin/UserRegistry.vue"),
      meta: { layout: "dashboard", requiresAdmin: true, requiresAuth: true },
      name: "user-registry",
      path: "/dashboard/admin/user-registry",
    },
    {
      component: () => import("@/views/dashboard/admin/ServiceStatus.vue"),
      meta: { layout: "dashboard", requiresAdmin: true, requiresAuth: true },
      name: "service-status",
      path: "/dashboard/admin/service-status",
    },
    {
      component: () =>
        import("@/views/dashboard/admin/SystemConfiguration.vue"),
      meta: { layout: "dashboard", requiresAdmin: true, requiresAuth: true },
      name: "system-configuration",
      path: "/dashboard/admin/system-configuration",
    },
  ],
});
// Navigation Guard

router.beforeEach((to, from, next) => {
  const authStore = useAuthStore();
  const { isAuthenticated, isAdmin } = authStore;

  if (to.meta.requiresAuth && !isAuthenticated) {
    next({ path: "/auth", query: { redirect: to.fullPath } });
  } else if (to.meta.guestOnly && isAuthenticated) {
    next((to.query.redirect as string) || "/dashboard");
  } else if (to.meta.requiresAdmin && !isAdmin) {
    next({ path: "/dashboard" });
  } else {
    next();
  }
});

export default router;
