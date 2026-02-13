import AuthView from "@/views/AuthView.vue";
import Dashboard from "@/views/dashboard/DashboardView.vue";
import SetupView from "@/views/onboarding/SetupView.vue";
import WelcomeView from "@/views/onboarding/WelcomeView.vue";
import SettingsView from "@/views/dashboard/AppearanceSettingsView.vue";
import { createRouter, createWebHistory } from "vue-router";
import AccessHistory from "@/views/dashboard/AccessHistory.vue";
import TagsAndCategoriesView from "@/views/dashboard/TagsAndCategoriesView.vue";
import MyAccount from "@/views/dashboard/MyAccount.vue";
import IndexView from "@/views/IndexView.vue";
import { useAuthStore } from "@/stores/auth";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: "/",
      name: "index",
      component: IndexView,
      meta: { layout: "default", requiresAuth: false },
    },
    {
      path: "/auth",
      name: "auth",
      component: AuthView,
      meta: { layout: "default", requiresAuth: false, guestOnly: true },
    },
    {
      name: "dashboard",
      path: "/dashboard/:dirId?",
      component: Dashboard,
      meta: { layout: "dashboard", requiresAuth: true },
      props: true,
    },
    {
      name: "appearance",
      path: "/appearance",
      meta: { layout: "dashboard", requiresAuth: true },
      component: SettingsView,
    },
    {
      path: "/initial-greeting",
      name: "initial-greeting",
      component: WelcomeView,
      meta: { layout: "default", requiresAuth: false },
    },
    {
      path: "/setup",
      name: "setup",
      component: SetupView,
      meta: { layout: "default", requiresAuth: false },
    },
    {
      path: "/settings/appearance",
      name: "settings-appearance",
      meta: { layout: "dashboard", requiresAuth: true },
      component: SettingsView,
    },
    {
      path: "/account",
      name: "account",
      meta: { layout: "dashboard", requiresAuth: true },
      component: MyAccount,
    },
    {
      path: "/dashboard/tags",
      name: "tags",
      meta: { layout: "dashboard", requiresAuth: true },
      component: TagsAndCategoriesView,
    },
    {
      path: "/access-history",
      name: "access-history",
      meta: { layout: "dashboard", requiresAuth: true },
      component: AccessHistory,
    },
  ],
});

// Navigation Guard
router.beforeEach((to, from, next) => {
  const authStore = useAuthStore();
  const requiresAuth = to.meta.requiresAuth;
  const guestOnly = to.meta.guestOnly;
  const isAuthenticated = authStore.isAuthenticated;

  if (requiresAuth && !isAuthenticated) {
    next({
      path: "/auth",
      query: { redirect: to.fullPath }, // Save the intended destination
    });
  }
  else if (guestOnly && isAuthenticated) {
    const redirectPath = to.query.redirect as string;
    next(redirectPath || "/dashboard");
  }
  else {
    next();
  }
});

export default router;
