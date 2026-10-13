<script setup lang="ts">
import { useEventListener, useMediaQuery, useTimestamp } from "@vueuse/core";
import { computed, defineAsyncComponent, nextTick, onUnmounted, ref, watch } from "vue";
import { onBeforeRouteLeave } from "vue-router";

import type { HomeWidgetSize, HomeWidgetType } from "@/enums/home-widget";
import type { HomeEditorTarget, HomeWidget, HomeWidgetOptions } from "@/types/home";

import { isAuthError } from "@/api/shuffle";
import HomeDashboardGrid from "@/components/dashboard/home/HomeDashboardGrid.vue";
import HomeEditorControls from "@/components/dashboard/home/HomeEditorControls.vue";
import HomeEditorPanel from "@/components/dashboard/home/HomeEditorPanel.vue";
import HomeLoadError from "@/components/dashboard/home/HomeLoadError.vue";
import HomeMobilePreview from "@/components/dashboard/home/HomeMobilePreview.vue";
import HomeWidgetCatalogue from "@/components/dashboard/home/HomeWidgetCatalogue.vue";
import HomeWidgetInspector from "@/components/dashboard/home/HomeWidgetInspector.vue";
import { HOME_WIDGET_REGISTRY } from "@/components/dashboard/home/registry";
import { useHomeEditorHistory } from "@/composables/useHomeEditorHistory";
import { useHomeSettings } from "@/composables/useHomeSettings";
import { useAuthStore } from "@/stores/auth";
import { useHomeStore } from "@/stores/home";
import { homeClockGreeting } from "@/utils/home-clock";
import {
  firstFreeHomeCell,
  homeSpatialOrder,
  homeWidgetFootprint,
  placeHomeWidget,
} from "@/utils/home-grid";
import { homeEditorKeyboardBlocked, homeResizeByCells } from "@/utils/home-grid-input";
import {
  MAX_HOME_WIDGETS,
  createHomeWidget,
  defaultHomeLayout,
  selectedHomeLayout,
  setSeparateMobileLayout,
  updateHomeWidgetOptions,
} from "@/utils/home-layout";
import { cloneHomeWidgetOptions } from "@/utils/home-widget-options";
import { glassModalContent } from "@/utils/modalUi";

const HomeWidgetShowcase = defineAsyncComponent(
  () => import("@/components/dashboard/home/HomeWidgetShowcase.vue"),
);

const auth = useAuthStore();
const store = useHomeStore();
const { error, isLoading, isSaving, refresh, save } = useHomeSettings();

const isMobile = useMediaQuery("(max-width: 767px)");
const timestamp = useTimestamp({ interval: 60_000 });

const catalogueOpen = ref(false);
const showcaseOpen = ref(false);
const controlsOpen = ref(false);
const previewing = ref(false);
const wideEditor = useMediaQuery("(min-width: 768px)");
const hoveredId = ref<string | null>(null);

const saveError = ref<string | null>(null);
const announcement = ref("");
const exitDialogOpen = ref(false);

const dragActive = ref(false);
const selectedId = ref<string | null>(null);
const grid = ref<InstanceType<typeof HomeDashboardGrid> | null>(null);

const history = useHomeEditorHistory({
  session: () => `${store.ownerEpoch}:${store.isEditing}`,
  read: () => store.draft,
  write: (settings) => {
    store.draft = settings;
  },
  context: () => ({ target: store.target, selectedId: selectedId.value }),
  restoreContext: (context) => {
    store.target = context.target;
    selectedId.value = context.selectedId;
  },
});

const { canUndo, canRedo } = history;

let exitRequest: Promise<boolean> | null = null;
let resolveExit: ((leave: boolean) => void) | null = null;

const greeting = computed(() => homeClockGreeting(new Date(timestamp.value).getHours()));

const showHeaderGreeting = computed(
  () => (store.draft ?? store.settings)?.showHeaderGreeting ?? true,
);

const layout = computed(() => {
  if (store.isEditing) return store.activeLayout;
  if (!store.settings) return null;

  return selectedHomeLayout(store.settings, isMobile.value ? "mobile" : "desktop");
});

const sharedPreview = computed(
  () => store.isEditing && store.target === "mobile" && !store.draft?.useSeparateMobileLayout,
);

