<template>
  <USkeleton v-if="previewLoading" />
  <div
    v-else-if="previewUrl || archivePreview || textPreview"
    class="bg-neutral-100 dark:bg-neutral-800/50 rounded-lg p-4"
  >
    <h4 class="text-sm font-medium text-gray-700 dark:text-gray-300 mb-3">Preview</h4>

    <!-- Audio Preview -->
    <div
      v-if="mimeType.startsWith('audio/') && previewUrl"
      class="relative w-full bg-black/5 dark:bg-black/20 rounded-lg overflow-hidden"
    >
      <div class="relative w-full aspect-video">
        <img
          v-if="thumbnailUrl"
          :src="thumbnailUrl"
          alt="Audio thumbnail"
          class="w-full h-full object-cover"
        />
        <div
          class="absolute inset-0 flex items-center justify-center cursor-pointer transition-all duration-200"
          :class="isAudioPlaying ? 'bg-black/20' : 'bg-black/0 hover:bg-black/30'"
          @click="toggleAudio"
        >
          <div
            v-if="!isAudioPlaying"
            class="flex items-center justify-center w-16 h-16 bg-white/30 backdrop-blur-sm rounded-full hover:bg-white/40 hover:scale-110 transition-all"
          >
            <Icon icon="mdi-play" class="w-10 h-10 text-white ml-1" />
          </div>
          <AudioEqualizer v-else />
        </div>
      </div>

      <div
        class="px-3 py-2 bg-neutral-100 dark:bg-neutral-900/50 backdrop-blur-sm border-t border-neutral-200 dark:border-neutral-800"
      >
        <div class="flex items-center gap-2">
          <button
            @click.stop="toggleAudio"
            class="p-1.5 hover:bg-neutral-200 dark:hover:bg-white/10 rounded-full transition-colors shrink-0"
          >
            <Icon
              :icon="isAudioPlaying ? 'mdi-pause' : 'mdi-play'"
              class="w-4 h-4 text-neutral-700 dark:text-white"
            />
          </button>
          <span class="text-xs text-neutral-600 dark:text-white/60 min-w-8.75 shrink-0">
            {{ formatTime(currentTime) }}
          </span>
          <input
            ref="progressBar"
            type="range"
            min="0"
            :max="duration || 100"
            :value="currentTime"
            @input="seekAudio"
            class="seek-range"
          />
          <span class="text-xs text-neutral-600 dark:text-white/60 min-w-8.75 shrink-0">
            {{ formatTime(duration) }}
          </span>
          <UPopover :content="{ side: 'top' }" class="shrink-0">
            <button
              class="p-1.5 hover:bg-neutral-200 dark:hover:bg-white/10 rounded-full transition-colors"
            >
              <Icon
                :icon="
                  isMuted || volume === 0
                    ? 'mdi-volume-off'
                    : volume < 0.5
                      ? 'mdi-volume-medium'
                      : 'mdi-volume-high'
                "
                class="w-4 h-4 text-neutral-700 dark:text-white"
              />
            </button>
            <template #content>
              <div
                class="flex flex-col items-center gap-2 p-3 bg-white dark:bg-neutral-900 rounded-lg shadow-lg border border-neutral-200 dark:border-neutral-800"
              >
                <span class="text-xs font-medium text-neutral-600 dark:text-white/70">
                  {{ Math.round(volume * 100) }}%
                </span>
                <input
                  type="range"
                  min="0"
                  max="1"
                  step="0.01"
                  :value="volume"
                  @input="updateVolume"
                  @click.stop
                  orient="vertical"
                  class="volume-range"
                />
                <button
                  @click.stop="toggleMute"
                  class="p-1 hover:bg-neutral-200 dark:hover:bg-white/10 rounded transition-colors"
                >
                  <Icon
                    icon="mdi-volume-off"
                    class="w-3.5 h-3.5 text-neutral-600 dark:text-white/70"
                  />
                </button>
              </div>
            </template>
          </UPopover>
        </div>
      </div>

      <audio
        ref="audioPlayer"
        :src="previewUrl"
        @timeupdate="updateProgress"
        @loadedmetadata="onAudioLoaded"
        @ended="onAudioEnded"
        @canplay="onAudioReady"
        class="hidden"
      />
    </div>

    <!-- Image Preview -->
    <div
      v-else-if="mimeType.startsWith('image/') && previewUrl"
      class="relative w-full rounded-lg overflow-hidden bg-black/5 dark:bg-black/20"
    >
      <img
        :src="previewUrl"
        :alt="fileName"
        class="w-full h-auto max-h-96 object-contain mx-auto"
      />
    </div>

    <!-- Video Preview -->
    <div
      v-else-if="mimeType.startsWith('video/') && previewUrl"
      class="relative w-full aspect-video bg-black rounded-lg overflow-hidden"
    >
      <video ref="videoPlayer" class="w-full h-full object-contain" controls>
        <source :src="previewUrl" type="video/mp4" />
      </video>
    </div>

    <!-- PDF Preview -->
    <div
      v-else-if="pdfPreviewMimes.includes(mimeType) && previewUrl"
      class="relative w-xl h-220 bg-white dark:bg-neutral-900 rounded-lg overflow-hidden"
    >
      <embed :src="previewUrl" type="application/pdf" class="w-full h-full" />
    </div>

    <!-- Archive Preview -->
    <div v-else-if="archivePreview">
      <UTree :items="archivePreviewItems" />
    </div>

    <!-- Text Preview -->
    <div
      v-else-if="textPreview"
      class="w-full p-6 bg-white dark:bg-neutral-900 rounded-lg border border-gray-200 dark:border-gray-700"
    >
      <p class="max-h-96 overflow-y-auto wrap-break-word">
        {{ textPreview }}
      </p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { getPreview } from "@/queries/files";
