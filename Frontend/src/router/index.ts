import { createRouter, createWebHistory } from "vue-router";
import { useAuthStore } from "@/stores/auth";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: "/",
      name: "index",
      component: () => import("@/views/IndexView.vue"),
      meta: { layout: "default", requiresAuth: false },
    },
    {
      path: "/auth",
      name: "auth",
      component: () => import("@/views/AuthView.vue"),
      meta: { layout: "default", requiresAuth: false, guestOnly: true },
    },
    {
      name: "dashboard",
      path: "/dashboard/:dirId?",
      component: () => import("@/views/dashboard/DashboardView.vue"),
      meta: { layout: "dashboard", requiresAuth: true },
      props: true,
    },
    {
      path: "/settings",
      name: "settings",
      component: () => import("@/views/dashboard/SettingsView.vue"),
      meta: { layout: "dashboard", requiresAuth: true },
    },
    {
      path: "/account",
      name: "account",
      component: () => import("@/views/dashboard/MyAccount.vue"),
      meta: { layout: "dashboard", requiresAuth: true },
    },
    {
      path: "/dashboard/tags",
      name: "tags",
      component: () => import("@/views/dashboard/TagsAndCategoriesView.vue"),
      meta: { layout: "dashboard", requiresAuth: true },
    },
    {
      path: "/access-history",
      name: "access-history",
      component: () => import("@/views/dashboard/AccessHistory.vue"),
      meta: { layout: "dashboard", requiresAuth: true },
    },
    {
      path: "/dashboard/trash",
      name: "trash",
      component: () => import("@/views/dashboard/DeletedItems.vue"),
      meta: { layout: "dashboard", requiresAuth: true },
    },
    {
      path: "/my-storage",
      name: "my-storage",
      component: () => import("@/views/dashboard/MyStorage.vue"),
      meta: { layout: "dashboard", requiresAuth: true },
    },
    {
      path: "/initial-greeting",
      name: "initial-greeting",
      component: () => import("@/views/onboarding/WelcomeView.vue"),
      meta: { layout: "default", requiresAuth: false },
    },
    {
      path: "/setup",
      name: "setup",
      component: () => import("@/views/onboarding/SetupView.vue"),
      meta: { layout: "default", requiresAuth: false },
    },
    {
      path: "/dashboard/admin",
      name: "admin-dashboard",
      component: () => import("@/views/dashboard/admin/AdminDashboard.vue"),
      meta: { layout: "dashboard", requiresAuth: true, requiresAdmin: true },
    },
    {
      path: "/dashboard/admin/user-registry",
      name: "user-registry",
      component: () => import("@/views/dashboard/admin/UserRegistry.vue"),
      meta: { layout: "dashboard", requiresAuth: true, requiresAdmin: true },
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
