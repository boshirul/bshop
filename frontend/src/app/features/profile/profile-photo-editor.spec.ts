import '@angular/compiler';
import { describe, expect, it, vi } from 'vitest';
import { ProfilePhotoEditorDialog } from './profile-photo-editor-dialog';
import {
  cropSourceRect,
  drawSquareCrop,
  initialCropState,
  panCrop,
  PROFILE_PHOTO_MAX_BYTES,
  PROFILE_PHOTO_OUTPUT_SIZE,
  validateProfilePhoto,
  withZoom
} from './profile-photo-editor';

describe('profile photo editing', () => {
  it('accepts supported image files', () => {
    expect(validateProfilePhoto(new File(['image'], 'avatar.jpg', { type: 'image/jpeg' }))).toBeNull();
    expect(validateProfilePhoto(new File(['image'], 'avatar.png', { type: 'image/png' }))).toBeNull();
    expect(validateProfilePhoto(new File(['image'], 'avatar.webp', { type: 'image/webp' }))).toBeNull();
  });

  it('rejects unsupported formats', () => {
    expect(validateProfilePhoto(new File(['image'], 'avatar.gif', { type: 'image/gif' }))).toContain('Only JPEG, PNG, and WebP');
  });

  it('rejects images over the upload limit', () => {
    const file = new File([new Uint8Array(PROFILE_PHOTO_MAX_BYTES + 1)], 'large.jpg', { type: 'image/jpeg' });
    expect(validateProfilePhoto(file)).toContain('2 MB or smaller');
  });

  it('draws a square 512 pixel crop without distortion', () => {
    const drawImage = vi.fn();
    const context = { clearRect: vi.fn(), drawImage } as unknown as CanvasRenderingContext2D;
    const image = {} as CanvasImageSource;
    drawSquareCrop(context, image, initialCropState(1200, 800), PROFILE_PHOTO_OUTPUT_SIZE);
    expect(drawImage).toHaveBeenCalledWith(image, 200, 0, 800, 800, 0, 0, 512, 512);
  });

  it('zooms around the crop center and clamps zoom limits', () => {
    const state = initialCropState(1200, 800);
    expect(cropSourceRect(withZoom(state, 2))).toEqual({ x: 400, y: 200, size: 400 });
    expect(withZoom(state, 99).zoom).toBe(3);
    expect(withZoom(state, 0).zoom).toBe(1);
  });

  it('repositions the crop and prevents it leaving the image', () => {
    const state = withZoom(initialCropState(1200, 800), 2);
    const moved = panCrop(state, -10_000, -10_000, 320);
    const rect = cropSourceRect(moved);
    expect(rect.x).toBe(800);
    expect(rect.y).toBe(400);
    expect(rect.x + rect.size).toBe(1200);
    expect(rect.y + rect.size).toBe(800);
  });

  it('disposes its source object URL on cancel and repeated editor cleanup', () => {
    const create = vi.spyOn(URL, 'createObjectURL').mockReturnValueOnce('blob:first').mockReturnValueOnce('blob:second');
    const revoke = vi.spyOn(URL, 'revokeObjectURL').mockImplementation(() => undefined);
    const close = vi.fn();
    const first = new ProfilePhotoEditorDialog(new File(['a'], 'a.jpg', { type: 'image/jpeg' }), { close } as never);
    first.ngOnDestroy();
    const second = new ProfilePhotoEditorDialog(new File(['b'], 'b.jpg', { type: 'image/jpeg' }), { close } as never);
    second.ngOnDestroy();
    expect(revoke.mock.calls).toEqual([['blob:first'], ['blob:second']]);
    create.mockRestore(); revoke.mockRestore();
  });
});
