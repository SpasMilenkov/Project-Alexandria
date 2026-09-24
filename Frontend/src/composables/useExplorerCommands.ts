import { useTabStore } from "@/stores/tab";

interface GuardDeps {
  hasOpenOverlay?: () => boolean;
  isActiveTab?: () => boolean;
}

const OVERLAY_OWNED_SELECTOR =
  '[role="dialog"], [role="alertdialog"], [role="menu"], [role="listbox"]';

const isEditableTarget = (target: EventTarget | null): boolean => {
  if (!(target instanceof HTMLElement)) return false;
  if (target.isContentEditable) return true;
  const attr = target.getAttribute("contenteditable");
  if (attr !== null && attr.toLowerCase() !== "false") return true;
  const tag = target.tagName;
  return tag === "INPUT" || tag === "TEXTAREA" || tag === "SELECT";
};

const isOverlayOwnedTarget = (target: EventTarget | null): boolean => {
  if (!(target instanceof HTMLElement)) return false;
  return target.closest(OVERLAY_OWNED_SELECTOR) !== null;
};

/**
 * Single-owner gate for explorer keyboard commands.
 * Explorer shortcuts apply only when this explorer hosts the active tab,
 * no overlay owns input, and the key event did not start inside an editable
 * control or a dialog/menu/listbox surface. Plain body focus still counts
 * as explorer input so background clicks do not silently disable shortcuts.
 */
export const useExplorerCommandGuard = (tabId: string, deps?: GuardDeps) => {
  const tabStore = useTabStore();
  const overlay = useOverlay();

  const isActiveTab = deps?.isActiveTab ?? (() => tabStore.activeTabId === tabId);
  const hasOpenOverlay =
    deps?.hasOpenOverlay ?? (() => overlay.overlays.some((entry) => entry.isOpen));

  const canHandleCommand = (event: KeyboardEvent): boolean => {
    if (!isActiveTab()) return false;
    if (hasOpenOverlay()) return false;
    if (isEditableTarget(event.target)) return false;
    if (isOverlayOwnedTarget(event.target)) return false;
    return true;
  };

  return { canHandleCommand };
};

export { isEditableTarget, isOverlayOwnedTarget };
