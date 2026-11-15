// Shared overlay preset. Single source for modal and drawer surfaces so
// the look cannot drift per instance. Overlays are intentionally SOLID: they
// sit over a dimmed scrim where translucency is perceptually invisible, so
// glass would only cost legibility and mobile blur performance. The theme
// `bg-default` token follows light/dark mode via CSS variables.
export const glassOverlayContent = "bg-default";

export const glassModalContent = glassOverlayContent;

export const glassModalContentWide = `${glassOverlayContent} sm:max-w-2xl`;

// Desktop modal that grows to the right while a side preview pane is open. It is
// pinned to the left edge a centred 2xl modal would have, so only the right edge
// moves. The left edge gives way just enough to keep the wide modal on screen:
// nothing from 88rem up, a few rem below. Growing waits for the pane content to
// fade out when closing, and leads it when opening.
const previewModalBase =
  "bg-default translate-x-0 transition-[max-width,left] duration-200 ease-[cubic-bezier(0.2,0,0,1)] motion-reduce:transition-none";

export const previewModalContent = `${previewModalBase} sm:max-w-2xl left-[calc(50vw-21rem)] delay-100`;

export const previewModalContentExpanded = `${previewModalBase} sm:max-w-5xl left-[min(calc(50vw-21rem),calc(100vw-65rem))] delay-0`;

export const glassModalBody = "space-y-4";

export const glassDrawerContent = glassOverlayContent;

// Form modals own their scroll region and pinned footer inside the body, so
// the body itself drops its padding and overflow.
export const formModalUi = {
  content: glassModalContentWide,
  header: "shrink-0 p-6 md:px-8",
  title: "text-xl font-semibold",
  body: "flex min-h-0 flex-col overflow-hidden p-0 sm:p-0",
};

export const playlistModalUi = {
  content: `${glassModalContent} sm:max-w-3xl`,
  header: "shrink-0 p-6 md:px-8",
  title: "text-xl font-semibold",
  body: "flex min-h-0 flex-col overflow-hidden p-0 sm:p-0",
};
