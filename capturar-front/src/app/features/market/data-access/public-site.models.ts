export interface PublicEventCard {
  id: number;
  name: string;
  description?: string | null;
  eventDate: string;
  pricePerPhoto: number;
  originalPrice?: number | null;
  priceType?: 'paid' | 'free';
  productType?: 'digital_file' | 'digital_link' | 'physical';
  paymentMethods?: string;
  photoCount: number;
  digitalAssetCount?: number;
  coverPhotoUrl?: string | null;
}

export interface SiteTheme {
  accent: string;
  background: string;
  surface: string;
  text: string;
}

export interface PublicStudio {
  userId: number;
  studioName: string;
  slug: string;
  theme?: SiteTheme | null;
  events: PublicEventCard[];
}

export interface PublicPhoto {
  id: number;
  url: string;
  tags: string[];
  originalFileName: string;
}

export interface PublicEventDetail {
  id: number;
  studioName: string;
  studioSlug: string;
  name: string;
  description?: string | null;
  eventDate: string;
  pricePerPhoto: number;
  originalPrice?: number | null;
  priceType?: 'paid' | 'free';
  productType?: 'digital_file' | 'digital_link' | 'physical';
  paymentMethods?: string;
  digitalAssetCount?: number;
  coverPhotoUrl?: string | null;
  photos: PublicPhoto[];
  totalPhotos?: number;
  page?: number;
  pageSize?: number;
  hasMore?: boolean;
}

export interface PublicPhotoCheckoutRequest {
  photoIds: number[];
  buyerEmail: string;
  buyerName?: string | null;
  discountCode?: string | null;
}

export interface PublicPhotoCheckoutResponse {
  success: boolean;
  message: string;
  checkoutUrl?: string | null;
  preferenceId?: string | null;
  externalReference?: string | null;
  paymentMethod?: string | null;
  transferData?: PublicTransferPaymentData | null;
  subtotalAmount: number;
  discountAmount: number;
  totalAmount: number;
  currency: string;
}

export interface PublicTransferReceiptResponse {
  success: boolean;
  message: string;
  externalReference?: string | null;
  status?: string | null;
}

export interface PublicTransferPaymentData {
  holderName: string;
  bankName: string;
  alias?: string | null;
  cbu?: string | null;
  accountInfo?: string | null;
  amount: string;
  currency: string;
  reference: string;
}
