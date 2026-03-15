<template>
  <div class="flex flex-1 items-center justify-center">
    <!--
      Two-pane carousel.
      - Left:  feature content (icon, title, description, tips, nav)
      - Right: app preview screenshot
      On mobile they stack: preview on top, content below.
    -->
    <div class="w-full max-w-4xl">
      <!-- Progress dots -->
      <div class="mb-8 flex items-center justify-center gap-2">
        <button
          v-for="(f, i) in tourFeatures"
          :key="i"
          :aria-label="`Go to ${f.title}`"
          class="rounded-full focus:outline-none transition-all duration-300"
          :class="
            i === current
              ? 'w-6 h-2 bg-primary'
              : i < current
                ? 'w-2 h-2 bg-primary/40'
                : 'w-2 h-2 bg-gray-300/50 dark:bg-gray-700/50'
          "
          @click="goTo(i)"
        />
      </div>

      <!-- Two-pane card -->
      <div
        class="overflow-hidden rounded-2xl border border-gray-200/60 bg-white/50 shadow-lg shadow-black/5 backdrop-blur-sm dark:border-gray-700/50 dark:bg-white/4"
      >
        <div class="flex flex-col lg:flex-row lg:min-h-105">
          <!-- LEFT: content pane -->
          <div
            class="flex flex-col justify-between p-8 lg:w-[42%] lg:border-r lg:border-gray-200/60 lg:dark:border-gray-700/50"
          >
            <Transition :name="transitionName" mode="out-in">
              <div :key="current" class="flex flex-col gap-5">
                <!-- Icon -->
                <div
                  :class="[
                    'flex h-12 w-12 items-center justify-center rounded-xl transition-colors duration-300',
                    tourFeatures[current].highlight
                      ? 'bg-primary/15 text-primary'
                      : 'bg-gray-100/80 text-gray-500 dark:bg-white/10 dark:text-gray-400',
                  ]"
                >
                  <Icon :icon="tourFeatures[current].icon" class="h-6 w-6" />
                </div>

                <!-- Title + description -->
                <div>
                  <h2 class="text-xl font-semibold tracking-tight text-gray-900 dark:text-gray-100">
                    {{ tourFeatures[current].title }}
                  </h2>
                  <p class="mt-2 text-sm leading-relaxed text-gray-500 dark:text-gray-400">
                    {{ tourFeatures[current].description }}
                  </p>
                </div>

                <!-- Tips -->
                <ul class="space-y-2.5">
                  <li
                    v-for="(tip, i) in tourFeatures[current].tips"
                    :key="tip"
                    class="flex items-start gap-3 text-sm text-gray-600 dark:text-gray-300"
                  >
                    <div
                      :class="[
                        'mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center rounded-full text-[10px] font-bold',
                        tourFeatures[current].highlight
                          ? 'bg-primary/15 text-primary'
                          : 'bg-gray-100 text-gray-500 dark:bg-white/10 dark:text-gray-400',
                      ]"
                    >
                      {{ i + 1 }}
                    </div>
                    {{ tip }}
                  </li>
                </ul>
              </div>
            </Transition>

            <!-- Navigation — always at the bottom of content pane -->
            <div class="mt-8 flex items-center gap-3">
              <UButton
                v-if="current > 0"
                color="neutral"
                variant="outline"
                size="lg"
                leading-icon="i-mdi-arrow-left"
                @click="prev"
              >
                Back
              </UButton>

              <UButton
                color="primary"
                variant="solid"
                size="lg"
                class="flex-1"
                :trailing-icon="isLast ? 'i-mdi-rocket-launch-outline' : 'i-mdi-arrow-right'"
                @click="isLast ? handleFinish() : next()"
              >
                {{ isLast ? "Get started" : "Next" }}
              </UButton>
            </div>
          </div>

          <!-- RIGHT: preview pane -->
          <div
            :class="[
              'relative flex items-center justify-center overflow-hidden lg:flex-1',
              isDark ? 'bg-gray-950/60' : 'bg-gray-200/60',
            ]"
          >
            <!-- Grid texture — flips colour with theme -->
            <div
              class="pointer-events-none absolute inset-0 opacity-[0.06]"
              :style="{
                backgroundImage: `linear-gradient(${isDark ? '#fff' : '#000'} 1px, transparent 1px), linear-gradient(90deg, ${isDark ? '#fff' : '#000'} 1px, transparent 1px)`,
                backgroundSize: '24px 24px',
              }"
            />

            <Transition :name="transitionName" mode="out-in">
              <div :key="current" class="relative w-full p-6 lg:p-8">
                <img
                  :src="imageUrl"
                  :alt="`${tourFeatures[current].title} preview`"
                  class="w-full rounded-lg shadow-2xl ring-1"
                  :class="
                    isDark ? 'shadow-black/40 ring-white/10' : 'shadow-black/15 ring-black/10'
                  "
                  style="aspect-ratio: 520/340; object-fit: cover"
                  loading="lazy"
                  decoding="async"
                />
              </div>
            </Transition>
          </div>
        </div>
      </div>

      <!-- Server error -->
      <Transition
        enter-active-class="transition-all duration-200 ease-out"
        enter-from-class="opacity-0 -translate-y-1"
        leave-active-class="transition-all duration-150 ease-in"
        leave-to-class="opacity-0"
      >
        <div
          v-if="serverError"
          class="mt-4 flex items-start gap-3 rounded-xl border border-red-200/70 bg-red-50/70 px-4 py-3.5 dark:border-red-900/50 dark:bg-red-950/30"
        >
          <Icon icon="mdi:alert-circle-outline" class="mt-0.5 h-4 w-4 shrink-0 text-red-500" />
          <p class="text-sm text-red-600 dark:text-red-400">{{ serverError }}</p>
        </div>
      </Transition>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { useRouter } from "vue-router";
