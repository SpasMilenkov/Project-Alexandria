import { defineStore } from "pinia";
import { computed, ref } from "vue";

import type { HomeEditorTarget, HomeSettings } from "@/types/home";

import { cloneHomeSettings, selectedHomeLayout } from "@/utils/home-layout";

export const useHomeStore = defineStore("home", () => {
  const ownerId = ref<string | null>(null);
  const ownerEpoch = ref(0);
  const settings = ref<HomeSettings | null>(null);
  const draft = ref<HomeSettings | null>(null);
  const baseline = ref<HomeSettings | null>(null);
  const target = ref<HomeEditorTarget>("desktop");

  const isEditing = computed(() => draft.value !== null);
  const showWidgetTitles = computed(
    () => (draft.value ?? settings.value)?.showWidgetTitles ?? false,
  );
  const isDirty = computed(() =>
    Boolean(draft.value && JSON.stringify(draft.value) !== JSON.stringify(baseline.value)),
  );
  const activeLayout = computed(() => {
    const source = draft.value ?? settings.value;

    return source ? selectedHomeLayout(source, target.value) : null;
  });

  const setOwner = (nextOwner: string | null) => {
    if (ownerId.value === nextOwner) return;

    ownerEpoch.value += 1;
    ownerId.value = nextOwner;
    settings.value = null;
    draft.value = null;
    baseline.value = null;
    target.value = "desktop";
  };

  const hydrate = (requestOwner: string, value: HomeSettings) => {
    if (ownerId.value !== requestOwner) return;

    settings.value = cloneHomeSettings(value);
  };

  const resetSessionState = () => {
    ownerEpoch.value += 1;
    ownerId.value = null;
    settings.value = null;
    draft.value = null;
    baseline.value = null;
    target.value = "desktop";
  };

  const beginEditing = (nextTarget: HomeEditorTarget) => {
    if (!settings.value) return;

    baseline.value = cloneHomeSettings(settings.value);
    draft.value = cloneHomeSettings(baseline.value);
    target.value = nextTarget;
  };

  const cancelEditing = () => {
    draft.value = null;
    baseline.value = null;
  };

  const commit = (requestOwner: string, value: HomeSettings) => {
    if (ownerId.value !== requestOwner) return;

    hydrate(requestOwner, value);
    draft.value = null;
    baseline.value = null;
  };

  return {
    activeLayout,
    baseline,
    beginEditing,
    cancelEditing,
    commit,
    draft,
    hydrate,
    isDirty,
    isEditing,
    ownerEpoch,
    ownerId,
    resetSessionState,
    settings,
    showWidgetTitles,
    setOwner,
    target,
  };
});
