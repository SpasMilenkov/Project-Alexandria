<script setup lang="ts">
import { useEventListener, useMediaQuery, useTimestamp } from "@vueuse/core";
import { computed, onUnmounted, ref, watch } from "vue";
import { onBeforeRouteLeave } from "vue-router";

import type { HomeWidgetSize, HomeWidgetType } from "@/enums/home-widget";
import type { HomeWidget } from "@/types/home";

import { isAuthError } from "@/api/shuffle";
import HomeDashboardGrid from "@/components/dashboard/home/HomeDashboardGrid.vue";
import HomeEditorControls from "@/components/dashboard/home/HomeEditorControls.vue";
import HomeEditorPanel from "@/components/dashboard/home/HomeEditorPanel.vue";
import HomeMobilePreview from "@/components/dashboard/home/HomeMobilePreview.vue";
import HomeWidgetCatalogue from "@/components/dashboard/home/HomeWidgetCatalogue.vue";
import HomeWidgetInspector from "@/components/dashboard/home/HomeWidgetInspector.vue";
import { HOME_WIDGET_REGISTRY } from "@/components/dashboard/home/registry";
import { useHomeSettings } from "@/composables/useHomeSettings";
import { useAuthStore } from "@/stores/auth";
import { useHomeStore } from "@/stores/home";
import { firstFreeHomeCell, homeWidgetFootprint, placeHomeWidget } from "@/utils/home-grid";
import {
  MAX_HOME_WIDGETS,
  createHomeWidget,
  defaultHomeLayout,
  selectedHomeLayout,
  setSeparateMobileLayout,
} from "@/utils/home-layout";
import { glassModalContent } from "@/utils/modalUi";

const auth = useAuthStore();
const store = useHomeStore();
const { error, isLoading, isSaving, refresh, save } = useHomeSettings();

const isMobile = useMediaQuery("(max-width: 767px)");
const timestamp = useTimestamp({ interval: 60_000 });

const catalogueOpen = ref(false);
const controlsOpen = ref(false);
const previewing = ref(false);
const wideEditor = useMediaQuery("(min-width: 1024px)");

const saveError = ref<string | null>(null);
const announcement = ref("");
const exitDialogOpen = ref(false);

const dragActive = ref(false);
const selectedId = ref<string | null>(null);
const grid = ref<InstanceType<typeof HomeDashboardGrid> | null>(null);

let exitRequest: Promise<boolean> | null = null;
let resolveExit: ((leave: boolean) => void) | null = null;

const greeting = computed(() => {
  const hour = new Date(timestamp.value).getHours();

  if (hour < 12) return "Good morning";
  if (hour < 18) return "Good afternoon";

  return "Good evening";
});

const layout = computed(() => {
  if (store.isEditing) return store.activeLayout;
  if (!store.settings) return null;

  return selectedHomeLayout(store.settings, isMobile.value ? "mobile" : "desktop");
});

const sharedPreview = computed(
  () => store.isEditing && store.target === "mobile" && !store.draft?.useSeparateMobileLayout,
);

const canEdit = computed(
  () => store.isEditing && !sharedPreview.value && !isSaving.value && !previewing.value,
);

const showError = computed(() => error.value && !isAuthError(error.value));

const canAdd = computed(
  () => canEdit.value && (layout.value?.widgets.length ?? 0) < MAX_HOME_WIDGETS,
);

const mobileEditor = computed(() => store.isEditing && store.target === "mobile");

const catalogueDocked = computed(() => store.isEditing && wideEditor.value);

const catalogueDisabledReason = computed(() => {
  if (sharedPreview.value) return "Enable a separate mobile layout to add widgets here.";
  if (isSaving.value) return "Your layout is being saved.";

  return "This layout has reached its widget limit.";
});

watch(catalogueDocked, (docked) => {
  if (!docked) catalogueOpen.value = false;
  else if (mobileEditor.value) catalogueOpen.value = true;
});

watch(
  () => store.target,
  () => {
    catalogueOpen.value = mobileEditor.value && catalogueDocked.value;
    grid.value?.cancel();
  },
);

watch(
  () => layout.value?.widgets.map((widget) => widget.instanceId).join(","),
  () => {
    if (!layout.value?.widgets.some((widget) => widget.instanceId === selectedId.value))
      selectedId.value = layout.value?.widgets[0]?.instanceId ?? null;
  },
);

watch(
  () => store.ownerId,
  () => {
    controlsOpen.value = false;
    previewing.value = false;
    catalogueOpen.value = false;
    saveError.value = null;
    finishExit(false);
  },
);

