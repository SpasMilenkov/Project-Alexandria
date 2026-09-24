import { type Ref, nextTick, onMounted, onScopeDispose, ref } from "vue";

interface PlayerModeTransitionOptions {
  card: Ref<HTMLElement | null>;
  layout: Ref<HTMLElement | null>;
  mode: Ref<"pip" | "strip">;
  positionFloating: () => void;
  beforeChange: () => void;
}

const EASING = "cubic-bezier(0.2, 0.8, 0.2, 1)";
const ARTWORK = "[data-player-artwork]";

const placeLayer = (element: HTMLElement, rect: DOMRect, zIndex: number) => {
  Object.assign(element.style, {
    position: "fixed",
    top: `${rect.top}px`,
    left: `${rect.left}px`,
    width: `${rect.width}px`,
    height: `${rect.height}px`,
    margin: "0",
    transition: "none",
    transformOrigin: "0 0",
    pointerEvents: "none",
    zIndex: String(zIndex),
  });
};

const inverseTransform = (from: DOMRect, to: DOMRect) =>
  `translate(${from.left - to.left}px, ${from.top - to.top}px) scale(${from.width / to.width}, ${from.height / to.height})`;

const makeClone = (element: HTMLElement, deep: boolean) => {
  const clone = element.cloneNode(deep) as HTMLElement;
  clone.removeAttribute("id");
  clone.querySelectorAll("[id]").forEach((child) => child.removeAttribute("id"));
  clone.inert = true;
  clone.setAttribute("aria-hidden", "true");
  clone.classList.remove("player-motion-live");
  return clone;
};

export const usePlayerModeTransition = (options: PlayerModeTransitionOptions) => {
  const isSwitching = ref(false);
  let generation = 0;
  let disposed = false;
  let animations: Animation[] = [];
  let layers: HTMLElement[] = [];
  let surface: HTMLElement | null = null;
  let artwork: HTMLElement | null = null;
  let observer: ResizeObserver | undefined;
  let motionPreference: MediaQueryList | undefined;

  const cleanup = () => {
    animations.forEach((animation) => animation.cancel());
    animations = [];
    layers.forEach((layer) => layer.remove());
    layers = [];
    surface = null;
    artwork = null;
    options.card.value?.classList.remove("player-motion-live");
    if (options.layout.value) options.layout.value.style.height = "";
  };

  const settle = async () => {
    const token = ++generation;
    cleanup();
    isSwitching.value = true;
    await nextTick();
    if (disposed || token !== generation) return;
    if (options.mode.value === "pip") options.positionFloating();
    await nextTick();
    if (disposed || token !== generation) return;
    isSwitching.value = false;
  };

  const animate = (
    element: HTMLElement,
    keyframes: Keyframe[],
    timing: KeyframeAnimationOptions,
  ) => {
    const animation = element.animate(keyframes, { fill: "both", ...timing });
    animations.push(animation);
    return animation;
  };

  const toggle = async () => {
    const card = options.card.value;
    const layout = options.layout.value;
    if (!card || !layout || disposed) return;

    const token = ++generation;
    const from = (surface ?? card).getBoundingClientRect();
    const oldArtwork = artwork ?? card.querySelector<HTMLElement>(ARTWORK);
    const artFrom = oldArtwork?.getBoundingClientRect();
    const artClone = oldArtwork ? makeClone(oldArtwork, true) : null;
    const outgoing = makeClone(card, true);
    const focused = document.activeElement;
    const restoreFocus =
      focused instanceof HTMLElement && focused.hasAttribute("data-player-mode-toggle");
    // An interrupted surface supplies visible geometry, but its live controls already use the destination layout.
    const contentRect = card.getBoundingClientRect();
    const contentOpacity = getComputedStyle(card.children[0] ?? card).opacity;
    const reservedHeight = layout.getBoundingClientRect().height;
    cleanup();
    isSwitching.value = true;
    options.beforeChange();
    options.mode.value = options.mode.value === "strip" ? "pip" : "strip";
    if (options.mode.value === "pip") layout.style.height = `${reservedHeight}px`;

    await nextTick();
    if (disposed || token !== generation) return;
    if (options.mode.value === "pip") {
      options.positionFloating();
      await nextTick();
      if (disposed || token !== generation) return;
    }

    const to = card.getBoundingClientRect();
    if (
      restoreFocus &&
      (document.activeElement === focused || document.activeElement === document.body)
    ) {
      let target = card.querySelector<HTMLButtonElement>(
        `[data-player-mode-toggle="${options.mode.value}"]`,
      );
      if (!target?.getClientRects().length) {
        target = card.querySelector<HTMLButtonElement>(
          "#audio-strip [data-player-transport-focus]",
        );
      }
      target?.focus({ preventScroll: true });
    }
    const reduced =
      motionPreference?.matches ?? window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (reduced || !card.animate || !from.width || !from.height || !to.width || !to.height) {
      cleanup();
      isSwitching.value = false;
      return;
    }

    try {
      const duration = options.mode.value === "pip" ? 220 : 200;
      surface = makeClone(card, false);
      surface.classList.add("player-motion-surface");
      placeLayer(surface, to, 0);
      layout.append(surface);
      layers.push(surface);

      outgoing.classList.add("player-motion-snapshot");
      outgoing.querySelectorAll<HTMLElement>(ARTWORK).forEach((element) => {
        element.style.visibility = "hidden";
      });
      placeLayer(outgoing, contentRect, 3);
      layout.append(outgoing);
      layers.push(outgoing);

      const newArtwork = card.querySelector<HTMLElement>(ARTWORK);
      const artTo = newArtwork?.getBoundingClientRect();
      if (artClone && artFrom?.width && artFrom.height && artTo?.width && artTo.height) {
        artwork = artClone;
        artwork.classList.add("player-motion-artwork");
        placeLayer(artwork, artTo, 1);
        artwork.style.visibility = "visible";
        layout.append(artwork);
        layers.push(artwork);
        animate(artwork, [{ transform: inverseTransform(artFrom, artTo) }, { transform: "none" }], {
          duration,
          easing: EASING,
        });
      }

      card.classList.add("player-motion-live");
      animate(surface, [{ transform: inverseTransform(from, to) }, { transform: "none" }], {
        duration,
        easing: EASING,
      });
      animate(outgoing, [{ opacity: contentOpacity }, { opacity: 0 }], { duration: 70 });
      for (const child of card.children) {
        if (child instanceof HTMLElement) {
          animate(child, [{ opacity: 0 }, { opacity: 1 }], {
            duration: 120,
            delay: 60,
            easing: "ease-out",
          });
        }
      }
      await Promise.allSettled(animations.map((animation) => animation.finished));
    } finally {
      if (!disposed && token === generation) {
        cleanup();
        isSwitching.value = false;
      }
    }
  };

  onMounted(() => {
    motionPreference = window.matchMedia("(prefers-reduced-motion: reduce)");
    motionPreference.addEventListener("change", settle);
    window.addEventListener("resize", settle);
    let width = options.layout.value?.getBoundingClientRect().width;
    observer = new ResizeObserver(([entry]) => {
      if (!entry || entry.contentRect.width === width) return;
      width = entry.contentRect.width;
      void settle();
    });
    if (options.layout.value) observer.observe(options.layout.value);
  });

  onScopeDispose(() => {
    disposed = true;
    ++generation;
    observer?.disconnect();
    motionPreference?.removeEventListener("change", settle);
    window.removeEventListener("resize", settle);
    cleanup();
  });

  return { isSwitching, toggle, settle };
};
