<template>
  <Teleport to="body">
    <Transition
      enter-active-class="transition duration-150 ease-out"
      enter-from-class="scale-95 opacity-0"
      leave-active-class="transition duration-100 ease-in"
      leave-to-class="opacity-0"
    >
      <DropTargetPreview
        v-if="panel"
        ref="panelRef"
        class="fixed top-0 left-0 z-[80] origin-top-left"
        :style="{ transform: `translate3d(${panel.position.x}px, ${panel.position.y}px, 0)` }"
        :trail="panel.trail"
        :excluded-ids="session?.directoryIds ?? []"
      />
    </Transition>

    <Transition
      leave-active-class="transition duration-150 ease-in"
      leave-to-class="scale-75 opacity-0"
    >
      <div
        v-if="ghost"
        class="pointer-events-none fixed top-0 left-0 z-[90]"
        :style="{ transform: `translate3d(${ghostPoint.x + 14}px, ${ghostPoint.y + 14}px, 0)` }"
        aria-hidden="true"
      >
        <div class="relative w-60">
          <div
            v-if="ghost.count > 2"
            class="absolute inset-0 translate-x-1.5 translate-y-1.5 rounded-lg border border-gray-200/70 frosted-glass glass-surface-strong dark:border-gray-700/70"
          />
          <div
            v-if="ghost.count > 1"
            class="absolute inset-0 translate-x-0.75 translate-y-0.75 rounded-lg border border-gray-200/70 frosted-glass glass-surface-strong dark:border-gray-700/70"
          />

          <div
            class="relative flex items-center gap-2 rounded-lg border border-gray-200/70 px-2.5 py-2 shadow-lg frosted-glass glass-surface-strong dark:border-gray-700/70"
          >
            <Icon
              :icon="ghost.lead.icon"
              class="h-5 w-5 shrink-0 text-gray-500 dark:text-gray-400"
            />

            <div class="min-w-0 flex-1">
              <p class="truncate text-sm font-medium text-gray-900 dark:text-gray-100">
                {{ ghost.lead.name }}
              </p>
              <p class="flex min-w-0 items-center gap-1 text-xs text-gray-600 dark:text-gray-400">
                <Icon :icon="status.icon" class="h-3.5 w-3.5 shrink-0" />
                <span class="truncate">
                  {{ status.prefix }}
                  <strong v-if="status.name" class="font-semibold text-gray-900 dark:text-gray-100">
                    {{ status.name }}
                  </strong>
                </span>
              </p>
            </div>

            <span
              v-if="ghost.count > 1"
              class="shrink-0 rounded-full bg-primary px-1.5 text-[11px] leading-[18px] font-semibold tabular-nums text-white dark:text-neutral-900"
            >
              {{ ghost.count }}
            </span>
          </div>
        </div>
      </div>
    </Transition>

    <span class="sr-only" aria-live="polite">{{ announcement }}</span>
  </Teleport>
</template>

<script setup lang="ts">
import { Icon } from "@iconify/vue";
import { useQueryCache } from "@pinia/colada";
import { computed, onScopeDispose, ref, shallowRef, useTemplateRef, watch } from "vue";

import { useAppToast } from "@/composables/useAppToast";
import { useEdgeAutoScroll } from "@/composables/useEdgeAutoScroll";
import {
  type DragSession,
  type DropInvalidReason,
  type DropKind,
  type MoveRecord,
  dropInvalidReason,
  fromDropId,
  useExplorerDragSession,
} from "@/composables/useExplorerDragSession";
import { previewDirectoriesQuery, previewFilesQuery } from "@/features/explorer/drag/drop-preview";
import { createDwellTimer } from "@/features/explorer/drag/dwell-timer";
import {
  type Box,
  type Point,
  boxContains,
  expandBox,
  hasMovedBeyond,
  placeFloatingPanel,
} from "@/features/explorer/drag/geometry";
import { suppressNextClick } from "@/features/explorer/drag/pointer-gesture";
import { describeMove, executeExplorerMove, reverseMove } from "@/features/explorer/move";
import { moveDirectories } from "@/mutations/directories";
import { moveFiles } from "@/mutations/files";
import { useTabStore } from "@/stores/tab";

import DropTargetPreview, { type PreviewTrailSegment } from "./DropTargetPreview.vue";

interface DropTarget {
  id: string | null;
  name: string;
  kind: DropKind;
  invalid: DropInvalidReason | null;
  element: HTMLElement | null;
}

interface PanelState {
  trail: PreviewTrailSegment[];
  anchor: Box;
  position: Point;
}

