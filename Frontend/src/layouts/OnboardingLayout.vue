<template>
  <OfflineBanner />

  <div class="flex min-h-screen">
    <!-- LEFT PANEL ( only) -->
    <aside
      class="relative hidden w-72 shrink-0 flex-col border-r border-gray-200/60 bg-gray-50/60 dark:border-gray-800/60 dark:bg-gray-900/40 lg:flex xl:w-80"
    >
      <div class="flex flex-1 flex-col px-8 py-10">
        <!-- App identity -->
        <div class="mb-10 flex items-center gap-2.5">
          <div
            class="flex h-8 w-8 items-center justify-center rounded-lg bg-primary/20 ring-1 ring-primary/30"
          >
            <Icon icon="mdi:book-open-variant" class="h-4 w-4 text-primary" />
          </div>
          <span class="text-sm font-semibold tracking-tight text-gray-900 dark:text-gray-100">
            Alexandria
          </span>
        </div>

        <!-- Vertical stepper -->
        <nav class="flex flex-1 flex-col" aria-label="Setup progress">
          <div class="space-y-1">
            <template v-for="(step, i) in ONBOARDING_STEPS" :key="step.key">
              <div
                :class="[
                  'group flex items-center gap-3 rounded-xl px-3 py-3 transition-all duration-200',
                  i === currentStep
                    ? 'bg-primary/10'
                    : 'hover:bg-gray-200/40 dark:hover:bg-white/5',
                ]"
                :aria-current="i === currentStep ? 'step' : undefined"
              >
                <div
                  :class="[
                    'flex h-8 w-8 shrink-0 items-center justify-center rounded-full border text-xs font-bold transition-all duration-300',
                    i < currentStep
                      ? 'border-primary bg-primary text-white shadow-sm shadow-primary/30'
                      : i === currentStep
                        ? 'border-primary bg-primary/15 text-primary'
                        : 'border-gray-300/60 bg-transparent text-gray-400 dark:border-gray-600/60 dark:text-gray-500',
                  ]"
                >
                  <Icon v-if="i < currentStep" icon="mdi:check" class="h-3.5 w-3.5" />
                  <span v-else>{{ i + 1 }}</span>
                </div>

                <div>
                  <p
                    :class="[
                      'text-sm font-medium leading-tight transition-colors duration-200',
                      i === currentStep
                        ? 'text-gray-900 dark:text-gray-100'
                        : i < currentStep
                          ? 'text-gray-500 dark:text-gray-400'
                          : 'text-gray-400 dark:text-gray-500',
                    ]"
                  >
                    {{ step.label }}
                  </p>
                  <p
                    :class="[
                      'mt-0.5 text-xs transition-colors duration-200',
                      i === currentStep
                        ? 'text-gray-500 dark:text-gray-400'
                        : 'text-gray-400/60 dark:text-gray-600',
                    ]"
                  >
                    {{ step.description }}
                  </p>
                </div>
              </div>

              <div v-if="i < ONBOARDING_STEPS.length - 1" class="ml-7 flex h-5 w-px items-stretch">
                <div
                  :class="[
                    'mx-auto w-px rounded-full transition-all duration-500',
                    i < currentStep ? 'bg-primary/50' : 'bg-gray-300/50 dark:bg-gray-700/50',
                  ]"
                />
              </div>
            </template>
          </div>

          <div class="mt-auto flex items-center justify-between pt-10">
            <p class="text-xs dark:text-gray-400">
              Step {{ currentStep + 1 }} of {{ ONBOARDING_STEPS.length }}
            </p>
            <UColorModeButton size="sm" variant="ghost" color="neutral" />
          </div>
        </nav>
      </div>
    </aside>

    <!-- RIGHT PANEL -->
    <main class="flex flex-1 flex-col">
      <div
        class="relative flex items-center border-b border-gray-200/50 px-4 py-3 dark:border-gray-800/50 lg:hidden"
        aria-label="Setup progress"
      >
        <div class="flex flex-1 items-center justify-center gap-2">
          <template v-for="(step, i) in ONBOARDING_STEPS" :key="step.key">
            <div class="flex items-center gap-1.5">
              <div
                :class="[
                  'flex h-7 w-7 shrink-0 items-center justify-center rounded-full border text-xs font-semibold transition-all duration-300',
                  i < currentStep
                    ? 'border-primary bg-primary text-white'
                    : i === currentStep
                      ? 'border-primary bg-primary/10 text-primary'
                      : 'border-gray-300/60 bg-transparent text-gray-400 dark:border-gray-600/60 dark:text-gray-500',
                ]"
              >
                <Icon v-if="i < currentStep" icon="mdi:check" class="h-3 w-3" />
                <span v-else>{{ i + 1 }}</span>
              </div>
              <span
                :class="[
                  'hidden text-xs font-medium transition-colors sm:block',
                  i === currentStep
                    ? 'text-gray-800 dark:text-gray-200'
                    : 'text-gray-400 dark:text-gray-500',
                ]"
                >{{ step.label }}</span
              >
            </div>

            <div
              v-if="i < ONBOARDING_STEPS.length - 1"
              :class="[
                'h-px w-6 rounded-full transition-all duration-500 sm:w-8',
                i < currentStep ? 'bg-primary/60' : 'bg-gray-300/40 dark:bg-gray-700/40',
              ]"
            />
          </template>
        </div>

        <UColorModeButton size="sm" variant="ghost" color="neutral" class="absolute right-4" />
      </div>

      <!-- Content -->
      <div class="flex flex-1 flex-col px-2 md:px-8 py-10 lg:px-12">
        <slot />
      </div>
    </main>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute } from "vue-router";
import { Icon } from "@iconify/vue";
import OfflineBanner from "@/components/common/OfflineBanner.vue";

const ONBOARDING_STEPS = [
  {
    description: "Replace your temporary password",
    key: "change-password",
    label: "Set password",
  },
  {
    description: "Personalise your account",
    key: "setup-profile",
    label: "Your profile",
  },
  {
    description: "See what's available to you",
    key: "tour",
    label: "Quick tour",
  },
] as const;

const route = useRoute();

const STEP_BY_ROUTE: Record<string, number> = {
  "change-password": 0,
  "setup-profile": 1,
  tour: 2,
};

const currentStep = computed(() => {
  if (typeof route.meta.onboardingStep === "number") {
    return route.meta.onboardingStep;
  }
  const name = String(route.name ?? "");
  return STEP_BY_ROUTE[name] ?? 0;
});
</script>
