export interface CartItem {
  cartItemId: number;
  productId: number;
  productVariantId: number | null;
  name: string;
  slug: string;
  imageUrl: string | null;
  variantLabel: string | null;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
  availableQty: number;
  inStock: boolean;
}

export interface Cart {
  cartId: number;
  items: CartItem[];
  itemCount: number;
  distinctCount: number;
  subtotal: number;
}
