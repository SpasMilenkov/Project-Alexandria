<script setup lang="ts">
import type { NavigationMenuItem } from '@nuxt/ui'
import { computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()

const items = computed<NavigationMenuItem[]>(() => [
  {
    label: 'Log in',
    to: '/auth',
    active: route.path.startsWith('/auth'),
  },
])
</script>

<template>
  <UApp>
    <UHeader>
      <template #title>Alexandria</template>
      <UNavigationMenu :items="items" />
      <template #right>
        <UColorModeButton />
      </template>
    </UHeader>
    <UMain>
      <UContainer class="flex justify-center">
        <RouterView />
      </UContainer>
    </UMain>
    <UFooter />
  </UApp>
</template>

<style scoped></style>
