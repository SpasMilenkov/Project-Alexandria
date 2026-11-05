/**
 * Swallows the click the browser dispatches right after a drag gesture ends, so
 * ending a marquee or a drop never doubles as an ordinary click (which would
 * clear or replace the selection). The listener expires on the next task when
 * no click follows, for example when the pointer was released outside the page.
 */
export const suppressNextClick = () => {
  const swallow = (event: MouseEvent) => {
    event.stopPropagation();
    event.preventDefault();
    window.removeEventListener("click", swallow, true);
  };

  window.addEventListener("click", swallow, true);
  setTimeout(() => window.removeEventListener("click", swallow, true), 0);
};

export const ITEM_SELECTOR = "[data-file-id],[data-dir-id]";

/** Elements that keep their own pointer behavior inside the explorer listing. */
export const INTERACTIVE_SELECTOR =
  "button,a,input,textarea,select,[role='button'],[role='menuitem']";
