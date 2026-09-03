export interface CreateEventRequest {
  name: string;
  description?: string;
  eventDate: string;
  pricePerPhoto: number;
  originalPrice?: number | null;
  priceType: ProductPriceType;
  productType: ProductType;
  paymentMethods: string;
  buyerInstructions?: string;
  deliveryLink?: string;
  isPublished: boolean;
}

export interface UpdateEventRequest {
  name: string;
  description?: string;
  eventDate: string;
  pricePerPhoto: number;
  originalPrice?: number | null;
  priceType: ProductPriceType;
  productType: ProductType;
  paymentMethods: string;
  buyerInstructions?: string;
  deliveryLink?: string;
  isPublished: boolean;
}

export type ProductPriceType = 'paid' | 'free';
export type ProductType = 'digital_file' | 'digital_link' | 'physical';

export interface EventPhotoDto {
  id: number;
  originalFileName: string;
  url: string;
  sizeBytes: number;
  watermarkApplied: boolean;
  createdAt: string;
}

export interface PhotographerEventDto {
  id: number;
  name: string;
  description?: string;
  eventDate: string;
  pricePerPhoto: number;
  originalPrice?: number | null;
  priceType: ProductPriceType;
  productType: ProductType;
  paymentMethods: string;
  buyerInstructions?: string;
  deliveryLink?: string;
  coverImageUrl?: string | null;
  isPublished: boolean;
  photoCount: number;
  createdAt: string;
  photos: EventPhotoDto[];
  productAssets: ProductAssetDto[];
}

export interface ProductAssetDto {
  id: number;
  kind: 'cover' | 'digital_file';
  originalFileName: string;
  objectKey: string;
  url?: string | null;
  contentType: string;
  sizeBytes: number;
  createdAt: string;
}
