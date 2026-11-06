import "vue-router";
import type { SiteIconPlace } from "@/features/site-icon/places";

declare module "vue-router" {
  interface RouteMeta {
    layout?: "default" | "dashboard" | "onboarding";
    siteIcon?: SiteIconPlace;
    title?: string;
  }
}
