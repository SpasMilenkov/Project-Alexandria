import { computed, ref, watch } from "vue";

// Moves an active row through a list from the keyboard while focus stays in an
// input, the way a command palette does. The active row follows its item when
// the list refreshes and resets once the item is gone.
export const useListNavigation = <T>(items: () => T[], keyOf: (item: T) => string) => {
  const activeIndex = ref(-1);
  let lastKeys: string[] = [];

  const activeItem = computed(() => items()[activeIndex.value] ?? null);

  watch(
    () => items().map(keyOf).join("\n"),
    () => {
      const keys = items().map(keyOf);
      const activeKey = lastKeys[activeIndex.value];
      activeIndex.value = activeKey === undefined ? -1 : keys.indexOf(activeKey);
      lastKeys = keys;
    },
    { immediate: true },
  );

  const move = (step: 1 | -1) => {
    const count = items().length;
    if (count === 0) {
      return;
    }
    if (activeIndex.value === -1) {
      activeIndex.value = step === 1 ? 0 : count - 1;
      return;
    }
    activeIndex.value = (activeIndex.value + step + count) % count;
  };

  const setActive = (index: number) => {
    activeIndex.value = index;
  };

  const clear = () => {
    activeIndex.value = -1;
  };

  // Handles the arrows, Enter and Escape, and reports whether it used the key so
  // the caller can leave the rest to the input and the modal.
  const handleKey = (event: KeyboardEvent, open: (item: T) => void): boolean => {
    const isArrow = event.key === "ArrowDown" || event.key === "ArrowUp";
    if (isArrow && items().length > 0) {
      event.preventDefault();
      move(event.key === "ArrowDown" ? 1 : -1);
      return true;
    }

    const item = activeItem.value;
    if (!item) {
      return false;
    }

    if (event.key === "Enter") {
      event.preventDefault();
      open(item);
      return true;
    }
    if (event.key === "Escape") {
      // The modal closes on an unhandled Escape, so the first press only drops the row.
      event.preventDefault();
      clear();
      return true;
    }
    return false;
  };

  return { activeIndex, activeItem, clear, handleKey, move, setActive };
};
