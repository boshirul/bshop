import { AfterViewInit, Component, ElementRef, Inject, OnDestroy, ViewChild, signal } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import {
  CropState,
  drawSquareCrop,
  initialCropState,
  panCrop,
  PROFILE_PHOTO_OUTPUT_SIZE,
  withZoom
} from './profile-photo-editor';

@Component({
  selector: 'app-profile-photo-editor-dialog',
  imports: [MatDialogModule, MatButtonModule],
  template: `
    <h2 mat-dialog-title>Edit profile photo</h2>
    <mat-dialog-content>
      <p class="transaction-helper">Drag to reposition, then zoom until the photo looks right.</p>
      <div class="editor-layout">
        <div class="crop-frame" [class.loading]="loading()">
          <canvas #cropCanvas width="512" height="512" aria-label="Square profile photo crop"
            (pointerdown)="startDrag($event)" (pointermove)="drag($event)" (pointerup)="endDrag($event)" (pointercancel)="endDrag($event)"></canvas>
          @if (loading()) { <span>Preparing photo…</span> }
        </div>
        <div class="preview-block"><span>Avatar preview</span><canvas #previewCanvas width="128" height="128" aria-label="Circular avatar preview"></canvas></div>
      </div>
      <label class="zoom-control"><span>Zoom</span><input type="range" min="1" max="3" step="0.01" [value]="zoom()" (input)="changeZoom($event)" [disabled]="loading() || !!error()" /></label>
      @if (error()) { <p class="error-message" role="alert">{{ error() }}</p> }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" [disabled]="processing()" (click)="cancel()">Cancel</button>
      <button matButton="filled" class="transaction-primary" type="button" [disabled]="loading() || processing() || !!error()" (click)="save()">{{ processing() ? 'Processing…' : 'Save photo' }}</button>
    </mat-dialog-actions>
  `,
  styles: `
    :host{display:block}.editor-layout{align-items:center;display:grid;gap:1.25rem;grid-template-columns:minmax(0,20rem) 8rem;justify-content:center}.crop-frame{aspect-ratio:1;background:var(--bshop-color-bg);border:1px solid var(--bshop-color-border);border-radius:var(--bshop-radius-md);overflow:hidden;position:relative;touch-action:none}.crop-frame canvas{cursor:grab;display:block;height:100%;width:100%}.crop-frame canvas:active{cursor:grabbing}.crop-frame span{inset:0;display:grid;place-items:center;position:absolute}.crop-frame.loading canvas{opacity:.25}.preview-block{align-items:center;display:flex;flex-direction:column;gap:.5rem;text-align:center}.preview-block canvas{border:2px solid var(--bshop-color-border);border-radius:50%;height:6rem;width:6rem}.zoom-control{display:grid;gap:.4rem;margin-top:1rem}.zoom-control input{width:100%}@media(max-width:600px){.editor-layout{grid-template-columns:1fr}.crop-frame{margin:auto;max-width:20rem;width:100%}}
  `
})
export class ProfilePhotoEditorDialog implements AfterViewInit, OnDestroy {
  @ViewChild('cropCanvas') private cropCanvas!: ElementRef<HTMLCanvasElement>;
  @ViewChild('previewCanvas') private previewCanvas!: ElementRef<HTMLCanvasElement>;
  protected readonly loading = signal(true);
  protected readonly processing = signal(false);
  protected readonly error = signal('');
  protected readonly zoom = signal(1);
  private readonly image = new Image();
  private readonly sourceUrl: string;
  private state: CropState | null = null;
  private dragPoint: { x: number; y: number } | null = null;
  private disposed = false;

  constructor(
    @Inject(MAT_DIALOG_DATA) readonly file: File,
    private readonly ref: MatDialogRef<ProfilePhotoEditorDialog, File>
  ) {
    this.sourceUrl = URL.createObjectURL(file);
  }

  ngAfterViewInit(): void {
    this.image.onload = () => {
      if (this.disposed) return;
      this.state = initialCropState(this.image.naturalWidth, this.image.naturalHeight);
      this.loading.set(false);
      this.render();
    };
    this.image.onerror = () => {
      if (this.disposed) return;
      this.loading.set(false);
      this.error.set('The selected file could not be read as an image.');
    };
    this.image.src = this.sourceUrl;
  }

  ngOnDestroy(): void {
    this.disposed = true;
    this.image.onload = null;
    this.image.onerror = null;
    this.image.src = '';
    URL.revokeObjectURL(this.sourceUrl);
  }

  protected changeZoom(event: Event): void {
    if (!this.state) return;
    const value = Number((event.target as HTMLInputElement).value);
    this.state = withZoom(this.state, value);
    this.zoom.set(this.state.zoom);
    this.render();
  }

  protected startDrag(event: PointerEvent): void {
    if (!this.state) return;
    this.dragPoint = { x: event.clientX, y: event.clientY };
    (event.currentTarget as HTMLCanvasElement).setPointerCapture(event.pointerId);
  }

  protected drag(event: PointerEvent): void {
    if (!this.state || !this.dragPoint) return;
    const canvas = this.cropCanvas.nativeElement;
    const viewport = canvas.getBoundingClientRect().width || canvas.width;
    this.state = panCrop(this.state, event.clientX - this.dragPoint.x, event.clientY - this.dragPoint.y, viewport);
    this.dragPoint = { x: event.clientX, y: event.clientY };
    this.render();
  }

  protected endDrag(event: PointerEvent): void {
    const canvas = event.currentTarget as HTMLCanvasElement;
    if (canvas.hasPointerCapture(event.pointerId)) canvas.releasePointerCapture(event.pointerId);
    this.dragPoint = null;
  }

  protected cancel(): void { if (!this.processing()) this.ref.close(); }

  protected async save(): Promise<void> {
    if (!this.state || this.processing()) return;
    this.processing.set(true);
    try {
      const canvas = document.createElement('canvas');
      canvas.width = PROFILE_PHOTO_OUTPUT_SIZE;
      canvas.height = PROFILE_PHOTO_OUTPUT_SIZE;
      const context = canvas.getContext('2d', { alpha: true });
      if (!context) throw new Error('Canvas is unavailable.');
      drawSquareCrop(context, this.image, this.state, PROFILE_PHOTO_OUTPUT_SIZE);
      const blob = await canvasBlob(canvas, 'image/webp', 0.88);
      if (blob.size > 2 * 1024 * 1024) throw new Error('The optimized profile photo is larger than 2 MB.');
      this.ref.close(new File([blob], 'profile-photo.webp', { type: blob.type, lastModified: Date.now() }));
    } catch {
      this.error.set('The cropped photo could not be created. Please try another image.');
      this.processing.set(false);
    }
  }

  private render(): void {
    if (!this.state) return;
    const crop = this.cropCanvas.nativeElement.getContext('2d');
    const preview = this.previewCanvas.nativeElement.getContext('2d');
    if (!crop || !preview) { this.error.set('Image editing is not supported by this browser.'); return; }
    drawSquareCrop(crop, this.image, this.state, this.cropCanvas.nativeElement.width);
    drawSquareCrop(preview, this.image, this.state, this.previewCanvas.nativeElement.width);
  }
}

function canvasBlob(canvas: HTMLCanvasElement, type: string, quality: number): Promise<Blob> {
  return new Promise((resolve, reject) => canvas.toBlob(blob => blob ? resolve(blob) : reject(new Error('Image encoding failed.')), type, quality));
}
