export interface Point {
  x: number;
  y: number;
}

export interface Size {
  width: number;
  height: number;
}

/** A scale around the photo's centre plus a pan, in screen pixels. */
export interface ZoomState {
  scale: number;
  x: number;
  y: number;
}

export const MIN_SCALE = 1;
export const MAX_SCALE = 8;

export const RESTING: ZoomState = { scale: MIN_SCALE, x: 0, y: 0 };

const clamp = (value: number, low: number, high: number) => Math.min(high, Math.max(low, value));

/**
 * Scales to `scale` while keeping the content under `point` where it is on screen. The point
 * is relative to the photo's resting centre, which is also its transform origin.
 */
export const zoomAround = (state: ZoomState, point: Point, scale: number): ZoomState => {
  const next = clamp(scale, MIN_SCALE, MAX_SCALE);
  const ratio = next / state.scale;

  return {
    scale: next,
    x: point.x - (point.x - state.x) * ratio,
    y: point.y - (point.y - state.y) * ratio,
  };
};

// A photo smaller than the view on an axis stays put on it. A larger one may pan until its
// edge meets the view's edge. Zero stays allowed on both, so the photo never jumps as it
// grows past the view mid-gesture.
const panLimits = (content: number, view: number, offset: number) => ({
  high: Math.max(0, (content - view) / 2 - offset),
  low: Math.min(0, (view - content) / 2 - offset),
});

/**
 * Keeps a zoomed photo covering the view instead of sliding off it. `offset` is how far the
 * photo's resting centre sits from the view's centre, since the toolbar pads the top more.
 */
export const clampZoom = (state: ZoomState, frame: Size, view: Size, offset: Point): ZoomState => {
  const scale = clamp(state.scale, MIN_SCALE, MAX_SCALE);
  const horizontal = panLimits(frame.width * scale, view.width, offset.x);
  const vertical = panLimits(frame.height * scale, view.height, offset.y);

  return {
    scale,
    x: clamp(state.x, horizontal.low, horizontal.high),
    y: clamp(state.y, vertical.low, vertical.high),
  };
};

const WHEEL_LINE_PX = 16;
// Pinching a touchpad sends small wheel steps with ctrl held; a mouse notch is about 100px.
const PINCH_SENSITIVITY = 0.01;
const WHEEL_SENSITIVITY = 0.0015;

/** The scale factor one wheel event asks for: above 1 zooms in, below 1 zooms out. */
export const wheelZoomFactor = (event: Pick<WheelEvent, "deltaY" | "deltaMode" | "ctrlKey">) => {
  const pixels = event.deltaMode === 1 ? event.deltaY * WHEEL_LINE_PX : event.deltaY;
  const sensitivity = event.ctrlKey ? PINCH_SENSITIVITY : WHEEL_SENSITIVITY;

  return Math.exp(-pixels * sensitivity);
};
