<script setup lang="ts">
import { computed, ref } from "vue";
import { useRoute } from "vue-router";

import { useAppToast } from "@/composables/useAppToast";
import { useReportProblem } from "@/mutations/monitoring";

const MAX_DESCRIPTION_LENGTH = 2000;

const emit = defineEmits<{ close: [] }>();

const route = useRoute();
const toast = useAppToast();
const { mutateAsync: submitReport, isLoading: isSubmitting } = useReportProblem();

const description = ref("");

// pageContext is captured automatically from the current route and shown
// read-only so users know what is being attached to their report.
const pageContext = computed(() => route.fullPath);

const canSubmit = computed(() => description.value.trim().length > 0);

const dismiss = () => emit("close");

const handleSubmit = async () => {
  const trimmed = description.value.trim();
  if (!trimmed || isSubmitting.value) return;
  try {
    await submitReport({ description: trimmed, pageContext: pageContext.value });
    toast.success("Report sent", "Thanks — we'll take a look.");
    description.value = "";
    dismiss();
  } catch (err) {
    const status = (err as { response?: { status?: number } })?.response?.status;
    if (status === 429) {
      toast.warning(
        "Too many reports",
        "You've already sent a few reports recently — try again later.",
      );
    } else {
      toast.error("Could not send report", err);
    }
  }
};
</script>

<template>
  <UModal
    :open="true"
    :close="false"
    :overlay="true"
    :ui="{
      content:
        'bg-white/60 dark:bg-white/2 backdrop-blur-sm border border-gray-200/70 dark:border-gray-700/70',
    }"
    @update:open="!$event && dismiss()"
  >
    <template #content>
      <div class="p-6 space-y-6">
        <!-- Header -->
        <div class="flex items-start justify-between gap-3">
          <div class="flex items-center gap-3 min-w-0">
            <span
              class="w-9 h-9 rounded-xl flex items-center justify-center shrink-0 bg-gray-500/10"
            >
              <UIcon name="i-lucide-life-buoy" class="w-5 h-5 text-gray-500 dark:text-gray-400" />
            </span>
            <div class="min-w-0">
              <h2 class="text-base font-semibold text-gray-900 dark:text-gray-100">
                Report a problem
              </h2>
              <p class="text-xs text-gray-500 dark:text-gray-400 mt-0.5">
                Tell us what happened — we'll look into it.
              </p>
            </div>
          </div>
          <UButton
            icon="i-lucide-x"
            color="neutral"
            variant="ghost"
            size="xs"
            aria-label="Close"
            @click="dismiss"
          />
        </div>

        <!-- Description -->
        <UFormField label="What happened?">
          <UTextarea
            v-model="description"
            class="w-full"
            :rows="5"
            :maxlength="MAX_DESCRIPTION_LENGTH"
            placeholder="The more detail, the faster we can track it down…"
            autoresize
            :autofocus="true"
          />

          <!-- Page context + character count live together as one quiet
               meta line under the field, instead of a separate boxed callout. -->
          <div class="flex items-center justify-between gap-3 mt-1.5">
            <p class="text-xs text-gray-500 dark:text-gray-400 flex items-center gap-1.5 min-w-0">
              <UIcon name="i-lucide-map-pin" class="w-3 h-3 shrink-0" />
              <span class="truncate font-mono">{{ pageContext }}</span>
            </p>
            <span class="text-[11px] tabular-nums text-gray-400 dark:text-gray-500 shrink-0">
              {{ description.length }}/{{ MAX_DESCRIPTION_LENGTH }}
            </span>
          </div>
        </UFormField>

        <!-- Actions -->
        <div class="flex justify-end gap-2 pt-1">
          <UButton variant="outline" color="neutral" :disabled="isSubmitting" @click="dismiss">
            Cancel
          </UButton>
          <UButton
            color="primary"
            icon="i-lucide-send"
            :loading="isSubmitting"
            :disabled="!canSubmit"
            @click="handleSubmit"
          >
            Send report
          </UButton>
        </div>
      </div>
    </template>
  </UModal>
</template>