const PANEL_SIZE = { height: 320, width: 288 };
const PREFETCH_MS = 150;
const DWELL_MS = 600;
const PANEL_GRACE_MS = 300;
const PANEL_BRIDGE_PX = 20;
const DWELL_KINDS: ReadonlySet<DropKind> = new Set([
  "item",
  "breadcrumb",
  "preview-row",
  "preview-trail",
]);

const dragSession = useExplorerDragSession();
const tabStore = useTabStore();
const queryCache = useQueryCache();
const appToast = useAppToast("move");

const { mutateAsync: moveFilesMutate } = moveFiles();
const { mutateAsync: moveDirectoriesMutate } = moveDirectories();

const session = dragSession.session;
const panelRef = useTemplateRef<InstanceType<typeof DropTargetPreview>>("panelRef");

// The ghost keeps its last session for the leave transition after the drop.
const ghost = shallowRef<DragSession | null>(null);
const ghostPoint = ref<Point>({ x: 0, y: 0 });
const target = shallowRef<DropTarget | null>(null);
const hoveredTabTitle = ref<string | null>(null);
const panel = shallowRef<PanelState | null>(null);
const announcement = ref("");

let point: Point = { x: 0, y: 0 };
let activeElement: HTMLElement | null = null;
let activeTabElement: HTMLElement | null = null;
let dwellLock: Point | null = null;
let graceTimer: ReturnType<typeof setTimeout> | null = null;

const status = computed(() => {
  const current = target.value;
  const count = ghost.value?.count ?? 0;

  if (hoveredTabTitle.value) {
    return { icon: "mdi:folder-move-outline", name: hoveredTabTitle.value, prefix: "Hold to open" };
  }

  if (current?.invalid === "self") {
    return { icon: "mdi:folder-remove-outline", name: "", prefix: "Can't move into itself" };
  }

  if (current?.invalid === "origin") {
    return { icon: "mdi:folder-remove-outline", name: "", prefix: "Already here" };
  }

  if (current) return { icon: "mdi:folder-move-outline", name: current.name, prefix: "Move to" };

  return {
    icon: "mdi:folder-move-outline",
    name: "",
    prefix: `Move ${count} item${count === 1 ? "" : "s"}`,
  };
});

// Highlight state lives on the DOM as an attribute so a moving target never
// re-renders the listing; each surface styles `data-drop-active` itself.
const markElement = (element: HTMLElement | null, value: string | null) => {
  if (!element) return;
  if (value === null) element.removeAttribute("data-drop-active");
  else element.setAttribute("data-drop-active", value);
};

const setDocumentState = (state: string | null) => {
  const root = document.documentElement;

  if (state === null) root.removeAttribute("data-explorer-drag");
  else root.setAttribute("data-explorer-drag", state);
};

const prefetchFolder = (folderId: string | null) => {
  void queryCache.refresh(queryCache.ensure(previewDirectoriesQuery(folderId))).catch(() => null);
  void queryCache.refresh(queryCache.ensure(previewFilesQuery(folderId))).catch(() => null);
};

const clearGrace = () => {
  if (graceTimer !== null) clearTimeout(graceTimer);
  graceTimer = null;
};

const closePanel = () => {
  clearGrace();
  panel.value = null;
};

const panelBox = (): Box | null => {
  const element = panelRef.value?.$el as HTMLElement | undefined;

  if (!element) return null;

  const rect = element.getBoundingClientRect();

  return { bottom: rect.bottom, left: rect.left, right: rect.right, top: rect.top };
};

const openPanel = (dropTarget: DropTarget) => {
  if (!dropTarget.element) return;

  const rect = dropTarget.element.getBoundingClientRect();
  const anchor = { bottom: rect.bottom, left: rect.left, right: rect.right, top: rect.top };
  const viewport = { height: window.innerHeight, width: window.innerWidth };

  clearGrace();
  panel.value = {
    anchor,
    position: placeFloatingPanel(anchor, PANEL_SIZE, viewport, point),
    trail: [{ id: dropTarget.id, name: dropTarget.name }],
  };
  announcement.value = `Showing ${dropTarget.name}`;
};

const drillPanel = (dropTarget: DropTarget) => {
  const current = panel.value;

  if (!current) return;

  const index = current.trail.findIndex((segment) => segment.id === dropTarget.id);
  const isStepBack = dropTarget.kind === "preview-trail" && index >= 0;

  if (isStepBack && index === current.trail.length - 1) return;

  const trail = isStepBack
    ? current.trail.slice(0, index + 1)
    : [...current.trail, { id: dropTarget.id, name: dropTarget.name }];

  panel.value = { ...current, trail };
  announcement.value = `Showing ${dropTarget.name}`;
};

const prefetchDwell = createDwellTimer<DropTarget>(PREFETCH_MS, (dropTarget) => {
  prefetchFolder(dropTarget.id);
});

