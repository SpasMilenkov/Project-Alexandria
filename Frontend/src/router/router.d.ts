import "vue-router";

declare module "vue-router" {
  interface RouteMeta {
    layout?: "default" | "dashboard" | "onboarding";
  }
}
