import { Injectable, computed, signal } from '@angular/core';
import { OnlineProductListItem } from './online-store.models';

export type OnlineStoreCartLine = OnlineProductListItem & { quantity: number };

@Injectable({ providedIn: 'root' })
export class OnlineStoreCartService {
  readonly lines = signal<OnlineStoreCartLine[]>([]);
  readonly subtotal = computed(() =>
    this.lines().reduce((sum, line) => sum + line.salePrice * line.quantity, 0));

  add(product: OnlineProductListItem): void {
    if (product.availableQuantity <= 0) return;
    this.lines.update(lines => {
      const existing = lines.find(line => line.id === product.id);
      if (existing) {
        return lines.map(line => line.id === product.id
          ? { ...line, quantity: Math.min(line.quantity + 1, line.availableQuantity) }
          : line);
      }
      return [...lines, { ...product, quantity: 1 }];
    });
  }

  setQuantity(id: string, requested: number): void {
    this.lines.update(lines => lines.map(line => line.id === id
      ? { ...line, quantity: Math.max(1, Math.min(Number.isFinite(requested) ? requested : 1, line.availableQuantity)) }
      : line));
  }

  remove(id: string): void { this.lines.update(lines => lines.filter(line => line.id !== id)); }
  clear(): void { this.lines.set([]); }
}
