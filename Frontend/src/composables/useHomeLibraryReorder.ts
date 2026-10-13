import { useEventListener } from "@vueuse/core";
import { nextTick, onUnmounted, ref } from "vue";

import type { HomeLibraryDestination } from "@/enums/home-widget";
import type { HomeLibraryShortcut } from "@/types/home";

interface LibraryReorderOptions {
  selection: () => HomeLibraryShortcut[];
  change: (shortcuts: HomeLibraryShortcut[]) => void;
  label: (destination: HomeLibraryDestination) => string;
}

interface LibraryPointerSession {
  pointerId: number;
  destination: HomeLibraryDestination;
  handle: HTMLElement;
  scroll: HTMLElement | null;
  start: { x: number; y: number };
}

export const useHomeLibraryReorder = (options: LibraryReorderOptions) => {
  const list = ref<HTMLOListElement | null>(null);
  const dragging = ref<HomeLibraryDestination | null>(null);
  const dropTarget = ref<{ destination: HomeLibraryDestination; after: boolean } | null>(null);
  const announcement = ref("");
  let session: LibraryPointerSession | null = null;
  let point = { x: 0, y: 0 };
  let frame = 0;

  const cancel = () => {
    cancelAnimationFrame(frame);
    const current = session;
    session = null;

    if (current?.handle.hasPointerCapture(current.pointerId))
      current.handle.releasePointerCapture(current.pointerId);

    dragging.value = null;
    dropTarget.value = null;
    frame = 0;
  };

  const focusHandle = (destination: HomeLibraryDestination) => {
    void nextTick(() => {
      list.value?.querySelector<HTMLElement>(`[data-library-reorder="${destination}"]`)?.focus();
    });
  };

  const moveTo = (destination: HomeLibraryDestination, target: number) => {
    const next = options.selection().map((shortcut) => ({ ...shortcut }));
    const index = next.findIndex((shortcut) => shortcut.destination === destination);

    if (index < 0 || target < 0 || target >= next.length || index === target) return;

    const [shortcut] = next.splice(index, 1);
    next.splice(target, 0, shortcut!);
    options.change(next);
    announcement.value = `${options.label(destination)} moved to position ${target + 1} of ${next.length}.`;
    focusHandle(destination);
  };

  const updateTarget = () => {
    const element = document.elementFromPoint(point.x, point.y);
    const row = element?.closest<HTMLElement>("[data-library-destination]");

    if (!row || !list.value?.contains(row)) {
      dropTarget.value = null;

      return;
    }

    const destination = Number(row.dataset.libraryDestination) as HomeLibraryDestination;
    const bounds = row.getBoundingClientRect();
    const after = point.y >= bounds.top + bounds.height / 2;

    if (dropTarget.value?.destination !== destination || dropTarget.value.after !== after)
      dropTarget.value = { destination, after };
  };

  const scrollFrame = () => {
    if (!session || dragging.value === null) return;

    const scroll = session.scroll;

    if (scroll) {
      const bounds = scroll.getBoundingClientRect();
      const top = Math.max(0, bounds.top);
      const bottom = Math.min(window.innerHeight, bounds.bottom);

      if (point.y < top + 40) scroll.scrollTop -= 8;
      else if (point.y > bottom - 40) scroll.scrollTop += 8;
    }

    updateTarget();
    frame = requestAnimationFrame(scrollFrame);
  };

  const scrollContainer = () => {
    let parent = list.value?.parentElement ?? null;

    while (parent) {
      if (/(?:auto|scroll)/u.test(getComputedStyle(parent).overflowY)) return parent;

      parent = parent.parentElement;
    }

    return null;
  };

  const start = (destination: HomeLibraryDestination, event: PointerEvent) => {
    if (event.button !== 0 || !event.isPrimary || options.selection().length < 2) return;
    if (!(event.currentTarget instanceof HTMLElement)) return;

    cancel();
    event.preventDefault();
    event.currentTarget.focus({ preventScroll: true });
    event.currentTarget.setPointerCapture(event.pointerId);
    point = { x: event.clientX, y: event.clientY };
    session = {
      pointerId: event.pointerId,
      destination,
      handle: event.currentTarget,
      scroll: scrollContainer(),
      start: point,
    };
  };

  const keyboard = (destination: HomeLibraryDestination, event: KeyboardEvent) => {
    const selection = options.selection();
    const index = selection.findIndex((shortcut) => shortcut.destination === destination);
    let target = index;

    if (event.key === "ArrowUp") target--;
    else if (event.key === "ArrowDown") target++;
    else if (event.key === "Home") target = 0;
    else if (event.key === "End") target = selection.length - 1;
    else return;

    event.preventDefault();
    event.stopPropagation();
    moveTo(destination, target);
  };

  useEventListener(window, "pointermove", (event) => {
    if (!session || event.pointerId !== session.pointerId) return;

    point = { x: event.clientX, y: event.clientY };

    if (dragging.value === null) {
      if (Math.hypot(point.x - session.start.x, point.y - session.start.y) < 6) return;

      dragging.value = session.destination;
      announcement.value = `Moving ${options.label(session.destination)}. Release to place, or press Escape to cancel.`;
      scrollFrame();
    }

    updateTarget();
  });

  useEventListener(window, "pointerup", (event) => {
    if (!session || event.pointerId !== session.pointerId) return;

    point = { x: event.clientX, y: event.clientY };
    updateTarget();
    const destination = dragging.value;
    const target = dropTarget.value;
    const selection = options.selection();
    cancel();

    if (destination === null || !target || destination === target.destination) return;

    const index = selection.findIndex((shortcut) => shortcut.destination === destination);
    let position = selection.findIndex((shortcut) => shortcut.destination === target.destination);

    if (position < 0 || index < 0) return;
    if (target.after) position++;
    if (index < position) position--;

    moveTo(destination, position);
  });

  useEventListener(window, "pointercancel", (event) => {
    if (event.pointerId !== session?.pointerId) return;

    announcement.value = "Shortcut move cancelled.";
    cancel();
  });

  useEventListener(list, "lostpointercapture", (event) => {
    if (event.pointerId === session?.pointerId) cancel();
  });

  useEventListener(window, "blur", cancel);

  useEventListener(
    window,
    "keydown",
    (event) => {
      if (event.key !== "Escape" || !session) return;

      event.preventDefault();
      event.stopPropagation();
      announcement.value = "Shortcut move cancelled.";
      cancel();
    },
    { capture: true },
  );

  onUnmounted(cancel);

  return { list, dragging, dropTarget, announcement, start, keyboard };
};