const canEdit = computed(
  () =>
    store.isEditing &&
    !sharedPreview.value &&
    !isSaving.value &&
    !previewing.value &&
    !showcaseOpen.value,
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

watch(
  () => store.target,
  () => {
    catalogueOpen.value = false;
    selectedId.value = null;
    hoveredId.value = null;
    grid.value?.cancel();
  },
  { flush: "sync" },
);

watch(
  () => layout.value?.widgets.map((widget) => widget.instanceId).join(","),
  () => {
    if (!layout.value?.widgets.some((widget) => widget.instanceId === selectedId.value))
      selectedId.value = null;
  },
);

watch(
  () => store.ownerId,
  () => {
    showcaseOpen.value = false;
    controlsOpen.value = false;
    previewing.value = false;
    catalogueOpen.value = false;
    saveError.value = null;
    finishExit(false);
  },
);

const beginEditing = () => {
  showcaseOpen.value = false;
  saveError.value = null;

  const target = isMobile.value && store.settings?.useSeparateMobileLayout ? "mobile" : "desktop";

  previewing.value = false;
  selectedId.value = null;
  store.beginEditing(target);
};

const togglePreview = () => {
  grid.value?.cancel();
  catalogueOpen.value = false;
  previewing.value = !previewing.value;
};

const openShowcase = () => {
  if (isSaving.value || dragActive.value) return;

  grid.value?.cancel();
  history.endGroup();
  catalogueOpen.value = false;
  showcaseOpen.value = true;
};

const closeShowcase = async () => {
  showcaseOpen.value = false;

  await nextTick();
  document.querySelector<HTMLButtonElement>('button[aria-label="Add widget"]')?.focus();
};

watch(wideEditor, () => {
  previewing.value = false;
  controlsOpen.value = false;
});

const DISMISS_GUARD_MS = 600;

let drawerDismissedAt = 0;

watch(controlsOpen, (isOpen, wasOpen) => {
  if (wasOpen && !isOpen) drawerDismissedAt = Date.now();
});

const recentlyDismissed = () =>
  !controlsOpen.value && Date.now() - drawerDismissedAt < DISMISS_GUARD_MS;

const cancel = () => {
  showcaseOpen.value = false;
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
    showcaseOpen.value = false;

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
  if (!store.draft || isSaving.value || dragActive.value) return;

  history.record(() => setSeparateMobileLayout(store.draft!, enabled));
};

const addWidget = (type: HomeWidgetType) => {
  if (!canAdd.value || !layout.value) return;

  const widget = createHomeWidget(type, crypto.randomUUID(), layout.value.columns);

  const position = firstFreeHomeCell(layout.value, widget);

  if (!position) return;

  history.record(() => {
    layout.value!.widgets = [...layout.value!.widgets, { ...widget, ...position }].sort(
      homeSpatialOrder,
    );
    selectedId.value = widget.instanceId;
  });
  catalogueOpen.value = false;
  if (!wideEditor.value && !recentlyDismissed()) controlsOpen.value = true;
  announcement.value = `${HOME_WIDGET_REGISTRY[type].label} added.`;
};

const startCatalogueDrag = (type: HomeWidgetType, event: PointerEvent) => {
  if (canAdd.value) grid.value?.startCatalogueDrag(type, event);
};

const placeWidget = (widget: HomeWidget, group?: string) => {
  if (!canEdit.value || !layout.value) return;

  const added = !layout.value.widgets.some((item) => item.instanceId === widget.instanceId);

  history.record(() => {
    if (placeHomeWidget(layout.value!, widget)) {
      if (added) catalogueOpen.value = false;
      selectedId.value = widget.instanceId;
      announcement.value = `Widget placed at column ${widget.x + 1}, row ${widget.y + 1}.`;
    } else {
      announcement.value = "That placement does not fit. Your layout has not changed.";
    }
  }, group);
};

const selectWidget = async (instanceId: string, reveal = false, silent = false) => {
  selectedId.value = instanceId;
  hoveredId.value = null;

  if (!wideEditor.value && !silent && !recentlyDismissed()) controlsOpen.value = true;

  if (!reveal) return;

  await nextTick();
  grid.value?.$el
    .querySelector(`[data-widget-id="${CSS.escape(instanceId)}"]`)
    ?.scrollIntoView({ block: "nearest", behavior: "smooth" });
};

const changeTarget = (target: HomeEditorTarget) => {
  if (isSaving.value || dragActive.value) return;

  history.endGroup();
  store.target = target;
};

const resizeWidget = (instanceId: string, x: number, y: number, group?: string) => {
  const widget = layout.value?.widgets.find((item) => item.instanceId === instanceId);

  if (!widget || !layout.value) return;

  placeWidget(homeResizeByCells(widget, layout.value.columns, "bottom-right", x, y), group);
};

const nudgeWidget = (instanceId: string, x: number, y: number, group?: string) => {
  const widget = layout.value?.widgets.find((item) => item.instanceId === instanceId);

  if (widget) placeWidget({ ...widget, x: widget.x + x, y: widget.y + y }, group);
};

const duplicateWidget = async (instanceId: string) => {
  const widget = layout.value?.widgets.find((item) => item.instanceId === instanceId);

  if (!canAdd.value || !widget || !layout.value) return;

  const position = firstFreeHomeCell(layout.value, widget);

  if (!position) {
    announcement.value = "There is no room for a duplicate in this layout.";
    return;
  }

  const copy = {
    ...widget,
    ...position,
    instanceId: crypto.randomUUID(),
    options: cloneHomeWidgetOptions(widget.options),
  };

  history.record(() => {
    layout.value!.widgets = [...layout.value!.widgets, copy].sort(homeSpatialOrder);
    selectedId.value = copy.instanceId;
  });

  announcement.value = `${HOME_WIDGET_REGISTRY[widget.type].label} duplicated. Press Done to save.`;

  await nextTick();
  const card = grid.value?.$el.querySelector(`[data-widget-id="${CSS.escape(copy.instanceId)}"]`);
  card?.focus({ preventScroll: true });
  card?.scrollIntoView({ block: "nearest" });
};

const removeWidget = (instanceId: string) => {
  if (!canEdit.value || !layout.value) return;

  const ordered = [...layout.value.widgets].sort(homeSpatialOrder);
  const index = ordered.findIndex((widget) => widget.instanceId === instanceId);

  if (index < 0) return;

  grid.value?.cancel();
  history.record(() => {
    layout.value!.widgets = layout.value!.widgets.filter(
      (widget) => widget.instanceId !== instanceId,
    );

    if (selectedId.value === instanceId)
      selectedId.value = ordered[index + 1]?.instanceId ?? ordered[index - 1]?.instanceId ?? null;
  });

  announcement.value = "Widget removed.";
};

const changeOptions = (instanceId: string, options: Partial<HomeWidgetOptions>) => {
  if (!canEdit.value || !layout.value) return;

  history.record(() => {
    if (updateHomeWidgetOptions(layout.value!, instanceId, options))
      announcement.value = "Widget options updated. Press Done to save.";
  });
};

const changeTitle = (instanceId: string, showTitle: boolean | null) => {
  if (!canEdit.value || !layout.value) return;

  const widget = layout.value.widgets.find((item) => item.instanceId === instanceId);

  if (widget)
    history.record(() => {
      widget.showTitle = showTitle;
    });
};

const changeTitles = (showWidgetTitles: boolean) => {
  if (store.draft && !isSaving.value && !dragActive.value)
    history.record(() => {
      store.draft!.showWidgetTitles = showWidgetTitles;
    });
};

const changeHeaderGreeting = (show: boolean) => {
  if (store.draft && !isSaving.value && !dragActive.value)
    history.record(() => {
      store.draft!.showHeaderGreeting = show;
    });
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

  history.record(() => {
    layout.value!.widgets = defaultHomeLayout(layout.value!.columns).widgets;
  });
  announcement.value = "This layout has been reset. Press Done to save.";
};

const changeHistory = async (redo = false, focus = false) => {
  if (
    !store.isEditing ||
    isSaving.value ||
    dragActive.value ||
    previewing.value ||
    showcaseOpen.value ||
    exitDialogOpen.value
  )
    return;

  grid.value?.cancel();

  const changed = redo ? history.redo() : history.undo();

  if (!changed) return;

  catalogueOpen.value = false;
  announcement.value = redo ? "Change redone." : "Change undone.";

  if (!focus || !selectedId.value) return;

  await nextTick();
  grid.value?.$el
    .querySelector(`[data-widget-id="${CSS.escape(selectedId.value)}"]`)
    ?.focus({ preventScroll: true });
};

useEventListener(window, "pointerdown", history.endGroup, { capture: true });
useEventListener(window, "keyup", history.endGroup);
useEventListener(window, "blur", history.endGroup);

useEventListener(window, "keydown", (event) => {
  if (
    event.defaultPrevented ||
    showcaseOpen.value ||
    !store.isEditing ||
    event.altKey ||
    event.repeat ||
    homeEditorKeyboardBlocked(event)
  )
    return;
  if (!event.ctrlKey && !event.metaKey) return;

  const key = event.key.toLowerCase();

  if (key === "z" || (key === "y" && event.ctrlKey && !event.shiftKey)) {
    event.preventDefault();
    void changeHistory(key === "y" || event.shiftKey, true);
  } else if (
    key === "d" &&
    !event.shiftKey &&
    canEdit.value &&
    !dragActive.value &&
    selectedId.value
  ) {
    event.preventDefault();
    duplicateWidget(selectedId.value);
  }
});

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

  if (event.key === "Escape" && catalogueOpen.value) {
    event.preventDefault();
    catalogueOpen.value = false;

    return;
  }

  if (event.key === "Escape" && controlsOpen.value && !wideEditor.value && !showcaseOpen.value) {
    event.preventDefault();
    controlsOpen.value = false;

    return;
  }

  if (event.key !== "Escape" || !store.isEditing || catalogueOpen.value || exitDialogOpen.value)
    return;
  const overlays = document.querySelectorAll('[role="dialog"], [role="listbox"]');

  if ([...overlays].some((overlay) => overlay.checkVisibility({ visibilityProperty: true })))
    return;

  event.preventDefault();
  if (showcaseOpen.value) {
    void closeShowcase();

    return;
  }

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
  <main class="home-page w-full min-w-0" :class="{ 'is-editing': store.isEditing }">
    <header
      class="home-toolbar flex shrink-0 flex-wrap items-center gap-2 border-gray-200/70 dark:border-gray-700/70"
      :class="{ 'frosted-glass glass-surface border-b': store.isEditing }"
    >
      <div
        class="home-heading min-w-0"
        :class="{ 'sr-only': !store.isEditing && !showHeaderGreeting }"
      >
        <h1
          class="truncate font-semibold tracking-tight text-gray-900 dark:text-gray-100"
          :class="store.isEditing ? 'text-sm' : 'text-lg md:text-2xl'"
        >
          <template v-if="showcaseOpen">Widget catalogue</template>
          <template v-else-if="previewing">Dashboard preview</template>
          <template v-else-if="store.isEditing">Arrange your dashboard</template>
          <template v-else-if="!showHeaderGreeting">Home</template>
          <template v-else
            >{{ greeting
            }}<template v-if="auth.user?.user.name">, {{ auth.user.user.name }}</template></template
          >
        </h1>
        <p
          v-if="!store.isEditing && showHeaderGreeting"
          class="mt-2 text-sm text-gray-600 dark:text-gray-400"
        >
          Everything in your library, arranged your way.
        </p>
      </div>

      <template v-if="store.settings && store.isEditing">
        <UButton
          v-if="showcaseOpen"
          color="neutral"
          variant="outline"
          class="ml-auto"
          icon="i-heroicons-arrow-left"
          @click="closeShowcase"
          >Back to editing</UButton
        >

        <template v-else>
          <div
            class="home-target flex items-center rounded-lg border border-gray-200/70 p-0.5 dark:border-gray-700/70"
            role="group"
            aria-label="Layout preview"
          >
            <UButton
              v-for="target in ['desktop', 'mobile'] as const"
              :key="target"
              :color="store.target === target ? 'primary' : 'neutral'"
              :variant="store.target === target ? 'soft' : 'ghost'"
              :aria-pressed="store.target === target"
              :disabled="isSaving || dragActive"
              size="sm"
              @click="changeTarget(target)"
              >{{ target === "desktop" ? "Desktop" : "Mobile" }}</UButton
            >
          </div>

          <div class="home-tools flex items-center gap-2">
            <div
              v-if="!previewing"
              class="flex items-center"
              role="group"
              aria-label="Editing history"
            >
              <UButton
                color="neutral"
                variant="ghost"
                icon="i-lucide-undo-2"
                aria-label="Undo"
                title="Undo (Ctrl/Cmd+Z)"
                :disabled="!canUndo || isSaving || dragActive"
                class="justify-center"
                @click="changeHistory()"
              />
              <UButton
                color="neutral"
                variant="ghost"
                icon="i-lucide-redo-2"
                aria-label="Redo"
                title="Redo (Ctrl/Cmd+Shift+Z or Ctrl+Y)"
                :disabled="!canRedo || isSaving || dragActive"
                class="justify-center"
                @click="changeHistory(true)"
              />
            </div>

            <UButton
              color="neutral"
              variant="ghost"
              :icon="previewing ? 'i-lucide-eye-off' : 'i-lucide-eye'"
              :aria-label="previewing ? 'Back to editing' : 'Preview'"
              :aria-pressed="previewing"
              :disabled="isSaving || dragActive"
              @click="togglePreview"
              ><span class="hidden xl:inline">{{
                previewing ? "Back to editing" : "Preview"
              }}</span></UButton
            >
            <UButton
              v-if="!previewing"
              color="neutral"
              variant="outline"
              icon="i-heroicons-plus"
              aria-label="Add widget"
              :disabled="isSaving || dragActive"
              @click="catalogueOpen = !catalogueOpen"
              ><span class="hidden xl:inline">Add widget</span></UButton
            >
            <UButton
              v-if="!wideEditor && !previewing"
              color="neutral"
              variant="ghost"
              icon="i-heroicons-cog-6-tooth"
              aria-label="Layout controls"
              :aria-expanded="controlsOpen"
              :disabled="isSaving || dragActive"
              @click="controlsOpen = !controlsOpen"
            />
          </div>

          <div class="home-save flex items-center gap-2">
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
          </div>
        </template>
      </template>

      <UButton
        v-else-if="store.settings"
        class="ml-auto"
        color="neutral"
        variant="outline"
        icon="i-heroicons-cog-6-tooth"
        @click="beginEditing"
        >Customize</UButton
      >
    </header>

    <HomeLoadError v-if="showError" :retrying="isLoading" :disabled="isSaving" @retry="refresh()" />

    <div
      v-if="!store.settings && !showError"
      class="flex min-h-48 items-center justify-center"
      role="status"
      aria-label="Loading Home"
    >
      <UIcon name="i-mdi-loading" class="size-8 animate-spin text-gray-500 dark:text-gray-500" />
    </div>

    <template v-else-if="store.settings && layout">
      <UAlert
        v-if="saveError"
        color="error"
        variant="soft"
        title="Changes are not saved"
        :description="saveError"
      />

      <div class="home-editor-container">
        <Suspense v-if="showcaseOpen">
          <HomeWidgetShowcase />

          <template #fallback>
            <div
              class="flex h-full items-center justify-center"
              role="status"
              aria-label="Loading widget catalogue"
            >
              <UIcon
                name="i-mdi-loading"
                class="size-8 animate-spin text-gray-500 dark:text-gray-500"
              />
            </div>
          </template>
        </Suspense>

        <div
          v-show="!showcaseOpen"
          class="home-editor-workspace"
          :class="{
            'has-controls': catalogueDocked && !previewing,
            'has-sheet': !wideEditor && controlsOpen && !previewing,
          }"
        >
          <div class="home-canvas min-h-0 min-w-0">
            <component
              :is="mobileEditor ? HomeMobilePreview : 'div'"
              v-bind="
                mobileEditor
                  ? { shared: sharedPreview, dropActive: dragActive, framed: !isMobile }
                  : {}
              "
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
                :hovered-id="hoveredId"
                @select="(instanceId, silent) => selectWidget(instanceId, false, silent)"
                @deselect="selectedId = null"
                @place="placeWidget"
                @nudge="nudgeWidget"
                @resize="resizeWidget"
                @duplicate="duplicateWidget"
                @options="changeOptions"
                @remove="removeWidget"
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
          </div>

          <HomeEditorPanel
            v-if="store.isEditing && !previewing"
            v-model:open="controlsOpen"
            :docked="catalogueDocked"
          >
            <HomeWidgetInspector
              v-if="selectedId && (canEdit || isSaving)"
              :layout="layout"
              :selected-id="selectedId"
              :disabled="isSaving || dragActive"
              :can-duplicate="canAdd"
              @back="selectedId = null"
              @options="changeOptions"
              @title="changeTitle"
              @preset="applyPreset"
              @remove="removeWidget"
              @nudge="nudgeWidget"
              @resize="resizeWidget"
              @duplicate="duplicateWidget"
            />

            <HomeEditorControls
              v-else-if="store.draft"
              :target="store.target"
              :layout="layout"
              :separate-mobile="store.draft.useSeparateMobileLayout"
              :show-widget-titles="store.draft.showWidgetTitles"
              :show-header-greeting="store.draft.showHeaderGreeting"
              :disabled="isSaving || dragActive"
              @select="selectWidget($event, true)"
              @hover="hoveredId = $event"
              @separate="toggleSeparate"
              @titles="changeTitles"
              @greeting="changeHeaderGreeting"
              @reset="resetLayout"
            />
          </HomeEditorPanel>

          <HomeWidgetCatalogue
            v-if="store.isEditing && !previewing"
            v-model:open="catalogueOpen"
            :disabled="!canAdd"
            :disabled-reason="catalogueDisabledReason"
            :docked="catalogueDocked"
            :dragging="dragActive"
            @add="addWidget"
            @pointer-drag="startCatalogueDrag"
            @showcase="openShowcase"
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

    <p id="home-placement-help" class="sr-only">
      Drag a widget to place it and its edges to resize. Tab selects widgets, arrow keys move one
      cell, Shift plus arrow keys resize, and Delete removes the selected widget. Ctrl or Cmd plus D
      duplicates, Z undoes, and Shift plus Z redoes. On touch screens, tap to select before
      dragging.
    </p>

    <p class="sr-only" aria-live="polite">{{ announcement }}</p>
  </main>
