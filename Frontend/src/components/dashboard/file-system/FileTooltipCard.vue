<template>
  <div class="tooltip-card">
    <!-- Header: icon + name -->
    <div class="tooltip-card__header">
      <div class="tooltip-card__icon-wrap">
        <Icon :icon="getFileIcon(data.fileName)" class="tooltip-card__icon" />
      </div>
      <div class="tooltip-card__name-block">
        <p class="tooltip-card__name">{{ data.fileName }}</p>
        <p class="tooltip-card__type">{{ readableType }}</p>
      </div>
    </div>

    <!-- Divider -->
    <div class="tooltip-card__divider" />

    <!-- Meta rows -->
    <dl class="tooltip-card__meta">
      <div class="tooltip-card__meta-row">
        <dt>
          <UIcon name="i-heroicons-scale" class="tooltip-card__meta-icon" />
          Size
        </dt>
        <dd>{{ readableSize }}</dd>
      </div>

      <div class="tooltip-card__meta-row">
        <dt>
          <UIcon name="i-heroicons-clock" class="tooltip-card__meta-icon" />
          Created
        </dt>
        <dd>{{ readableDate }}</dd>
      </div>

      <div class="tooltip-card__meta-row">
        <dt>
          <UIcon
            name="i-heroicons-archive-box"
            class="tooltip-card__meta-icon"
          />
          Version
        </dt>
        <dd>v{{ data.currentVersion.versionNumber }}</dd>
      </div>

      <div class="tooltip-card__meta-row">
        <dt>
          <UIcon name="i-heroicons-user" class="tooltip-card__meta-icon" />
          Owner
        </dt>
        <dd>{{ data.owner.name }}</dd>
      </div>
    </dl>

    <!-- Tags -->
    <template v-if="data.tags && data.tags.length > 0">
      <div class="tooltip-card__divider" />
      <div class="tooltip-card__tags">
        <span
          v-for="tag in data.tags.slice(0, 4)"
          :key="tag.id"
          class="tooltip-card__tag"
        >
          <Icon
            v-if="tag.icon"
            :icon="getIconByValue(tag.icon)"
            class="size-3"
          />
          {{ tag.name }}
        </span>
        <span
          v-if="data.tags.length > 4"
          class="tooltip-card__tag tooltip-card__tag--overflow"
        >
          +{{ data.tags.length - 4 }}
        </span>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { Icon } from "@iconify/vue";
import type { FileResult } from "@/api/file";
import { getFileIcon, getIconByValue } from "@/utils/icon.utils";
import { getFileTypeReadable } from "@/utils/mimetype.utils";
import { formatDate } from "@/utils/date-formatters";

const props = defineProps<{
  data: FileResult;
}>();

const readableType = computed(() =>
  getFileTypeReadable(props.data.currentVersion.mimeType, props.data.fileName),
);

const readableSize = computed(() => {
  const bytes = Number(props.data.currentVersion.size);
  if (!bytes) return "—";
  const units = ["B", "KB", "MB", "GB", "TB"];
  let size = bytes;
  let i = 0;
  while (size >= 1024 && i < units.length - 1) {
    size /= 1024;
    i++;
  }
  return `${size.toFixed(1)} ${units[i]}`;
});

const readableDate = computed(() => formatDate(props.data.createdAt));
</script>

<style scoped>
/* Card shell */
.tooltip-card {
  min-width: 220px;
  max-width: 280px;
  padding: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
}

/* Header */
.tooltip-card__header {
  display: flex;
  align-items: center;
  gap: 0.625rem;
}

.tooltip-card__icon-wrap {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2.25rem;
  height: 2.25rem;
  border-radius: 0.4rem;
  background: color-mix(in srgb, var(--ui-primary) 10%, transparent);
}

.tooltip-card__icon {
  width: 1.25rem;
  height: 1.25rem;
  color: var(--ui-primary);
}

.tooltip-card__name-block {
  min-width: 0;
  flex: 1;
}

.tooltip-card__name {
  font-size: 0.8125rem;
  font-weight: 600;
  line-height: 1.3;
  /* allow up to 2 lines before truncating */
  display: -webkit-box;
  -webkit-line-clamp: 2;
  line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}

.tooltip-card__type {
  font-size: 0.6875rem;
  color: var(--ui-text-muted);
  margin-top: 0.125rem;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

/*  Divider */
.tooltip-card__divider {
  height: 1px;
  background: var(--ui-border);
  margin: 0 -0.125rem;
}

/* Meta list */
.tooltip-card__meta {
  display: flex;
  flex-direction: column;
  gap: 0.3rem;
}

.tooltip-card__meta-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  font-size: 0.75rem;
}

.tooltip-card__meta-row dt {
  display: flex;
  align-items: center;
  gap: 0.3rem;
  color: var(--ui-text-muted);
  white-space: nowrap;
}

.tooltip-card__meta-icon {
  width: 0.75rem;
  height: 0.75rem;
  flex-shrink: 0;
}

.tooltip-card__meta-row dd {
  font-weight: 500;
  text-align: right;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 130px;
}

/* Tags */
.tooltip-card__tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.3rem;
}

.tooltip-card__tag {
  display: inline-flex;
  align-items: center;
  gap: 0.2rem;
  font-size: 0.6875rem;
  font-weight: 500;
  padding: 0.15rem 0.45rem;
  border-radius: 9999px;
  background: color-mix(in srgb, var(--ui-primary) 10%, transparent);
  color: var(--ui-primary);
  border: 1px solid color-mix(in srgb, var(--ui-primary) 20%, transparent);
  white-space: nowrap;
}

.tooltip-card__tag--overflow {
  background: color-mix(in srgb, var(--ui-text-muted) 10%, transparent);
  color: var(--ui-text-muted);
  border-color: color-mix(in srgb, var(--ui-text-muted) 20%, transparent);
}
</style>
