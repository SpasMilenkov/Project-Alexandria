<script setup lang="ts">
import HomeClockStylePreview from "@/components/dashboard/home/HomeClockStylePreview.vue";
import HomeFileStylePreview from "@/components/dashboard/home/HomeFileStylePreview.vue";
import HomeGeneralStylePreview from "@/components/dashboard/home/HomeGeneralStylePreview.vue";
import HomeGreetingStylePreview from "@/components/dashboard/home/HomeGreetingStylePreview.vue";
import HomeLibraryStylePreview from "@/components/dashboard/home/HomeLibraryStylePreview.vue";
import HomeQuickStylePreview from "@/components/dashboard/home/HomeQuickStylePreview.vue";
import HomePlayerRecord from "@/components/dashboard/home/widgets/HomePlayerRecord.vue";
import {
  HomeClockStyle,
  HomeGreetingStyle,
  HomeLibraryStyle,
  HomeQuickAccessStyle,
  HomeQuickUploadStyle,
  HomeRecentFilesStyle,
  HomeStorageStyle,
  HomeWidgetType,
} from "@/enums/home-widget";

defineProps<{ type: HomeWidgetType; icon: string }>();
</script>

<template>
  <div
    class="flex h-16 items-center justify-center overflow-hidden text-gray-600 dark:text-gray-400"
    aria-hidden="true"
  >
    <span class="flex scale-70 items-center justify-center">
      <HomeClockStylePreview
        v-if="type === HomeWidgetType.Clock"
        :clock-style="HomeClockStyle.Minimal"
      />
      <HomeGreetingStylePreview
        v-else-if="type === HomeWidgetType.Greeting"
        :greeting-style="HomeGreetingStyle.Minimal"
      />
      <HomeFileStylePreview
        v-else-if="type === HomeWidgetType.Storage"
        :storage-style="HomeStorageStyle.Meter"
      />
      <HomeFileStylePreview
        v-else-if="type === HomeWidgetType.RecentFiles"
        :recent-style="HomeRecentFilesStyle.List"
      />
      <HomeQuickStylePreview
        v-else-if="type === HomeWidgetType.QuickAccess"
        :access-style="HomeQuickAccessStyle.Tiles"
      />
      <HomeLibraryStylePreview
        v-else-if="type === HomeWidgetType.LibraryShortcuts"
        :library-style="HomeLibraryStyle.Launchpad"
      />
      <HomeQuickStylePreview
        v-else-if="type === HomeWidgetType.QuickUpload"
        :upload-style="HomeQuickUploadStyle.DropZone"
      />
      <HomePlayerRecord
        v-else-if="type === HomeWidgetType.Player"
        :src="null"
        :loaded="false"
        :spinning="false"
        :icon="icon"
        class="size-20"
      />
      <HomeGeneralStylePreview
        v-else-if="
          type === HomeWidgetType.Countdown ||
          type === HomeWidgetType.FocusTimer ||
          type === HomeWidgetType.DailyCover
        "
        :type="type"
        :variant="0"
      />
      <UIcon v-else :name="icon" class="size-12" />
    </span>
  </div>
</template>