</template>

<style scoped>
.home-page {
  padding: 1.5rem 1rem calc(6rem + env(safe-area-inset-bottom));
}

.home-toolbar {
  margin-bottom: 2rem;
}

.home-heading {
  flex: 1;
}

.is-editing {
  display: flex;
  height: 100%;
  min-height: 0;
  flex-direction: column;
  padding: 0;
}

.is-editing .home-toolbar {
  margin: 0;
  padding: 0.5rem 1rem;
  gap: 0.5rem 1rem;
}

.is-editing .home-heading {
  flex: initial;
}

.home-tools {
  margin-left: auto;
}

.home-editor-container {
  container-name: home-editor;
  container-type: inline-size;
}

.is-editing .home-editor-container {
  flex: 1;
  min-height: 0;
}

.home-editor-workspace {
  position: relative;
  display: grid;
  min-height: 0;
}

.is-editing .home-editor-workspace {
  height: 100%;
}

.is-editing .home-canvas {
  overflow: auto;
  overscroll-behavior: contain;
  padding: 1.5rem;
}

.has-sheet .home-canvas {
  padding-bottom: 72dvh;
  scroll-padding-bottom: 68dvh;
}

@media (min-width: 768px) {
  .home-page:not(.is-editing) {
    padding: 2rem 1.5rem;
  }

  .home-editor-workspace.has-controls {
    grid-template-columns: minmax(0, 1fr) 280px;
  }
}

@media (min-width: 1280px) {
  .home-editor-workspace.has-controls {
    grid-template-columns: minmax(0, 1fr) 320px;
  }
}

@media (max-width: 767px) {
  .is-editing .home-toolbar {
    padding: 0.5rem 0.75rem;
  }

  .home-heading {
    order: 1;
    flex: 1 !important;
  }

  .home-save {
    order: 2;
  }

  .home-target {
    order: 3;
  }

  .home-tools {
    order: 4;
  }

  .is-editing .home-heading {
    flex-basis: calc(100% - 10rem) !important;
  }

  .is-editing .home-canvas {
    padding: 0.75rem;
  }

  .has-sheet .home-canvas {
    padding-bottom: 72dvh;
  }
}
</style>
