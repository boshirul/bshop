export const PROFILE_PHOTO_MAX_BYTES = 2 * 1024 * 1024;
export const PROFILE_PHOTO_OUTPUT_SIZE = 512;
export const PROFILE_PHOTO_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const;

export interface CropState {
  imageWidth: number;
  imageHeight: number;
  centerX: number;
  centerY: number;
  zoom: number;
}

export interface CropSourceRect { x: number; y: number; size: number; }

export function validateProfilePhoto(file: File): string | null {
  if (!PROFILE_PHOTO_TYPES.includes(file.type as typeof PROFILE_PHOTO_TYPES[number])) {
    return 'Only JPEG, PNG, and WebP profile photos are supported.';
  }
  if (file.size > PROFILE_PHOTO_MAX_BYTES) return 'Profile photos must be 2 MB or smaller.';
  return null;
}

export function initialCropState(imageWidth: number, imageHeight: number): CropState {
  return { imageWidth, imageHeight, centerX: imageWidth / 2, centerY: imageHeight / 2, zoom: 1 };
}

export function cropSourceRect(state: CropState): CropSourceRect {
  const size = Math.min(state.imageWidth, state.imageHeight) / state.zoom;
  const half = size / 2;
  const centerX = clamp(state.centerX, half, state.imageWidth - half);
  const centerY = clamp(state.centerY, half, state.imageHeight - half);
  return { x: centerX - half, y: centerY - half, size };
}

export function withZoom(state: CropState, zoom: number): CropState {
  const next = { ...state, zoom: clamp(zoom, 1, 3) };
  const rect = cropSourceRect(next);
  return { ...next, centerX: rect.x + rect.size / 2, centerY: rect.y + rect.size / 2 };
}

export function panCrop(state: CropState, deltaX: number, deltaY: number, viewportSize: number): CropState {
  const rect = cropSourceRect(state);
  const sourcePerPixel = rect.size / viewportSize;
  const next = { ...state, centerX: state.centerX - deltaX * sourcePerPixel, centerY: state.centerY - deltaY * sourcePerPixel };
  const clamped = cropSourceRect(next);
  return { ...next, centerX: clamped.x + clamped.size / 2, centerY: clamped.y + clamped.size / 2 };
}

export function drawSquareCrop(
  context: CanvasRenderingContext2D,
  image: CanvasImageSource,
  state: CropState,
  outputSize: number
): void {
  const source = cropSourceRect(state);
  context.clearRect(0, 0, outputSize, outputSize);
  context.drawImage(image, source.x, source.y, source.size, source.size, 0, 0, outputSize, outputSize);
}

function clamp(value: number, minimum: number, maximum: number): number {
  return Math.min(Math.max(value, minimum), maximum);
}
