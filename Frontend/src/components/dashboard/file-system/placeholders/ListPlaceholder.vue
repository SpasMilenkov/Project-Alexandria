<template>
  <div class="flex items-center gap-4 p-4 flex-wrap">
    <div v-for="i in 12" :key="i" class="flex items-center gap-4">
      <USkeleton class="h-12 w-12 rounded-md" />
      <USkeleton class="h-4 w-screen" />
    </div>
  </div>
</template>
<script setup lang="ts"></script>
<style scoped></style>