const beginEditing = () => {
  saveError.value = null;

  const target = isMobile.value && store.settings?.useSeparateMobileLayout ? "mobile" : "desktop";

  previewing.value = false;
  store.beginEditing(target);
};

const togglePreview = () => {
  grid.value?.cancel();
  catalogueOpen.value = false;
  previewing.value = !previewing.value;
};

watch(wideEditor, () => {
  previewing.value = false;
  controlsOpen.value = false;
});

const cancel = () => {
  store.cancelEditing();
  controlsOpen.value = false;
  previewing.value = false;
  catalogueOpen.value = false;
  saveError.value = null;
};

const finishExit = (leave: boolean) => {
  const resolve = resolveExit;

  resolveExit = null;
  exitRequest = null;
  exitDialogOpen.value = false;

  if (leave) cancel();

  resolve?.(leave);
};

const requestExit = (): Promise<boolean> => {
  if (isSaving.value) return Promise.resolve(false);
  if (!store.isEditing) return Promise.resolve(true);
  if (exitRequest) return exitRequest;

  exitDialogOpen.value = true;
  exitRequest = new Promise<boolean>((resolve) => {
    resolveExit = resolve;
  });

  return exitRequest;
};

watch(exitDialogOpen, (open) => {
  if (!open && resolveExit) finishExit(false);
});

const saveLayout = async () => {
  const epoch = store.ownerEpoch;
  saveError.value = null;

  try {
    await save();

    if (epoch !== store.ownerEpoch) return false;

    catalogueOpen.value = false;
    controlsOpen.value = false;
    previewing.value = false;

    return true;
  } catch (cause) {
    if (epoch === store.ownerEpoch && !isAuthError(cause))
      saveError.value =
        "Your layout could not be saved. Your changes are still here; try Done again.";

    return false;
  }
};

const saveAndExit = async () => {
  if (isSaving.value) return;

  if (await saveLayout()) finishExit(true);
};

const toggleSeparate = (enabled: boolean) => {
  if (store.draft) setSeparateMobileLayout(store.draft, enabled);
};

const addWidget = (type: HomeWidgetType) => {
  if (!canAdd.value || !layout.value) return;

  const widget = createHomeWidget(type, crypto.randomUUID());
  widget.options = { ...HOME_WIDGET_REGISTRY[type].defaultOptions };
  Object.assign(widget, homeWidgetFootprint(widget.size, layout.value.columns));

  const position = firstFreeHomeCell(layout.value, widget);

  if (!position) return;

  widget.x = Math.min(position.x, layout.value.columns - widget.width);
  widget.y = position.y;

  if (!placeHomeWidget(layout.value, widget)) return;

  selectedId.value = widget.instanceId;
  if (!catalogueDocked.value) catalogueOpen.value = false;
  announcement.value = `${HOME_WIDGET_REGISTRY[type].label} added.`;
};

const startCatalogueDrag = (type: HomeWidgetType, event: PointerEvent) => {
  if (canAdd.value) grid.value?.startCatalogueDrag(type, event);
};

const placeWidget = (widget: HomeWidget) => {
  if (!canEdit.value || !layout.value) return;

  if (placeHomeWidget(layout.value, widget)) {
    selectedId.value = widget.instanceId;
    announcement.value = `Widget placed at column ${widget.x + 1}, row ${widget.y + 1}.`;
  } else {
    announcement.value = "That placement does not fit. Your layout has not changed.";
  }
};

const nudgeWidget = (instanceId: string, x: number, y: number) => {
  const widget = layout.value?.widgets.find((item) => item.instanceId === instanceId);

  if (widget) placeWidget({ ...widget, x: widget.x + x, y: widget.y + y });
};

const removeWidget = (instanceId: string) => {
  if (!canEdit.value || !layout.value) return;

  layout.value.widgets = layout.value.widgets.filter((widget) => widget.instanceId !== instanceId);
  announcement.value = "Widget removed.";
};

const changeWidget = (widget: HomeWidget) => {
  if (!canEdit.value || !layout.value) return;

  const index = layout.value.widgets.findIndex((item) => item.instanceId === widget.instanceId);

  if (index < 0) return;

  placeWidget(widget);
};

const applyPreset = (instanceId: string, size: HomeWidgetSize) => {
  const widget = layout.value?.widgets.find((item) => item.instanceId === instanceId);

  if (!widget || !layout.value || !canEdit.value) return;

  const footprint = homeWidgetFootprint(size, layout.value.columns);

  placeWidget({
    ...widget,
    ...footprint,
    size,
    x: Math.min(widget.x, layout.value.columns - footprint.width),
  });
};

const resetLayout = () => {
  if (!canEdit.value || !layout.value) return;

  layout.value.widgets = defaultHomeLayout(layout.value.columns).widgets;
  announcement.value = "This layout has been reset. Press Done to save.";
};

