import { onScopeDispose } from "vue";

/**
 * Sends the blur Firefox skips when the focused element is removed from the page.
 *
 * Nuxt UI's shortcuts learn what has focus from focus and blur events. Firefox fires no blur
 * when a focused input is removed, and focus falling back to the page body fires no focus event
 * either. So after a dialog, drawer, popover or inline field closed with its input focused, the
 * shortcuts kept treating the removed input as focused and ignored every key until the next
 * click.
 *
 * The blur is dispatched on the body and does not bubble, so only capture listeners on the
 * window hear it, which is how Nuxt UI listens. Plain window blur listeners, such as the drag
 * handlers that cancel when the window loses focus, never see it.
 */
export const useFocusLossSync = () => {
  let focused: Element | null = null;

  const track = (event: FocusEvent) => {
    focused = event.target instanceof Element ? event.target : null;
  };

  const observer = new MutationObserver(() => {
    if (!focused || focused.isConnected) {
      return;
    }
    focused = null;
    document.body.dispatchEvent(new FocusEvent("blur", { relatedTarget: null }));
  });

  document.addEventListener("focusin", track, true);
  observer.observe(document.body, { childList: true, subtree: true });

  onScopeDispose(() => {
    document.removeEventListener("focusin", track, true);
    observer.disconnect();
  });
};
