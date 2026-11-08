<template>
  <DetailsSection title="Image details">
    <div v-if="isPending" class="flex h-8 items-center text-gray-500">
      <BlockSpinner :size="16" />
    </div>

    <p v-else-if="error" class="text-sm text-gray-600 dark:text-gray-400">
      Couldn't load the image details.
    </p>

    <p v-else-if="!metadata?.isExtracted" class="text-sm text-gray-600 dark:text-gray-400">
      Not read yet. Details appear once the preview is generated.
    </p>

    <p v-else-if="!hasAnything" class="text-sm text-gray-600 dark:text-gray-400">
      This image has no camera or capture details.
    </p>

    <template v-else>
      <ul
        v-if="facts.length > 0"
        class="flex flex-col divide-y divide-gray-200/70 rounded-xl border border-gray-200/70 dark:divide-gray-700/70 dark:border-gray-700/70"
      >
        <li v-for="fact in facts" :key="fact.key" class="group flex items-start gap-3 p-3">
          <Icon
            :icon="fact.icon"
            class="mt-0.5 h-5 w-5 shrink-0 text-gray-600 dark:text-gray-400"
            aria-hidden="true"
          />

          <div class="flex min-w-0 flex-1 flex-col gap-0.5">
            <span class="text-sm font-medium wrap-break-word text-gray-900 dark:text-gray-100">
              {{ fact.title }}
            </span>

            <span v-if="fact.detail" class="text-xs text-gray-600 dark:text-gray-400">
              {{ fact.detail }}
            </span>

            <span v-if="fact.note" class="text-xs text-gray-500 dark:text-gray-500">
              {{ fact.note }}
            </span>
          </div>

          <DetailsCopyButton
            v-if="fact.copyValue && fact.copyLabel"
            :value="fact.copyValue"
            :label="fact.copyLabel"
          />
        </li>
      </ul>

      <UCollapsible
        v-if="settingTiles.length > 0"
        v-model:open="isSettingsOpen"
        class="rounded-xl border border-gray-200/70 dark:border-gray-700/70"
      >
        <button type="button" class="flex w-full items-center gap-3 p-3 text-left">
          <span class="shrink-0 text-sm font-medium text-gray-900 dark:text-gray-100">
            Camera settings
          </span>

          <span class="min-w-0 truncate text-xs tabular-nums text-gray-600 dark:text-gray-400">
            {{ settingsSummary }}
          </span>

          <Icon
            icon="mdi:chevron-down"
            class="ml-auto h-4 w-4 shrink-0 text-gray-600 transition-transform duration-200 motion-reduce:transition-none dark:text-gray-400"
            :class="{ 'rotate-180': isSettingsOpen }"
            aria-hidden="true"
          />
        </button>

        <template #content>
          <div class="grid grid-cols-2 gap-2 px-3 pb-3">
            <DetailsTile
              v-for="tile in settingTiles"
              :key="tile.key"
              :label="tile.label"
              :hint="tile.hint"
              :class="{ 'col-span-2': tile.wide }"
            >
              <span class="block wrap-break-word tabular-nums">{{ tile.value }}</span>
            </DetailsTile>
          </div>
        </template>
      </UCollapsible>
    </template>
  </DetailsSection>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed } from "vue";

import type { CaptureMetadataDto } from "@/api/file";

import BlockSpinner from "@/components/common/BlockSpinner.vue";
import DetailsCopyButton from "@/components/common/details/DetailsCopyButton.vue";
import DetailsSection from "@/components/common/details/DetailsSection.vue";
import DetailsTile from "@/components/common/details/DetailsTile.vue";
import {
  describeCaptureMoment,
  describeShape,
  focalRangeHint,
  formatAperture,
  formatCamera,
  formatCoordinates,
  formatExposure,
  formatFocalLength,
  formatMegapixels,
  isoHint,
  shutterHint,
  summarizeSettings,
} from "@/features/files/capture-metadata";
import { getCaptureMetadata } from "@/queries/files";
import { useSettingsStore } from "@/stores/settings";
import { formatDate } from "@/utils/date-formatters";

interface CaptureFact {
  key: string;
  icon: string;
  title: string;
  detail?: string;
  note?: string;
  copyValue?: string;
  copyLabel?: string;
}

interface SettingTile {
  key: string;
  label: string;
  value: string;
  hint?: string;
  wide?: boolean;
}

const { fileId, versionId, enabled } = defineProps<{
  fileId: string;
  versionId: string;
  enabled: boolean;
}>();

