import { useQuery } from "@pinia/colada";
import { watch } from "vue";
import { useRoute, useRouter } from "vue-router";

import type { OnboardingStep } from "@/enums";

import { getOnboardingStep } from "@/queries/user";
import { getSignInDestination } from "@/utils/home-landing";

export const useOnboardingGuard = (requiredStep: OnboardingStep) => {
  const router = useRouter();
  const route = useRoute();
  const { data, isLoading, error } = useQuery(getOnboardingStep());

  watch(
    [data, isLoading, error],
    ([step, loading, queryError]) => {
      if (loading || queryError || step === undefined || step === null || step === requiredStep)
        return;

      void router.replace(getSignInDestination(router, step, route.query.redirect));
    },
    { immediate: true },
  );
};
