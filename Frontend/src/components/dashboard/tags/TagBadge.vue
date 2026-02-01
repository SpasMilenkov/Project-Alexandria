<template>
  <UTooltip :text="tag.description ?? tag.name">
    <UBadge
      color="neutral"
      variant="outline"
      :label="tag.name"
      class="transition-colors duration-200 max-w-32"
      :icon="getIconByValue(tag.icon)"
      :style="{
        color: tag.color,
      }"
    >
      <template #trailing>
        <UButton
          icon="heroicons-x-mark"
          size="xs"
          variant="ghost"
          color="neutral"
          class="hover:cursor-pointer"
          v-if="fileId"
          @click="removeTagMutate({ fileId, tagId: tag.id })"
        />
      </template>
    </UBadge>
  </UTooltip>
</template>
<script setup lang="ts">
import type { TagDto } from "@/api/tag";
import { removeTagFromFile } from "@/mutations/tags";
import { getIconByValue } from "@/utils/icon.utils";

const { mutate: removeTagMutate } = removeTagFromFile();

defineProps<{
  tag: TagDto;
  fileId?: string;
}>();
</script>
<style scoped></style>
