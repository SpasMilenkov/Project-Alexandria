<script setup lang="ts">
const emit = defineEmits<{ close: [] }>();

interface Shortcut {
  keys: string[];
  description: string;
}

interface ShortcutSection {
  title: string;
  shortcuts: Shortcut[];
}

const shortcutSections: ShortcutSection[] = [
  {
    title: "File Explorer",
    shortcuts: [
      {
        keys: ["meta", "C"],
        description: "Copy selected files/folders",
      },
      {
        keys: ["meta", "X"],
        description: "Cut selected files/folders",
      },
      {
        keys: ["meta", "V"],
        description: "Paste copied/cut items",
      },
      {
        keys: ["Delete"],
        description: "Delete selected items",
      },
    ],
  },
  {
    title: "Tabs",
    shortcuts: [
      {
        keys: ["meta", "shift", "N"],
        description: "Create new tab",
      },
      {
        keys: ["meta", "shift", "Q"],
        description: "Close active tab",
      },
    ],
  },
  {
    title: "Tags",
    shortcuts: [
      {
        keys: ["meta", "A"],
        description: "Select all tags",
      },
      {
        keys: ["Escape"],
        description: "Clear selection",
      },
      {
        keys: ["Delete"],
        description: "Delete selected tags",
      },
    ],
  },
];
</script>

<template>
  <UModal :close="{ onClick: () => emit('close') }" title="Keyboard Shortcuts">
    <template #body>
      <div class="space-y-6">
        <div
          v-for="section in shortcutSections"
          :key="section.title"
          class="space-y-3"
        >
          <h3 class="font-semibold text-sm opacity-70">{{ section.title }}</h3>
          <div class="space-y-2">
            <div
              v-for="(shortcut, index) in section.shortcuts"
              :key="index"
              class="flex items-center justify-between py-2 px-3 rounded-md hover:bg-elevated transition-colors"
            >
              <span class="text-sm">{{ shortcut.description }}</span>
              <div class="flex items-center gap-1">
                <UKbd
                  v-for="(key, keyIndex) in shortcut.keys"
                  :key="keyIndex"
                  :value="key"
                  size="sm"
                />
              </div>
            </div>
          </div>
        </div>
      </div>

      <div class="flex justify-end mt-6 pt-4 border-t border-t-primary">
        <UButton color="neutral" label="Close" @click="emit('close')" />
      </div>
    </template>
  </UModal>
</template>