onBeforeRouteLeave(async () => {
  if (!(await requestExit())) return false;

  cancel();

  return true;
});

useEventListener(window, "keydown", (event) => {
  if (event.defaultPrevented || isSaving.value) return;

  if (event.key === "Enter" && exitDialogOpen.value) {
    event.preventDefault();
    void saveAndExit();

    return;
  }

  if (event.key === "Escape" && catalogueOpen.value && catalogueDocked.value) {
    event.preventDefault();
    catalogueOpen.value = false;

    return;
  }

  if (event.key !== "Escape" || !store.isEditing || catalogueOpen.value || exitDialogOpen.value)
    return;
  if (document.querySelector('[role="dialog"], [role="listbox"]')) return;

  event.preventDefault();
  void requestExit();
});

useEventListener(window, "beforeunload", (event) => {
  if (!store.isDirty && !isSaving.value) return;

  event.preventDefault();
});

onUnmounted(() => {
  finishExit(false);
  store.cancelEditing();
});
</script>

<template>
  <main
    class="flex w-full flex-col gap-6 px-4 pt-6 pb-[calc(6rem+env(safe-area-inset-bottom))] md:px-6 md:py-8"
  >
    <header
      class="flex flex-wrap items-center justify-between gap-4"
      :class="{ 'frosted-glass glass-surface sticky top-0 z-20 rounded-2xl p-4': store.isEditing }"
    >
      <div>
        <h1
          class="text-lg font-semibold tracking-tight md:text-2xl text-gray-900 dark:text-gray-100"
        >
          <template v-if="previewing">Dashboard preview</template>
          <template v-else-if="store.isEditing">Arrange your dashboard</template>
          <template v-else>
            {{ greeting
            }}<template v-if="auth.user?.user.name">, {{ auth.user.user.name }}</template>
          </template>
        </h1>
        <p v-if="!store.isEditing" class="mt-2 text-sm text-gray-600 dark:text-gray-400">
          Everything in your library, arranged your way.
        </p>
      </div>

      <div v-if="store.settings" class="flex flex-wrap gap-2">
        <template v-if="store.isEditing">
          <UButton
            v-if="wideEditor"
            color="neutral"
            variant="outline"
            :aria-pressed="previewing"
            :disabled="isSaving || dragActive"
            @click="togglePreview"
            >{{ previewing ? "Back to editing" : "Preview" }}</UButton
          >

          <UButton
            v-if="!wideEditor"
            color="neutral"
            variant="outline"
            icon="i-heroicons-cog-6-tooth"
            aria-label="Layout controls"
            :disabled="isSaving || dragActive"
            @click="controlsOpen = true"
            >Controls</UButton
          >

          <UButton
            color="neutral"
            variant="outline"
            v-if="!previewing"
            icon="i-heroicons-plus"
            aria-label="Add widget"
            :disabled="!canAdd"
            @click="catalogueOpen = true"
            ><span class="hidden sm:inline">Add widget</span></UButton
          >

          <UButton
            color="neutral"
            variant="outline"
            :disabled="isSaving || dragActive"
            @click="requestExit"
            >Cancel</UButton
          >

          <UButton color="primary" :loading="isSaving" :disabled="dragActive" @click="saveLayout"
            >Done</UButton
          >
        </template>

        <UButton
          v-else
          color="neutral"
          variant="outline"
          icon="i-heroicons-cog-6-tooth"
          @click="beginEditing"
          >Customize</UButton
        >
      </div>
    </header>

    <div
      v-if="!store.settings && (isLoading || !showError)"
      class="flex min-h-48 items-center justify-center"
      role="status"
      aria-label="Loading Home"
    >
      <UIcon name="i-mdi-loading" class="size-8 animate-spin text-gray-500 dark:text-gray-500" />
    </div>

    <UAlert
      v-else-if="!store.settings && showError"
      color="error"
      variant="soft"
      title="Home could not be loaded"
      :description="error instanceof Error ? error.message : 'Check your connection and try again.'"
    >
      <template #actions
        ><UButton color="neutral" variant="outline" @click="refresh()">Try again</UButton></template
      >
    </UAlert>

    <template v-else-if="store.settings && layout">
      <UAlert
        v-if="saveError"
        color="error"
        variant="soft"
        title="Changes are not saved"
        :description="saveError"
      />

      <div class="home-editor-container">
        <div
          class="home-editor-workspace"
          :class="{ 'has-controls': catalogueDocked && !previewing }"
        >
          <component
            :is="mobileEditor ? HomeMobilePreview : 'div'"
            v-bind="mobileEditor ? { shared: sharedPreview, dropActive: dragActive } : {}"
            :data-preview="store.isEditing && !mobileEditor ? 'desktop' : undefined"
          >
            <HomeDashboardGrid
              v-if="layout.widgets.length || (store.isEditing && !previewing)"
              ref="grid"
              :layout="layout"
              :editing="(canEdit || isSaving) && !previewing"
              :disabled="isSaving"
              :mobile="mobileEditor || (!store.isEditing && isMobile)"
              :selected-id="selectedId"
              @select="selectedId = $event"
              @place="placeWidget"
              @nudge="nudgeWidget"
              @announce="announcement = $event"
              @dragging="dragActive = $event"
            />

            <div
              v-else
              class="frosted-glass glass-surface flex flex-col items-center gap-4 rounded-2xl border border-dashed border-gray-200/70 p-8 text-center dark:border-gray-700/70"
            >
              <UIcon
                name="i-heroicons-book-open"
                class="size-12 text-gray-400 dark:text-gray-600"
              />
              <div>
                <h2 class="font-semibold text-gray-900 dark:text-gray-100">
                  Your dashboard is empty
                </h2>
                <p class="mt-2 text-sm text-gray-600 dark:text-gray-400">
                  Add a widget to make this space yours.
                </p>
              </div>

              <UButton v-if="!store.isEditing" color="primary" @click="beginEditing"
                >Customize Home</UButton
              >
              <UButton
                v-else-if="!previewing"
                color="primary"
                :disabled="!canAdd"
                @click="catalogueOpen = true"
                >Add a widget</UButton
              >
            </div>
          </component>

          <HomeEditorPanel
            v-if="store.isEditing && !previewing"
            v-model:open="controlsOpen"
            :docked="catalogueDocked"
          >
            <HomeEditorControls
              v-if="store.isEditing && store.draft"
              :target="store.target"
              :separate-mobile="store.draft.useSeparateMobileLayout"
              :disabled="isSaving || dragActive"
              @target="store.target = $event"
              @separate="toggleSeparate"
              @reset="resetLayout"
            />

            <HomeWidgetInspector
              v-if="canEdit || isSaving"
              :layout="layout"
              :selected-id="selectedId"
              :disabled="isSaving || dragActive"
              @select="selectedId = $event"
              @change="changeWidget"
              @preset="applyPreset"
              @remove="removeWidget"
              @nudge="nudgeWidget"
            />

            <template v-if="catalogueDocked">
              <HomeWidgetCatalogue
                v-model:open="catalogueOpen"
                :disabled="!canAdd"
                :disabled-reason="catalogueDisabledReason"
                :docked="catalogueDocked"
                @add="addWidget"
                @pointer-drag="startCatalogueDrag"
              />
            </template>
          </HomeEditorPanel>

          <HomeWidgetCatalogue
            v-if="!catalogueDocked && !previewing"
            v-model:open="catalogueOpen"
            :disabled="!canAdd"
            :disabled-reason="catalogueDisabledReason"
            @add="addWidget"
          />
        </div>
      </div>
    </template>

    <UModal
      v-model:open="exitDialogOpen"
      title="Leave customization?"
      :description="
        store.isDirty
          ? 'Save your layout or discard your changes before leaving.'
          : 'Your layout has no changes. Choose whether to leave customization.'
      "
      :dismissible="!isSaving"
      :close="!isSaving"
      :ui="{ content: glassModalContent }"
    >
      <template #body>
        <UAlert
          v-if="saveError"
          color="error"
          variant="soft"
          title="Changes are not saved"
          :description="saveError"
        />

        <p v-else class="text-sm text-gray-600 dark:text-gray-400">Press Enter to save and exit.</p>
      </template>

      <template #footer>
        <div class="flex w-full flex-wrap justify-end gap-2">
          <UButton color="neutral" variant="outline" :disabled="isSaving" @click="finishExit(false)"
            >Keep editing</UButton
          >

          <UButton color="error" variant="outline" :disabled="isSaving" @click="finishExit(true)"
            >Discard and exit</UButton
          >

          <UButton color="primary" :loading="isSaving" @click="saveAndExit">Save changes</UButton>
        </div>
      </template>
    </UModal>

    <p class="sr-only" aria-live="polite">{{ announcement }}</p>
  </main>
</template>

<style scoped>
.home-editor-container {
  container-name: home-editor;
  container-type: inline-size;
}

.home-editor-workspace {
  display: grid;
  gap: 2rem;
  align-items: start;
}

@media (min-width: 1024px) {
  .home-editor-workspace.has-controls {
    grid-template-columns: minmax(0, 1fr) 320px;
  }
}
</style>
