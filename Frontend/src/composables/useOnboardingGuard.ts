import { useQuery } from "@pinia/colada";
import { watch } from "vue";
import { useRouter } from "vue-router";

import { OnboardingStep } from "@/enums";
import { getOnboardingStep } from "@/queries/user";
import { logger } from "@/utils/logger";

const STEP_ROUTES: Record<OnboardingStep, string> = {
  [OnboardingStep.SetPassword]: "set-password",
  [OnboardingStep.CompleteProfile]: "setup-profile",
  [OnboardingStep.Tour]: "tour",
  [OnboardingStep.Done]: "dashboard",
};

export const useOnboardingGuard = (requiredStep: OnboardingStep) => {
  const router = useRouter();
  const { data, isLoading, error } = useQuery(getOnboardingStep());

  watch(isLoading, (loading) => {
    if (loading || error.value || data.value === undefined || data.value === null) return;
    
    if (data.value !== requiredStep) {
      logger.log(data.value);
      router.replace({ name: STEP_ROUTES[data.value] });
    }
  });
};
