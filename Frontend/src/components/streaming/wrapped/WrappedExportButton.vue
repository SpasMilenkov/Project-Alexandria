<template>
  <UButton icon="mdi-export" color="neutral" variant="outline" @click="open = true">Export</UButton>

  <UModal
    v-model:open="open"
    title="Your year, ready to share"
    description="A composed image of your Wrapped, made on this device."
    :ui="{ content: glassModalContentWide, body: glassModalBody }"
  >
    <template #body>
      <div class="flex flex-wrap items-end justify-between gap-4">
        <UFormField label="Image format">
          <div class="flex gap-2">
            <UButton
              v-for="option in formats"
              :key="option"
              :aria-pressed="format === option"
              :color="format === option ? 'primary' : 'neutral'"
              variant="outline"
              @click="format = option"
              >{{ option.toUpperCase() }}</UButton
            >
          </div>
        </UFormField>

        <p class="text-xs text-gray-600 dark:text-gray-400">{{ dimensions }}</p>
      </div>

      <p class="text-sm text-gray-600 dark:text-gray-400">
        Includes track and artist names, dates and listening stats. Your account details and custom
        background are not included. Nothing is uploaded automatically.
      </p>

      <div
        v-if="busy"
        class="flex h-56 items-center justify-center"
        role="status"
        aria-label="Preparing your image"
      >
        <UIcon name="i-mdi-loading" class="h-8 w-8 animate-spin text-gray-500" />
      </div>
      <div v-else-if="error" role="alert" class="py-6">
        <p class="text-sm text-gray-900 dark:text-gray-100">{{ error }}</p>

        <UButton class="mt-4" color="neutral" variant="outline" @click="prepare()"
          >Try again</UButton
        >
      </div>
      <div
        v-else-if="preview"
        class="max-h-[50vh] overflow-auto rounded-lg border border-gray-200 dark:border-gray-700"
        tabindex="0"
        aria-label="Scroll to review the complete exported recap"
      >
        <img
          :src="preview"
          class="block h-auto w-full"
          :alt="`${year} Wrapped export preview, including your listening summary, rankings and personal stories`"
        />
      </div>

      <p v-if="status" class="text-sm text-gray-600 dark:text-gray-400" role="status">
        {{ status }}
      </p>

      <p v-if="!canShare && file" class="text-xs text-gray-600 dark:text-gray-400">
        Device sharing is unavailable here. Download the image and share it wherever you like.
      </p>
    </template>

    <template #footer>
      <div class="flex w-full flex-wrap justify-end gap-2">
        <UButton
          v-if="canShare"
          icon="i-mdi-share-variant-outline"
          color="neutral"
          variant="outline"
          :disabled="busy || !file"
          :loading="sharing"
          @click="share"
          >Share image</UButton
        >

        <UButton
          icon="i-mdi-download-outline"
          color="primary"
          :disabled="busy || !file || !!error"
          @click="download"
          >Download {{ format.toUpperCase() }}</UButton
        >
      </div>
    </template>
  </UModal>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, ref, shallowRef, watch } from "vue";

import type { WrappedDeckResponse } from "@/api/stats";
import type { WrappedExportFormat } from "@/utils/wrapped-export.utils";
import type { WrappedPalette } from "@/utils/wrapped-palette.utils";

import { useTheme } from "@/composables/useTheme";
import { glassModalBody, glassModalContentWide } from "@/utils/modalUi";

const props = defineProps<{
  response: WrappedDeckResponse;
  year: number;
  palette?: WrappedPalette | null;
}>();

const { isDark } = useTheme();
const formats: WrappedExportFormat[] = ["png", "jpeg"];
const format = ref<WrappedExportFormat>("png");
const open = ref(false);
const busy = ref(false);
const sharing = ref(false);
const preview = ref<string | null>(null);
const file = shallowRef<File | null>(null);
const error = ref("");
const status = ref("");
const dimensions = ref("");
let generation = 0;
let canvas: HTMLCanvasElement | null = null;

const clearImage = () => {
  if (preview.value) URL.revokeObjectURL(preview.value);

  preview.value = null;
  file.value = null;
};

const prepare = async () => {
  const ticket = ++generation;

  busy.value = true;
  error.value = "";
  status.value = "";
  clearImage();

  try {
    const source = props.response;
    const theme = isDark.value;
    const selectedPalette = props.palette ? ([...props.palette] as WrappedPalette) : null;
    const selectedFormat = format.value;

    const { renderWrappedExport, encodeWrappedExport, exportFilename } =
      await import("@/utils/wrapped-export.utils");

    // Give the spinner a paint before synchronous canvas composition starts.
    await new Promise<void>((resolve) =>
      requestAnimationFrame(() => requestAnimationFrame(() => resolve())),
    );

    if (ticket !== generation) return;

    const result = canvas ?? (await renderWrappedExport(source, theme, selectedPalette));

    if (ticket !== generation) return;

    canvas = result;

    const blob = await encodeWrappedExport(result, selectedFormat);

    if (ticket !== generation) return;

    file.value = new File([blob], exportFilename(props.year, selectedFormat), { type: blob.type });
    preview.value = URL.createObjectURL(blob);
    dimensions.value = `${result.width} × ${result.height} px · ${(blob.size / 1024 / 1024).toFixed(1)} MB`;
  } catch (cause) {
    if (ticket === generation)
      error.value =
        cause instanceof Error ? cause.message : "Could not create your image. Please try again.";
  } finally {
    if (ticket === generation) busy.value = false;
  }
};

watch([open, () => props.response, () => props.palette, isDark], () => {
  ++generation;
  canvas = null;
  clearImage();
  dimensions.value = "";

  if (open.value) void prepare();
});

watch(format, () => {
  if (open.value) void prepare();
});

onBeforeUnmount(() => {
  ++generation;
  canvas = null;
  clearImage();
});

const canShare = computed(() => {
  if (
    !file.value ||
    typeof navigator.share !== "function" ||
    typeof navigator.canShare !== "function"
  )
    return false;

  try {
    return navigator.canShare({ files: [file.value] });
  } catch {
    return false;
  }
});

const download = () => {
  if (!file.value) return;

  const url = URL.createObjectURL(file.value);
  const anchor = document.createElement("a");

  anchor.href = url;
  anchor.download = file.value.name;
  document.body.appendChild(anchor);
  anchor.click();
  anchor.remove();
  // Downloads may consume the URL after the click handler has returned.
  setTimeout(() => URL.revokeObjectURL(url), 30000);
  status.value = "Download started. Your image is ready to share.";
};

const share = async () => {
  if (!file.value || !canShare.value) return;

  sharing.value = true;
  status.value = "";

  try {
    // The file is already prepared, preserving the user activation required by Web Share.
    await navigator.share({ files: [file.value], title: `${props.year} Wrapped` });
    status.value = "Image shared.";
  } catch (cause) {
    if (!(cause instanceof Error && cause.name === "AbortError"))
      status.value = "Device sharing could not finish. You can still download the image.";
  } finally {
    sharing.value = false;
  }
};
</script>
