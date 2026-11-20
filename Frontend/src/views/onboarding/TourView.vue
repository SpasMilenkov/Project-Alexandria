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
          v-for="(slide, i) in slides"
          :key="slide.id"
          :aria-label="`Go to ${slide.title}`"
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
        class="overflow-hidden rounded-2xl border border-gray-200/60 bg-white shadow-lg shadow-black/5 dark:border-gray-700/50 dark:bg-neutral-900"
      >
        <div class="flex flex-col lg:flex-row lg:min-h-105">
          <!-- LEFT: content pane -->
          <div
            class="flex flex-col justify-between p-8 lg:w-[42%] lg:border-r lg:border-gray-200/60 lg:dark:border-gray-700/50"
          >
            <div class="grid">
              <!--
                Desktop sizer: the feature and library slides render invisibly in one grid cell,
                so the card starts at the tallest of them and keeps that height while stepping.
                The automation slide is left out on purpose and may grow to its full height.
              -->
              <div
                class="invisible col-start-1 row-start-1 hidden lg:grid"
                aria-hidden="true"
                inert
              >
                <div v-for="slide in sizedSlides" :key="slide.id" class="col-start-1 row-start-1">
                  <TourFeatureSlide v-if="slide.kind === 'feature'" :feature="slide.feature" />
                  <TourLibrarySlide
                    v-else
                    :drafts="drafts"
                    :capabilities="capabilities"
                    :errors="nameErrors"
                  />
                </div>
              </div>

              <Transition :name="transitionName" mode="out-in">
                <div :key="current" class="col-start-1 row-start-1">
                  <TourFeatureSlide v-if="activeFeature" :feature="activeFeature" />

                  <TourLibrarySlide
                    v-else-if="activeSlide.kind === 'library'"
                    v-model:drafts="drafts"
                    :capabilities="capabilities"
                    :errors="nameErrors"
                  />

                  <TourAutomationSlide
                    v-else
                    v-model:drafts="drafts"
                    :capabilities="capabilities"
                    :errors="qualityErrors"
                    @focus="focusedKind = $event"
                  />
                </div>
              </Transition>
            </div>

            <!-- Navigation — always at the bottom of content pane -->
            <div class="mt-8 flex items-center gap-3">
              <UButton
                v-if="current > 0"
                color="neutral"
                variant="outline"
                size="lg"
                leading-icon="i-lucide-arrow-left"
                @click="prev"
              >
                Back
              </UButton>

              <UButton
                color="primary"
                variant="solid"
                size="lg"
                class="flex-1"
                :loading="isFinishing"
                :trailing-icon="isLast ? 'i-lucide-rocket' : 'i-lucide-arrow-right'"
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
                  v-if="activeFeature"
                  :src="imageUrl"
                  :alt="`${activeFeature.title} preview`"
                  class="w-full rounded-lg shadow-2xl ring-1"
                  :class="
                    isDark ? 'shadow-black/40 ring-white/10' : 'shadow-black/15 ring-black/10'
                  "
                  style="aspect-ratio: 520/340; object-fit: cover"
                  loading="lazy"
                  decoding="async"
                />

                <LibraryExplorerPreview
                  v-else-if="activeSlide.kind === 'library'"
                  :drafts="drafts"
                  :capabilities="capabilities"
                />

                <AutomationDrawerPreview
                  v-else-if="focusedDraft"
                  :draft="focusedDraft"
                  :capabilities="capabilities"
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
          <UIcon name="i-lucide-circle-alert" class="mt-0.5 h-4 w-4 shrink-0 text-red-500" />
          <p class="text-sm text-red-600 dark:text-red-400">{{ serverError }}</p>
        </div>
      </Transition>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useDark } from "@vueuse/core";
import { computed, onBeforeUnmount, onMounted, ref, watch } from "vue";
import { useRoute, useRouter } from "vue-router";

import AutomationDrawerPreview from "@/components/onboarding/tour/AutomationDrawerPreview.vue";
import LibraryExplorerPreview from "@/components/onboarding/tour/LibraryExplorerPreview.vue";
import TourAutomationSlide from "@/components/onboarding/tour/TourAutomationSlide.vue";
import TourFeatureSlide from "@/components/onboarding/tour/TourFeatureSlide.vue";
import TourLibrarySlide from "@/components/onboarding/tour/TourLibrarySlide.vue";
import { useOnboardingGuard } from "@/composables/useOnboardingGuard";
import { type LibraryFolderKind, OnboardingStep } from "@/enums";
import { getHomeLandingDestination } from "@/features/home/landing";
import {
  LIBRARY_FOLDER_PRESETS,
  type LibraryErrors,
  type LibraryFolderDraft,
  buildFinishTourRequest,
  createLibraryDrafts,
  hasAnyAutomation,
  isAutomatable,
  readLibraryCapabilities,
  validateFolderNames,
  validateQualities,
} from "@/features/onboarding/library-setup";
import { finishTour } from "@/mutations/user";
import { useAuthStore } from "@/stores/auth";

