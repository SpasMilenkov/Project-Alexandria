<template>
  <UBreadcrumb :items="items" class="p-4" />
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useDirectoryStore } from "@/stores/directory";

interface Breadcrumb {
  label: string;
  to: string;
}

const directoryStore = useDirectoryStore();

const items = computed(() => [
  { label: "/", to: "/dashboard/" },
  ...directoryStore.pathList.map(
    (d) =>
      ({
        label: d.dirName,
        to: `/dashboard/${d.id}`,
      }) as Breadcrumb,
  ),
]);
</script>

<style scoped></style>
