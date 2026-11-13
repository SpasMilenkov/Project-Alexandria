// The crop viewport is a square the image always fully covers. Offsets are the image centre's
// distance from the viewport centre in on-screen pixels, so drags map onto them one to one.

export const AVATAR_OUTPUT_SIZE = 512;
export const AVATAR_MAX_ZOOM = 3;
export const AVATAR_MIN_ZOOM = 1;

// Large phone photos decode fine, but anything past this is unlikely to be a picture.
export const AVATAR_MAX_INPUT_BYTES = 25 * 1024 * 1024;

// GIFs skip the crop and re-encode to keep their animation, so these mirror the server's limits.
export const AVATAR_MAX_GIF_BYTES = 4 * 1024 * 1024;
export const AVATAR_MAX_GIF_DIMENSION = 1024;

export const isGifFile = (file: File | null) => file?.type === "image/gif";

export const gifFits = (width: number, height: number) =>
  width <= AVATAR_MAX_GIF_DIMENSION && height <= AVATAR_MAX_GIF_DIMENSION;

export interface CropFrame {
  naturalHeight: number;
  naturalWidth: number;
  viewport: number;
}

export interface CropState {
  offsetX: number;
  offsetY: number;
  zoom: number;
}

export interface SourceRect {
  size: number;
  x: number;
  y: number;
}

export const clampZoom = (zoom: number) =>
  Math.min(AVATAR_MAX_ZOOM, Math.max(AVATAR_MIN_ZOOM, zoom));

export const displayScale = (frame: CropFrame, zoom: number) =>
  (frame.viewport / Math.min(frame.naturalWidth, frame.naturalHeight)) * clampZoom(zoom);

export const clampOffsets = (frame: CropFrame, state: CropState): CropState => {
  const zoom = clampZoom(state.zoom);
  const scale = displayScale(frame, zoom);
  const maxX = Math.max(0, (frame.naturalWidth * scale - frame.viewport) / 2);
  const maxY = Math.max(0, (frame.naturalHeight * scale - frame.viewport) / 2);

  return {
    offsetX: Math.min(maxX, Math.max(-maxX, state.offsetX)),
    offsetY: Math.min(maxY, Math.max(-maxY, state.offsetY)),
    zoom,
  };
};

// Zooming keeps the point under the viewport centre where it is.
export const zoomTo = (frame: CropFrame, state: CropState, nextZoom: number): CropState => {
  const zoom = clampZoom(nextZoom);
  const ratio = zoom / clampZoom(state.zoom);

  return clampOffsets(frame, {
    offsetX: state.offsetX * ratio,
    offsetY: state.offsetY * ratio,
    zoom,
  });
};

export const panBy = (frame: CropFrame, state: CropState, dx: number, dy: number): CropState =>
  clampOffsets(frame, { ...state, offsetX: state.offsetX + dx, offsetY: state.offsetY + dy });

export const sourceRect = (frame: CropFrame, state: CropState): SourceRect => {
  const clamped = clampOffsets(frame, state);
  const scale = displayScale(frame, clamped.zoom);
  const size = frame.viewport / scale;

  return {
    size,
    x: frame.naturalWidth / 2 - clamped.offsetX / scale - size / 2,
    y: frame.naturalHeight / 2 - clamped.offsetY / scale - size / 2,
  };
};

const toBlob = (canvas: HTMLCanvasElement, type: string, quality: number) =>
  new Promise<Blob | null>((resolve) => {
    canvas.toBlob(resolve, type, quality);
  });

// Re-encoding through a canvas also drops EXIF, including any GPS position from a phone camera.
export const renderAvatar = async (
  image: CanvasImageSource,
  frame: CropFrame,
  state: CropState,
): Promise<Blob> => {
  const canvas = document.createElement("canvas");
  canvas.width = AVATAR_OUTPUT_SIZE;
  canvas.height = AVATAR_OUTPUT_SIZE;

  const context = canvas.getContext("2d");
  if (!context) throw new Error("This browser cannot prepare images.");

  const rect = sourceRect(frame, state);
  context.imageSmoothingQuality = "high";
  context.drawImage(
    image,
    rect.x,
    rect.y,
    rect.size,
    rect.size,
    0,
    0,
    AVATAR_OUTPUT_SIZE,
    AVATAR_OUTPUT_SIZE,
  );

  // Browsers without a WebP encoder silently hand back PNG, which is far larger than JPEG.
  const webp = await toBlob(canvas, "image/webp", 0.9);
  if (webp?.type === "image/webp") return webp;

  const jpeg = await toBlob(canvas, "image/jpeg", 0.9);
  if (!jpeg) throw new Error("This browser could not encode the picture.");

  return jpeg;
};
