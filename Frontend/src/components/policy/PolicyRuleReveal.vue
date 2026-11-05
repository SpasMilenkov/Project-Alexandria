<template>
  <!-- Collapsed regions cancel the parent's gap-3 so hiding them leaves no stray spacing. -->
  <div
    class="grid transition-[grid-template-rows,margin-top,opacity] duration-200 ease-out motion-reduce:transition-none"
    :class="open ? 'grid-rows-[1fr] opacity-100' : '-mt-3 grid-rows-[0fr] opacity-0'"
    :inert="!open"
  >
    <div class="-m-1 min-h-0 overflow-hidden">
      <div class="p-1">
        <slot />
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  open: boolean;
}>();
</script>
