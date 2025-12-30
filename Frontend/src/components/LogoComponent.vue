<template>
  <router-link class="flex items-center gap-2" to="/">
    <div class="bg-primary p-2 rounded-lg">
      <Icon icon="heroicons:book-open" class="w-6 h-6 text-white" />
    </div>

    <span class="font-semibold text-lg">Alexandria</span>
  </router-link>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
</script>

<style scoped></style>
