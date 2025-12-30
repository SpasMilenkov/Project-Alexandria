<template>
  <div class="flex flex-col gap-4 h-full">
    <h1>This is just a placeholder, configurations are coming later!</h1>
    <SimpleSetup />
    <UButton
      block
      size="md"
      color="primary"
      variant="solid"
      @click="router.push('/dashboard')"
      >Finish setup!</UButton
    >
  </div>
</template>
<script setup lang="ts">
import router from "@/router";
import SimpleSetup from "@/components/setup/SimpleSetup,.vue";
</script>
<style scoped></style>
