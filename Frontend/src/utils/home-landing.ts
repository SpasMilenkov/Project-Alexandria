import type { RouteLocationRaw, Router } from "vue-router";

import { OnboardingStep } from "@/enums/OnboardingStep";

const onboardingPaths: Partial<Record<OnboardingStep, string>> = {
  [OnboardingStep.SetPassword]: "/onboarding/set-password",
  [OnboardingStep.CompleteProfile]: "/onboarding/setup-profile",
  [OnboardingStep.Tour]: "/onboarding/tour",
};

export const getHomeLandingDestination = (router: Router, redirect: unknown): string => {
  if (
    typeof redirect !== "string" ||
    !redirect.startsWith("/") ||
    redirect.startsWith("//") ||
    redirect.includes("\\")
  ) {
    return "/home";
  }

  const destination = router.resolve(redirect);

  if (
    destination.matched.length === 0 ||
    destination.meta.guestOnly ||
    destination.meta.layout === "onboarding" ||
    destination.name === "index"
  ) {
    return "/home";
  }

  return destination.fullPath;
};

export const getSignInDestination = (
  router: Router,
  step: OnboardingStep | undefined,
  redirect: unknown,
): RouteLocationRaw => {
  const destination = getHomeLandingDestination(router, redirect);
  const onboardingPath = step === undefined ? undefined : onboardingPaths[step];

  if (onboardingPath) {
    return { path: onboardingPath, query: { redirect: destination } };
  }

  return destination;
};
