// Shared overlay preset. Single source for modal and drawer surfaces so
// the look cannot drift per instance. Overlays are intentionally SOLID: they
// sit over a dimmed scrim where translucency is perceptually invisible, so
// glass would only cost legibility and mobile blur performance. The theme
// `bg-default` token follows light/dark mode via CSS variables.
export const glassOverlayContent = "bg-default";

export const glassModalContent = glassOverlayContent;

export const glassModalContentWide = `${glassOverlayContent} sm:max-w-2xl`;

export const glassModalBody = "space-y-4";

export const glassDrawerContent = glassOverlayContent;