const settingsStore = useSettingsStore();

const isSettingsOpen = computed({
  get: () => settingsStore.isCameraSettingsOpen,
  set: (value: boolean) => settingsStore.setCameraSettingsOpen(value),
});

const {
  data: metadata,
  isPending,
  error,
} = useQuery(() => ({
  ...getCaptureMetadata({ fileId, versionId }),
  enabled,
}));

const momentFact = (m: CaptureMetadataDto): CaptureFact | null => {
  if (!m.capturedAt) return null;

  const moment = describeCaptureMoment(m.capturedAt, m.capturedOffsetMinutes);

  return {
    key: "moment",
    icon: "mdi:calendar-clock-outline",
    title: moment.headline,
    detail: `${moment.date} at ${moment.time} · ${formatDate(m.capturedAt)}`,
    note: moment.zoneNote,
  };
};

const deviceFact = (m: CaptureMetadataDto): CaptureFact | null => {
  const camera = formatCamera(m.cameraMake, m.cameraModel);
  const title = camera ?? m.lensModel;
  if (!title) return null;

  const fact: CaptureFact = { key: "device", icon: "mdi:camera-outline", title };
  if (camera && m.lensModel) fact.detail = m.lensModel;

  return fact;
};

const sizeFact = (m: CaptureMetadataDto): CaptureFact | null => {
  if (m.width === null || m.height === null) return null;

  return {
    key: "size",
    icon: "mdi:image-outline",
    title: `${describeShape(m.width, m.height)} · ${formatMegapixels(m.width, m.height)}`,
    detail: `${m.width} × ${m.height} pixels`,
  };
};

const locationFact = (m: CaptureMetadataDto): CaptureFact | null => {
  if (!m.hasLocation || m.latitude === null || m.longitude === null) return null;

  return {
    key: "location",
    icon: "mdi:map-marker-outline",
    title: formatCoordinates(m.latitude, m.longitude),
    detail: "Saved in the photo by the camera",
    copyValue: `${m.latitude}, ${m.longitude}`,
    copyLabel: "Coordinates",
  };
};

const facts = computed((): CaptureFact[] => {
  const m = metadata.value;
  if (!m?.isExtracted) return [];

  return [momentFact(m), deviceFact(m), sizeFact(m), locationFact(m)].filter(
    (fact): fact is CaptureFact => fact !== null,
  );
});

const focalTile = (focalMm: number | null, focal35mm: number | null): SettingTile => {
  if (focal35mm === null) {
    return { key: "focal", label: "Focal length", value: formatFocalLength(focalMm ?? 0) };
  }

  let hint = focalRangeHint(focal35mm);
  if (focalMm !== null && Math.round(focalMm) !== focal35mm) {
    hint = `${hint}, ${formatFocalLength(focalMm)} actual`;
  }

  return { key: "focal", label: "Focal length", value: formatFocalLength(focal35mm), hint };
};

const settingTiles = computed((): SettingTile[] => {
  const m = metadata.value;
  if (!m?.isExtracted) return [];

  const tiles: SettingTile[] = [];

  if (m.fNumber !== null) {
    tiles.push({ key: "aperture", label: "Aperture", value: formatAperture(m.fNumber) });
  }

  if (m.exposureSeconds !== null) {
    tiles.push({
      key: "shutter",
      label: "Shutter",
      value: formatExposure(m.exposureSeconds),
      hint: shutterHint(m.exposureSeconds),
    });
  }

  if (m.iso !== null) {
    tiles.push({ key: "iso", label: "ISO", value: String(m.iso), hint: isoHint(m.iso) });
  }

  if (m.focalLengthMm !== null || m.focalLength35mm !== null) {
    tiles.push(focalTile(m.focalLengthMm, m.focalLength35mm));
  }

  if (m.software) {
    tiles.push({ key: "software", label: "Software", value: m.software, wide: true });
  }

  // An odd half-width tile would leave a hole in the two-column grid, so it spans instead.
  const halves = tiles.filter((tile) => !tile.wide);
  const lastHalf = halves.at(-1);
  if (lastHalf && halves.length % 2 === 1) lastHalf.wide = true;

  return tiles;
});

const settingsSummary = computed(() => {
  if (!metadata.value) return "";

  return summarizeSettings(metadata.value);
});

const hasAnything = computed(() => facts.value.length > 0 || settingTiles.value.length > 0);
</script>
