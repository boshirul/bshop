import { Injectable, computed, signal } from '@angular/core';
import { OnlineProductListItem } from './online-store.models';

export type OnlineStoreCartLine = OnlineProductListItem & { quantity: number };

@Injectable({ providedIn: 'root' })
export class OnlineStoreCartService {
  readonly lines = signal<OnlineStoreCartLine[]>([]);
  readonly isDrawerOpen = signal(false);
  readonly lastNotice = signal('');
  readonly itemCount = computed(() =>
    this.lines().reduce((count, line) => count + line.quantity, 0));
  readonly subtotal = computed(() =>
    this.lines().reduce((sum, line) => sum + line.salePrice * line.quantity, 0));

  add(product: OnlineProductListItem): 'added' | 'limit' | 'unavailable' {
    if (product.availableQuantity <= 0) {
      this.lastNotice.set('This product is currently unavailable.');
      return 'unavailable';
    }
    let result: 'added' | 'limit' = 'added';
    this.lines.update(lines => {
      const existing = lines.find(line => line.id === product.id);
      if (existing) {
        if (existing.quantity >= existing.availableQuantity) {
          result = 'limit';
          return lines;
        }
        return lines.map(line => line.id === product.id
          ? { ...line, quantity: Math.min(line.quantity + 1, line.availableQuantity) }
          : line);
      }
      return [...lines, { ...product, quantity: 1 }];
    });
    this.lastNotice.set(result === 'added'
      ? `${product.name} was added to your cart.`
      : `Only ${product.availableQuantity} ${product.unitSymbol} are currently available.`);
    return result;
  }

  setQuantity(id: string, requested: number): void {
    this.lines.update(lines => lines.map(line => line.id === id
      ? { ...line, quantity: Math.max(1, Math.min(Number.isFinite(requested) ? requested : 1, line.availableQuantity)) }
      : line));
  }

  remove(id: string): void { this.lines.update(lines => lines.filter(line => line.id !== id)); }
  openDrawer(): void { this.isDrawerOpen.set(true); }
  closeDrawer(): void { this.isDrawerOpen.set(false); }
  clear(): void { this.lines.set([]); this.lastNotice.set(''); }
}
