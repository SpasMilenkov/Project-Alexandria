import AuthView from "@/views/AuthView.vue";
import Dashboard from "@/views/dashboard/DashboardView.vue";
import SetupView from "@/views/onboarding/SetupView.vue";
import WelcomeView from "@/views/onboarding/WelcomeView.vue";
import SettingsView from "@/views/dashboard/AppearanceSettingsView.vue";
import { createRouter, createWebHistory } from "vue-router";
import AccessHistory from "@/views/dashboard/AccessHistory.vue";
import TagsAndCategoriesView from "@/views/dashboard/TagsAndCategoriesView.vue";
import MyAccount from "@/views/dashboard/MyAccount.vue";

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes: [
    {
      path: "/auth",
      component: AuthView,
      meta: { layout: "default" },
    },
    {
      name: "dashboard",
      path: "/dashboard/:dirId?",
      component: Dashboard,
      meta: { layout: "dashboard" },
      props: true,
    },
    {
      name: "/appearance",
      path: "/appearance",
      meta: { layout: "dashboard" },
      component: SettingsView,
    },
    {
      path: "/initial-greeting",
      component: WelcomeView,
      meta: { layout: "default" },
    },
    {
      path: "/setup",
      component: SetupView,
      meta: { layout: "default" },
    },
    {
      path: "/settings/appearance",
      meta: { layout: "dashboard" },
      component: SettingsView,
    },
    {
      path: "/account",
      meta: { layout: "dashboard" },
      component: MyAccount,
    },
    {
      path: "/dashboard/tags",
      meta: { layout: "dashboard" },
      component: TagsAndCategoriesView,
    },
    {
      path: "/access-history",
      meta: { layout: "dashboard" },
      component: AccessHistory,
    },
  ],
});

export default router;
