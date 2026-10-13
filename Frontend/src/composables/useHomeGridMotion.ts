import { useEventListener, usePreferredReducedMotion, useResizeObserver } from "@vueuse/core";
import { onBeforeUpdate, onScopeDispose, onUpdated, watch } from "vue";

interface HomeGridMotionOptions {
  board: () => HTMLElement | null;
  enabled: () => boolean;
  context: () => string;
  active: () => boolean;
}

interface Rectangle {
  left: number;
  top: number;
  width: number;
  height: number;
}

interface Snapshot {
  rectangle: Rectangle;
  position: string | undefined;
  widget: string | undefined;
}

const rectangleInBoard = (element: HTMLElement, board: DOMRect): Rectangle => {
  const rect = element.getBoundingClientRect();

  return {
    left: rect.left - board.left,
    top: rect.top - board.top,
    width: rect.width,
    height: rect.height,
  };
};

export const useHomeGridMotion = (options: HomeGridMotionOptions) => {
  const reducedMotion = usePreferredReducedMotion();
  const animations = new Map<HTMLElement, Animation>();
  const snapshots = new Map<string, Snapshot>();
  let boardWidth = 0;
  let context = options.context();

  const cancel = () => {
    animations.forEach((animation) => animation.cancel());
    animations.clear();
    snapshots.clear();
  };

  const elements = () => options.board()?.querySelectorAll<HTMLElement>("[data-home-motion]") ?? [];

  const enabled = () => options.enabled() && reducedMotion.value !== "reduce";

  const animateElement = (element: HTMLElement, bounds: DOMRect) => {
    if (element.dataset.motionHidden === "true" || !element.animate) return;

    let previous = snapshots.get(element.dataset.homeMotion!);
    if (previous?.position === element.dataset.motionPosition) return;

    // The placement outline supplies the visible origin when a dragged card returns.
    if (!previous) {
      const placement = snapshots.get("placement");
      if (placement?.widget === element.dataset.homeMotion) previous = placement;
    }
    if (!previous) return;

    animations.get(element)?.cancel();
    animations.delete(element);
    const to = rectangleInBoard(element, bounds);
    const from = previous.rectangle;
    if (!from.width || !to.width || !from.height || !to.height) return;
    if (
      Math.abs(from.left - to.left) < 0.5 &&
      Math.abs(from.top - to.top) < 0.5 &&
      Math.abs(from.width - to.width) < 0.5 &&
      Math.abs(from.height - to.height) < 0.5
    )
      return;

    const animation = element.animate(
      [
        {
          transform: `translate(${from.left - to.left}px, ${from.top - to.top}px)`,
          width: `${from.width}px`,
          height: `${from.height}px`,
        },
        { transform: "none", width: `${to.width}px`, height: `${to.height}px` },
      ],
      {
        duration: options.active() ? 90 : 150,
        easing: "cubic-bezier(0.2, 0.8, 0.2, 1)",
        fill: "both",
      },
    );
    animations.set(element, animation);
    animation.onfinish = () => {
      if (animations.get(element) !== animation) return;

      animation.cancel();
      animations.delete(element);
    };
  };

  onBeforeUpdate(() => {
    snapshots.clear();
    if (context !== options.context()) {
      context = options.context();
      cancel();
      return;
    }

    const board = options.board();
    if (!board || !enabled()) {
      cancel();
      return;
    }

    const bounds = board.getBoundingClientRect();
    boardWidth = bounds.width;

    elements().forEach((element) => {
      if (element.dataset.motionHidden === "true") return;

      snapshots.set(element.dataset.homeMotion!, {
        rectangle: rectangleInBoard(element, bounds),
        position: element.dataset.motionPosition,
        widget: element.dataset.motionWidget,
      });
    });
  });

  onUpdated(() => {
    const board = options.board();
    if (!board || !enabled()) {
      cancel();
      return;
    }

    const bounds = board.getBoundingClientRect();
    if (context !== options.context() || Math.abs(bounds.width - boardWidth) > 1) {
      cancel();
      return;
    }

    const current = new Set(elements());
    for (const [element, animation] of animations) {
      if (!current.has(element) || element.dataset.motionHidden === "true") {
        animation.cancel();
        animations.delete(element);
      }
    }

    current.forEach((element) => animateElement(element, bounds));
    snapshots.clear();
  });

  watch([options.enabled, options.context, reducedMotion], cancel, { flush: "sync" });
  useEventListener(window, "resize", cancel);
  useResizeObserver(options.board, ([entry]) => {
    if (entry && Math.abs(entry.contentRect.width - boardWidth) > 1) cancel();
  });
  onScopeDispose(cancel);

  return { cancel };
};