import { Icon } from "@iconify/vue";
import { useQuery } from "@pinia/colada";
import { computed, ref, watch } from "vue";
import AudioEqualizer from "../AudioEqualizer.vue";

const props = defineProps<{
  fileId: string;
  fileName: string;
  mimeType: string;
}>();

// Preview query

const { data: previewData, isLoading: previewLoading } = useQuery(() => getPreview(props.fileId));

const previewUrl = computed(() => previewData.value?.previewUrl ?? null);
const thumbnailUrl = computed(() => previewData.value?.thumbnailUrl ?? null);
const archivePreview = computed(() => previewData.value?.archivePreview ?? null);
const textPreview = computed(() => previewData.value?.textPreview ?? null);

// Audio player state

const audioPlayer = ref<HTMLAudioElement | null>(null);
const isAudioPlaying = ref(false);
const currentTime = ref(0);
const duration = ref(0);
const volume = ref(0.2);
const isMuted = ref(false);

// Audio handlers

const toggleAudio = async (): Promise<void> => {
  if (!audioPlayer.value) return;
  if (audioPlayer.value.paused) {
    await audioPlayer.value.play();
    isAudioPlaying.value = true;
  } else {
    audioPlayer.value.pause();
    isAudioPlaying.value = false;
  }
};

const onAudioReady = (): void => {
  if (audioPlayer.value) audioPlayer.value.volume = volume.value;
};

const seekAudio = (event: Event): void => {
  const target = event.target as HTMLInputElement;
  if (audioPlayer.value) audioPlayer.value.currentTime = Number(target.value);
};

const updateProgress = (): void => {
  if (audioPlayer.value) currentTime.value = audioPlayer.value.currentTime;
};

const onAudioLoaded = (): void => {
  if (audioPlayer.value) duration.value = audioPlayer.value.duration;
};

const onAudioEnded = (): void => {
  isAudioPlaying.value = false;
  currentTime.value = 0;
};

const updateVolume = (event: Event): void => {
  const target = event.target as HTMLInputElement;
  volume.value = Number(target.value);
  if (audioPlayer.value) {
    audioPlayer.value.volume = volume.value;
    isMuted.value = volume.value === 0;
  }
};

const toggleMute = (): void => {
  if (audioPlayer.value) {
    isMuted.value = !isMuted.value;
    audioPlayer.value.muted = isMuted.value;
  }
};