import { useDark } from "@vueuse/core";
import { Icon } from "@iconify/vue";
import { finishTour } from "@/mutations/user";
import { useOnboardingGuard } from "@/composables/useOnboardingGuard";
import { OnboardingStep } from "@/enums";

useOnboardingGuard(OnboardingStep.Tour);

const router = useRouter();
const isDark = useDark();

const { mutate, error: mutationError, status } = finishTour();

watch(status, (s) => {
  if (s === "success") router.push({ name: "dashboard" });
});

const serverError = computed(() => {
  if (!mutationError.value) return null;
  return mutationError.value instanceof Error
    ? mutationError.value.message
    : "Something went wrong. Please try again.";
});

// Carousel
const current = ref(0);
const direction = ref<"forward" | "back">("forward");
const transitionName = computed(() =>
  direction.value === "forward" ? "slide-left" : "slide-right",
);
const isLast = computed(() => current.value === tourFeatures.length - 1);

// Resolves the correct image from /public/images/onboarding/{dark|light}/
const imageUrl = computed(() => {
  const theme = isDark.value ? "dark" : "light";
  return `/images/onboarding/${theme}/${tourFeatures[current.value].image}`;
});

const next = () => {
  if (!isLast.value) {
    direction.value = "forward";
    current.value++;
  }
};
const prev = () => {
  if (current.value > 0) {
    direction.value = "back";
    current.value--;
  }
};
const goTo = (i: number) => {
  direction.value = i > current.value ? "forward" : "back";
  current.value = i;
};
const handleFinish = () => {
  mutate();
};

// Features
// `image` is the filename only — path is resolved by imageUrl computed
// as /public/images/onboarding/{dark|light}/{image}
interface TourFeature {
  id: string;
  icon: string;
  image: string;
  title: string;
  description: string;
  tips: string[];
  badge?: string;
  highlight?: boolean;
}

const tourFeatures: TourFeature[] = [
  {
    description:
      "Your central hub for every document, image, and file in Alexandria. Upload in bulk, create folders, and keep everything neatly organised.",
    highlight: true,
    icon: "mdi:folder-open-outline",
    id: "file-management",
    image: "explorer-preview.svg",
    tips: [
      "Drag and drop files directly onto the explorer to upload",
      "Right-click any file for a quick-action menu",
      "Use the search bar to find files by name across all folders",
    ],
    title: "File Explorer",
  },
  {
    description:
      "A flexible labelling system that works across folders. Tag a file once and find it from any view — no need to move or duplicate it.",
    icon: "mdi:tag-multiple-outline",
    id: "tags",
    image: "tags-preview.svg",
    tips: [
      "Create colour-coded tags to visually distinguish file types",
      "Filter the explorer by one or more tags simultaneously",
      "Assign tags in bulk by selecting multiple files",
    ],
    title: "Tags & Categories",
  },
  {
    description:
      "A full chronological log of every action performed in your library — uploads, edits, moves, deletions, and logins — so nothing goes unnoticed.",
    icon: "mdi:history",
    id: "history",
    image: "logs-preview.svg",
    tips: [
      "Filter by action type to quickly narrow down events",
      "Each log entry links back to the affected file",
      "Admins can see activity across all users",
    ],
    title: "Access History",
  },
  {
    description:
      "Keep tabs on your storage usage and recover anything deleted by accident. Files move to Trash first, giving you a safety net before permanent removal.",
    icon: "mdi:trash-can-outline",
    id: "trash",
    image: "storage-preview.svg",
    tips: [
      "Restore a file to its original location in one click",
      "Permanently delete individual files or empty the whole Trash",
      "Storage usage updates live as you upload and delete files",
    ],
    title: "Storage & Trash",
  },
];
</script>

<style scoped>
.slide-left-enter-active,
.slide-left-leave-active,
.slide-right-enter-active,
.slide-right-leave-active {
  transition:
    opacity 0.2s ease,
    transform 0.26s cubic-bezier(0.4, 0, 0.2, 1);
}

.slide-left-enter-from {
  opacity: 0;
  transform: translateX(28px);
}
.slide-left-leave-to {
  opacity: 0;
  transform: translateX(-28px);
}
.slide-right-enter-from {
  opacity: 0;
  transform: translateX(-28px);
}
.slide-right-leave-to {
  opacity: 0;
  transform: translateX(28px);
}
</style>