const openDwell = createDwellTimer<DropTarget>(DWELL_MS, (dropTarget) => {
  if (!session.value) return;

  if (dropTarget.kind === "item" || dropTarget.kind === "breadcrumb") openPanel(dropTarget);
  else drillPanel(dropTarget);

  // Content swaps under a resting pointer; wait for movement before the
  // next dwell so holding still never drills on its own.
  dwellLock = point;
  openDwell.reset();
  prefetchDwell.reset();
});

const tabDwell = createDwellTimer<string>(DWELL_MS, (tabId) => {
  if (!session.value) return;

  closePanel();
  tabStore.activeTabId = tabId;
  announcement.value = `Opened tab ${tabStore.getTab(tabId)?.title ?? ""}`;
});

const readTarget = (element: HTMLElement, current: DragSession): DropTarget => {
  const id = fromDropId(element.dataset.dropDirId ?? "");

  return {
    element,
    id,
    invalid: dropInvalidReason(id, current),
    kind: (element.dataset.dropKind as DropKind | undefined) ?? "item",
    name: element.dataset.dropDirName ?? "folder",
  };
};

const panelTarget = (): DropTarget | null => {
  const current = panel.value;
  const element = panelRef.value?.$el as HTMLElement | undefined;

  if (!current || !element) return null;

  const folder = current.trail[current.trail.length - 1];

  return { element, id: folder.id, invalid: null, kind: "panel", name: folder.name };
};

// The margin around the panel covers the gap to its anchor, so carrying an
// item from the folder into the panel never drops the target on the way.
const isOverPanel = () => {
  const box = panelBox();

  return box !== null && boxContains(expandBox(box, PANEL_BRIDGE_PX), point);
};

const isInsidePanelRegion = () => {
  const current = panel.value;

  if (!current) return false;

  return boxContains(current.anchor, point) || isOverPanel();
};

const resolveTarget = (element: Element | null, current: DragSession): DropTarget | null => {
  const dropElement = element?.closest<HTMLElement>("[data-drop-dir-id]");

  if (dropElement) {
    const resolved = readTarget(dropElement, current);

    // The origin listing's own background is simply "not a target".
    if (resolved.kind !== "background" || !resolved.invalid) return resolved;
  }

  if (isOverPanel()) return panelTarget();

  return null;
};

const sameTarget = (a: DropTarget | null, b: DropTarget | null) =>
  a?.element === b?.element && a?.id === b?.id && a?.kind === b?.kind;

const setTarget = (next: DropTarget | null) => {
  if (sameTarget(target.value, next)) return;

  markElement(activeElement, null);
  activeElement = next?.element ?? null;

  let mark: string | null = null;

  if (next?.invalid) mark = "invalid";
  else if (next) mark = "valid";

  markElement(activeElement, mark);
  target.value = next;
  setDocumentState(next?.invalid ? "invalid" : "active");

  if (next && !next.invalid) announcement.value = `Move to ${next.name}`;
};

const updateTab = (element: Element | null) => {
  const tabElement = element?.closest<HTMLElement>("[data-tab-id]") ?? null;
  const tabId = tabElement?.dataset.tabId ?? null;
  const isOtherTab = tabId !== null && tabId !== tabStore.activeTabId;

  if (tabElement !== activeTabElement) {
    markElement(activeTabElement, null);
    activeTabElement = isOtherTab ? tabElement : null;
    markElement(activeTabElement, "valid");
  }

  hoveredTabTitle.value = isOtherTab ? (tabStore.getTab(tabId)?.title ?? "tab") : null;
  tabDwell.track(isOtherTab ? tabId : null, tabId ?? undefined);
};

const updateDwell = (next: DropTarget | null) => {
  if (dwellLock && !hasMovedBeyond(dwellLock, point, 4)) return;

  dwellLock = null;

  const canDwell = next !== null && !next.invalid && DWELL_KINDS.has(next.kind);
  const isShown = canDwell && panel.value?.trail.at(-1)?.id === next.id;

  if (!canDwell || isShown) {
    prefetchDwell.track(null);
    openDwell.track(null);
    return;
  }

  const key = `${next.kind}:${next.id ?? "root"}`;

  prefetchDwell.track(key, next);
  openDwell.track(key, next);
};

const updatePanelGrace = () => {
  if (!panel.value || isInsidePanelRegion()) {
    clearGrace();
    return;
  }

  if (graceTimer === null) graceTimer = setTimeout(closePanel, PANEL_GRACE_MS);
};