useOnboardingGuard(OnboardingStep.Tour);

const router = useRouter();
const route = useRoute();
const authStore = useAuthStore();
const isDark = useDark();

const { mutate, error: mutationError, status, isLoading: isFinishing } = finishTour();

watch(status, (s) => {
  if (s !== "success") return;

  if (authStore.user) authStore.user.onboardingStep = OnboardingStep.Done;

  void router.replace(getHomeLandingDestination(router, route.query.redirect));
});

const serverError = computed(() => {
  if (!mutationError.value) return null;
  return mutationError.value instanceof Error
    ? mutationError.value.message
    : "Something went wrong. Please try again.";
});

// Library setup
const capabilities = readLibraryCapabilities();
const drafts = ref<LibraryFolderDraft[]>(createLibraryDrafts(capabilities));
const nameErrors = ref<LibraryErrors>({});
const qualityErrors = ref<LibraryErrors>({});
const validationAttempted = ref(false);

const firstAutomatable = LIBRARY_FOLDER_PRESETS.find((preset) =>
  isAutomatable(preset, capabilities),
);
const focusedKind = ref<LibraryFolderKind | null>(firstAutomatable?.kind ?? null);
const focusedDraft = computed(
  () => drafts.value.find((draft) => draft.kind === focusedKind.value) ?? null,
);

const hasErrors = (errors: LibraryErrors) => Object.keys(errors).length > 0;

const validateLibrary = () => {
  validationAttempted.value = true;
  nameErrors.value = validateFolderNames(drafts.value);
  qualityErrors.value = validateQualities(drafts.value, capabilities);
};

// Errors clear as the user fixes them, but a fresh form stays quiet until the first attempt.
watch(
  drafts,
  () => {
    if (validationAttempted.value) validateLibrary();
  },
  { deep: true },
);

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
    icon: "i-lucide-folder-open",
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
    icon: "i-lucide-tags",
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
    icon: "i-lucide-history",
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
    icon: "i-lucide-trash-2",
    id: "trash",
    image: "storage-preview.svg",
    tips: [
      "Restore a file to its original location in one click",
      "Files are automatically permanently deleted after 30 days of being in the trash",
      "Storage usage updates live as you upload and delete files",
    ],
    title: "Storage & Trash",
  },
];

// Carousel
type TourSlide =
  | { id: string; kind: "feature"; title: string; feature: TourFeature }
  | { id: string; kind: "library"; title: string }
  | { id: string; kind: "automation"; title: string };

// The library slides come last so the final "Get started" is the step that applies the choices.
const slides: TourSlide[] = [
  ...tourFeatures.map((feature): TourSlide => ({
    feature,
    id: feature.id,
    kind: "feature",
    title: feature.title,
  })),
  { id: "library", kind: "library", title: "Your library" },
];

if (hasAnyAutomation(capabilities)) {
  slides.push({ id: "automation", kind: "automation", title: "Automation" });
}

const current = ref(0);
const direction = ref<"forward" | "back">("forward");
const transitionName = computed(() =>
  direction.value === "forward" ? "slide-left" : "slide-right",
);

const activeSlide = computed(() => slides[current.value]);
const sizedSlides = slides.filter((slide) => slide.kind !== "automation");
const activeFeature = computed(() => {
  const slide = activeSlide.value;
  if (slide.kind !== "feature") return null;

  return slide.feature;
});
const isLast = computed(() => current.value === slides.length - 1);

// Resolves the correct image from /public/images/onboarding/{dark|light}/
const imageUrl = computed(() => {
  if (!activeFeature.value) return "";

  const theme = isDark.value ? "dark" : "light";
  return `/images/onboarding/${theme}/${activeFeature.value.image}`;
});

const slideIndex = (kind: TourSlide["kind"]) => slides.findIndex((slide) => slide.kind === kind);

const next = () => {
  if (isLast.value) return;

  if (activeSlide.value.kind === "library") {
    validateLibrary();
    if (hasErrors(nameErrors.value)) return;
  }

  direction.value = "forward";
  current.value++;
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
  validateLibrary();

  if (hasErrors(nameErrors.value)) {
    goTo(slideIndex("library"));
    return;
  }

  if (hasErrors(qualityErrors.value)) {
    goTo(slideIndex("automation"));
    return;
  }

  mutate(buildFinishTourRequest(drafts.value, capabilities));
};

// Enter inside a field or on a control belongs to that control, not to the carousel.
const INTERACTIVE_SELECTOR =
  "input, textarea, select, button, a, [role='switch'], [role='checkbox']";

const handleEnter = (e: KeyboardEvent) => {
  if (e.key !== "Enter") return;
  if (e.target instanceof HTMLElement && e.target.closest(INTERACTIVE_SELECTOR)) return;

  if (isLast.value) {
    handleFinish();
    return;
  }

  next();
};

onMounted(() => document.addEventListener("keydown", handleEnter));
onBeforeUnmount(() => document.removeEventListener("keydown", handleEnter));
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