// Stop audio when fileId changes (drawer opened for a different file)
watch(
  () => props.fileId,
  () => {
    if (audioPlayer.value) {
      audioPlayer.value.pause();
      audioPlayer.value.currentTime = 0;
    }
    isAudioPlaying.value = false;
    currentTime.value = 0;
  },
);

// Archive preview

interface ArchiveEntry {
  Key: string;
  SizeKB: number;
  Modified: string;
}

interface ArchiveData {
  FileCount: number;
  FileName: string;
  Entries: ArchiveEntry[];
}

const archivePreviewItems = computed(() => {
  if (!archivePreview.value) return undefined;

  const tree: ArchiveData = JSON.parse(archivePreview.value);
  const rootNode = {
    children: [] as any[],
    defaultExpanded: true,
    icon: "mdi-folder",
    label: tree.FileName,
  };

  const pathCache = new Map<string, any>();
  pathCache.set("", rootNode);

  tree.Entries.forEach((entry) => {
    const pathParts = entry.Key.split("/");
    let currentPath = "";
    let parentNode = rootNode;

    for (let i = 0; i < pathParts.length - 1; i++) {
      const part = pathParts[i];
      const newPath = currentPath ? `${currentPath}/${part}` : part;
      let folder = pathCache.get(newPath);

      if (!folder) {
        folder = { children: [], icon: "mdi-folder", label: part };
        if (!parentNode.children) parentNode.children = [];
        parentNode.children.push(folder);
        pathCache.set(newPath, folder);
      }

      parentNode = folder;
      currentPath = newPath;
    }

    if (!parentNode.children) parentNode.children = [];
    const fileName = pathParts[pathParts.length - 1];
    parentNode.children.push({ icon: "mdi-file", label: fileName });
  });

  return [rootNode];
});

// Formatters

const formatTime = (seconds: number): string => {
  if (!seconds || !isFinite(seconds)) return "0:00";
  const mins = Math.floor(seconds / 60);
  const secs = Math.floor(seconds % 60);
  return `${mins}:${secs.toString().padStart(2, "0")}`;
};

// MIME helpers

const pdfPreviewMimes = [
  "application/pdf",
  "application/msword",
  "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
  "application/vnd.oasis.opendocument.text",
  "application/rtf",
  "application/vnd.ms-excel",
  "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  "application/vnd.oasis.opendocument.spreadsheet",
  "text/csv",
  "text/tab-separated-values",
  "application/vnd.ms-powerpoint",
  "application/vnd.openxmlformats-officedocument.presentationml.presentation",
  "application/vnd.oasis.opendocument.presentation",
];
</script>

<style scoped>
.seek-range {
  flex: 1;
  height: 4px;
  border-radius: 8px;
  appearance: none;
  cursor: pointer;
  background: rgb(212 212 212); /* neutral-300 */
}

.seek-range::-webkit-slider-thumb {
  appearance: none;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--color-primary);
  cursor: pointer;
  transition: transform 150ms ease;
}

.seek-range::-webkit-slider-thumb:hover {
  transform: scale(1.25);
}

.seek-range::-moz-range-thumb {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  border: none;
  background: var(--color-primary);
  cursor: pointer;
}

.volume-range {
  height: 96px;
  width: 4px;
  border-radius: 8px;
  appearance: none;
  cursor: pointer;
  background: rgb(212 212 212); /* neutral-300 */
  writing-mode: vertical-lr;
  direction: rtl;
}

.volume-range::-webkit-slider-thumb {
  appearance: none;
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: var(--color-primary);
  cursor: pointer;
  transition: transform 150ms ease;
}

.volume-range::-webkit-slider-thumb:hover {
  transform: scale(1.25);
}

.volume-range::-moz-range-thumb {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  border: none;
  background: var(--color-primary);
  cursor: pointer;
}

@media (prefers-color-scheme: dark) {
  .seek-range {
    background: rgb(255 255 255 / 0.2);
  }

  .volume-range {
    background: rgb(255 255 255 / 0.2);
  }
}
</style>