const frame = () => {
  const current = session.value;

  if (!current) return;

  if (ghostPoint.value.x !== point.x || ghostPoint.value.y !== point.y) {
    ghostPoint.value = point;
  }

  const element = document.elementFromPoint(point.x, point.y);
  const scrollElement = element?.closest<HTMLElement>("[data-drop-scroll]");

  if (scrollElement) autoScroll.update(point, scrollElement);

  const next = resolveTarget(element, current);

  setTarget(next);
  updateTab(element);
  updateDwell(next);
  updatePanelGrace();
};

const autoScroll = useEdgeAutoScroll(frame);

const runMove = async (record: MoveRecord, isUndo: boolean) => {
  const count = record.fileIds.length + record.directoryIds.length;

  dragSession.beginMove(count);
  dragSession.notifyMoved(record);

  let errors: unknown[] = [];

  try {
    ({ errors } = await executeExplorerMove(record, {
      moveDirectories: moveDirectoriesMutate,
      moveFiles: moveFilesMutate,
    }));
  } finally {
    dragSession.settleMove(count);
  }

  if (errors.length > 0) {
    dragSession.notifyMoveFailed(record);
    appToast.error(isUndo ? "Couldn't undo the move" : "Couldn't move items", errors[0], {
      retry: () => runMove(record, isUndo),
    });
    return;
  }

  if (isUndo) {
    appToast.success("Move undone");
    return;
  }

  appToast.success(describeMove(record), {
    actions: [
      {
        icon: "i-lucide-undo-2",
        label: "Undo",
        onClick: () => {
          void runMove(reverseMove(record), true);
        },
      },
    ],
  });
};

const handlePointerMove = (event: PointerEvent) => {
  if (event.pointerId !== session.value?.pointerId) return;
  point = { x: event.clientX, y: event.clientY };
  autoScroll.update(point);
};

const finish = (drop: boolean) => {
  const current = session.value;

  if (!current) return;

  if (drop) frame();

  const dropTarget = target.value;

  dragSession.end();

  if (current.pointerType !== "touch") suppressNextClick();

  if (!drop || !dropTarget || dropTarget.invalid) {
    announcement.value = "Move cancelled";
    return;
  }

  void runMove(
    {
      destinationId: dropTarget.id,
      destinationName: dropTarget.name,
      directoryIds: current.directoryIds,
      fileIds: current.fileIds,
      leadName: current.lead.name,
      originId: current.originId,
      originName: current.originName,
    },
    false,
  );
};

const handlePointerUp = (event: PointerEvent) => {
  if (event.pointerId !== session.value?.pointerId) return;
  point = { x: event.clientX, y: event.clientY };
  finish(true);
};

const handlePointerCancel = (event: PointerEvent) => {
  if (event.pointerId !== session.value?.pointerId) return;
  finish(false);
};

const handleKeydown = (event: KeyboardEvent) => {
  if (event.key !== "Escape") return;
  event.preventDefault();
  event.stopPropagation();
  finish(false);
};

const handleBlur = () => finish(false);

const attach = (current: DragSession) => {
  point = current.point;
  ghost.value = current;
  ghostPoint.value = current.point;
  setDocumentState("active");
  window.addEventListener("pointermove", handlePointerMove, { passive: true });
  window.addEventListener("pointerup", handlePointerUp);
  window.addEventListener("pointercancel", handlePointerCancel);
  window.addEventListener("keydown", handleKeydown, true);
  window.addEventListener("blur", handleBlur);
  autoScroll.start(point, null);
  announcement.value = `Picked up ${current.count} item${current.count === 1 ? "" : "s"}`;
};

const detach = () => {
  window.removeEventListener("pointermove", handlePointerMove);
  window.removeEventListener("pointerup", handlePointerUp);
  window.removeEventListener("pointercancel", handlePointerCancel);
  window.removeEventListener("keydown", handleKeydown, true);
  window.removeEventListener("blur", handleBlur);
  autoScroll.stop();
  prefetchDwell.reset();
  openDwell.reset();
  tabDwell.reset();
  closePanel();
  markElement(activeElement, null);
  markElement(activeTabElement, null);
  activeElement = null;
  activeTabElement = null;
  dwellLock = null;
  target.value = null;
  hoveredTabTitle.value = null;
  ghost.value = null;
  setDocumentState(null);
};

// Synchronous so the pointer events right after a pick-up already land here.
watch(
  session,
  (current, previous) => {
    if (current && !previous) attach(current);
    if (!current && previous) detach();
  },
  { flush: "sync" },
);

onScopeDispose(() => {
  if (session.value) dragSession.end();
  detach();
});
</script>

<style>
html[data-explorer-drag],
html[data-explorer-drag] * {
  cursor: grabbing !important;
  user-select: none !important;
}

html[data-explorer-drag="invalid"],
html[data-explorer-drag="invalid"] * {
  cursor: not-allowed !important;
}
</style>
