export interface PricedProduct {
  id: string;
  salePrice: number;
}

/**
 * A product change is a new pricing decision. Returning null for an unchanged
 * selection preserves an approved manual override on the same sale line.
 */
export function priceForProductChange(
  previousProductId: string | undefined,
  selectedProductId: string,
  products: readonly PricedProduct[]
): number | null {
  if (!selectedProductId || selectedProductId === previousProductId) return null;
  return products.find((product) => product.id === selectedProductId)?.salePrice ?? null;
}
