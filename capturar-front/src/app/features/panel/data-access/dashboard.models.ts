export interface DashboardSummaryDto {
  totalSalesThisMonth: number;
  totalSalesAllTime: number;
  pendingAmount: number;
  availableAmount: number;
  nextAvailableAt?: string | null;
  totalWithdrawn: number;
  photosSoldThisMonth: number;
  totalPhotosSold: number;
  activeEventsCount: number;
}

export interface EventSalesDto {
  eventId: number;
  eventName: string;
  productType: string;
  totalSales: number;
  photosSold: number;
  pendingAmount: number;
  availableAmount: number;
}

export interface SaleDetailDto {
  saleId: number;
  soldAt: string;
  eventName: string;
  productType: string;
  buyerName: string;
  buyerEmail?: string | null;
  paymentMethod: string;
  totalAmount: number;
  quantity: number;
  status: string;
  externalReference?: string | null;
  purchasedPhotos: SalePurchasedPhotoDto[];
}

export interface SalePurchasedPhotoDto {
  photoId: number;
  label: string;
}

export interface ApproveTransferResultDto {
  success: boolean;
  message: string;
  externalReference?: string | null;
  status?: string;
}

export interface LiquidationDto {
  liquidationId: number;
  liquidationDate: string;
  amount: number;
  fromDate: string;
  toDate: string;
  ordersCount: number;
}

export interface WithdrawalResultDto {
  success: boolean;
  message: string;
  amount: number;
  processedAt: string;
}
