import type { SelectMenuItem } from "@nuxt/ui";

import { ref } from "vue";

import type { DirectoryOption } from "@/types/directory-option";

import { useDirectoryStore } from "@/stores/directory";

export const useParentDirectoryPicker = (initialId?: string, initialName?: string) => {
  const directoryStore = useDirectoryStore();
  const selectedDirectoryId = ref<string | null>(initialId ?? null);
  const parentDirectoryOptions = ref<DirectoryOption[]>(
    initialId && initialName ? [{ id: initialId, label: initialName }] : [],
  );
  const isLoadingParentDirs = ref(false);

  const searchParentDirectory = async (query: string) => {
    if (!query.trim()) return;

    isLoadingParentDirs.value = true;
    try {
      const response = await directoryStore.searchDirectory({
        isDeleted: false,
        nameContains: query,
        pageSize: 20,
      });

      if (response.success && response.data) {
        const newOptions = response.data.items.map((d) => ({ id: d.id, label: d.name }));
        const selectedId = selectedDirectoryId.value;
        const kept = parentDirectoryOptions.value.find((o) => o.id === selectedId);
        parentDirectoryOptions.value = kept
          ? [kept, ...newOptions.filter((o) => o.id !== selectedId)]
          : newOptions;
      }
    } finally {
      isLoadingParentDirs.value = false;
    }
  };
  return { searchParentDirectory, parentDirectoryOptions, isLoadingParentDirs };
};
