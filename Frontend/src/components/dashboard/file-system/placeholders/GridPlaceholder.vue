<template>
  <div class="flex items-center gap-12 p-4 flex-wrap">
    <USkeleton v-for="i in 16" :key="i" class="h-20 w-20 rounded-md" />
  </div>
</template>
<script setup lang="ts"></script>
<style scoped></style>
