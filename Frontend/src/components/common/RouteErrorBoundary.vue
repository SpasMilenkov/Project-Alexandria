<script setup lang="ts">
import { computed, onErrorCaptured, ref, watch } from "vue";
import { useRoute } from "vue-router";

import ErrorGlyph from "@/components/common/errors/ErrorGlyph.vue";
import ErrorScene from "@/components/common/errors/ErrorScene.vue";
import ErrorSlip from "@/components/common/errors/ErrorSlip.vue";
import ErrorSlipRow from "@/components/common/errors/ErrorSlipRow.vue";
import ReportProblemModal from "@/components/modals/ReportProblemModal.vue";
import { isPageBreakingError } from "@/features/app-errors/page-failures";
import { isStaleChunkError } from "@/features/app-errors/stale-chunks";
import { useAuthStore } from "@/stores/auth";
import { formatClockTime } from "@/utils/date-formatters";
import { logger } from "@/utils/logger";

interface PageFailure {
  at: Date;
  isOutdatedBuild: boolean;
  message: string;
  path: string;
}

defineSlots<{ default: (props: { attempt: number }) => unknown }>();

const route = useRoute();
const authStore = useAuthStore();
const reportProblemModal = useOverlay().create(ReportProblemModal);

const failure = ref<PageFailure>();
const attempt = ref(0);

const homePath = computed(() => (authStore.isSessionVerified ? "/home" : "/"));
const failedAt = computed(() => (failure.value ? formatClockTime(failure.value.at) : undefined));

const describeError = (error: unknown): string => {
  if (error instanceof Error && error.message) return error.message;

  return "No details were given.";
};

onErrorCaptured((error, _instance, info) => {
  if (!isPageBreakingError(info)) return;

  logger.error(`The page broke during ${info}`, error);
  failure.value = {
    at: new Date(),
    isOutdatedBuild: isStaleChunkError(error),
    message: describeError(error),
    path: route.fullPath,
  };

  return false;
});

// Leaving the broken page clears the error instead of carrying it to the next one.
watch(
  () => route.fullPath,
  () => {
    failure.value = undefined;
  },
);

const retry = () => {
  failure.value = undefined;
  attempt.value += 1;
};

const reload = () => {
  window.location.reload();
};

const reportProblem = async () => {
  await reportProblemModal.open().result;
};
</script>

<template>
  <template v-if="failure">
    <ErrorScene
      v-if="failure.isOutdatedBuild"
      eyebrow="Update available"
      title="Alexandria was updated"
      role="alert"
    >
      <template #mark>
        <ErrorGlyph icon="i-lucide-refresh-cw" />
      </template>

      <template #description>
        A newer version was installed while this tab was open, and this page needs its files. Reload
        to get it.
      </template>

      <template #actions>
        <UButton color="primary" class="rounded-full px-5" @click="reload">Reload</UButton>
      </template>

      <template #aside>
        <ErrorSlip title="Details" :stamp="failedAt">
          <ErrorSlipRow label="This tab">Older version</ErrorSlipRow>
          <ErrorSlipRow label="The server">Newer version</ErrorSlipRow>
          <ErrorSlipRow label="Your files">Safe</ErrorSlipRow>
        </ErrorSlip>
      </template>
    </ErrorScene>

    <ErrorScene v-else eyebrow="Something went wrong" title="This page failed to load" role="alert">
      <template #mark>Oops</template>

      <template #description>
        Something broke while loading it. The rest of the app still works, and trying again usually
        fixes it.
      </template>

      <template #actions>
        <UButton color="primary" class="rounded-full px-5" @click="retry">Try again</UButton>

        <UButton :to="homePath" color="neutral" variant="outline" class="rounded-full px-5">
          Go home
        </UButton>
      </template>

      <template #aside>
        <ErrorSlip title="Details" :stamp="failedAt">
          <ErrorSlipRow label="Page">
            <span class="font-mono break-all">{{ failure.path }}</span>
          </ErrorSlipRow>

          <ErrorSlipRow label="Error">
            <span class="line-clamp-4 font-mono break-words">{{ failure.message }}</span>
          </ErrorSlipRow>

          <ErrorSlipRow label="Your files">Safe</ErrorSlipRow>

          <template v-if="authStore.isSessionVerified" #footer>
            <UButton
              icon="i-lucide-life-buoy"
              color="neutral"
              variant="outline"
              size="sm"
              class="rounded-full px-4"
              @click="reportProblem"
            >
              Report this problem
            </UButton>
          </template>
        </ErrorSlip>
      </template>
    </ErrorScene>
  </template>

  <slot v-else :attempt="attempt" />
</template>
