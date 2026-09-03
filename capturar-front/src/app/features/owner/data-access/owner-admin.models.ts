export interface AdminDashboardDto {
  totalUsers: number;
  userGrowthPercent: number;
  newUsersThisMonth: number;
  newUsersGrowthPercent: number;
  totalStorageUsedBytes: number;
}

export interface AdminUserListDto {
  id: number;
  email: string;
  fullName?: string | null;
  isActive: boolean;
  role: string;
  planType: string;
  isProActive: boolean;
  storageUsedBytes: number;
  createdAt: string;
  lastLogin?: string | null;
}

export interface AdminUsersPagedDto {
  users: AdminUserListDto[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface AdminUserDetailDto {
  id: number;
  email: string;
  fullName?: string | null;
  phoneNumber?: string | null;
  isActive: boolean;
  emailVerified: boolean;
  role: string;
  planType: string;
  isProActive: boolean;
  trialUsed: boolean;
  trialDaysRemaining: number;
  proSubscriptionStartDate?: string | null;
  proSubscriptionEndDate?: string | null;
  createdAt: string;
  lastLogin?: string | null;
  usageTypeName: string;
  storageUsedBytes: number;
}

export interface OwnerStudioSalesDto {
  photographerId: number;
  studioName: string;
  studioEmail: string;
  ordersCount: number;
  grossTotal: number;
  platformCommissionTotal: number;
  netTotal: number;
  lastSaleAt?: string | null;
}

export interface OwnerGlobalSalesSummaryDto {
  grossTotal: number;
  platformCommissionTotal: number;
  netTotal: number;
  ordersCount: number;
  studios: OwnerStudioSalesDto[];
}

export interface OwnerTransferApprovalItemDto {
  externalReference: string;
  photographerId: number;
  studioName: string;
  studioEmail: string;
  eventId: number;
  eventName: string;
  buyerName: string;
  buyerEmail: string;
  photoCount: number;
  totalAmount: number;
  submittedAt: string;
  status: string;
}

export interface OwnerTransferApprovalListDto {
  totalCount: number;
  items: OwnerTransferApprovalItemDto[];
}

export interface OwnerAccreditationPhotographerDto {
  photographerId: number;
  studioName: string;
  studioEmail: string;
  withdrawalHolderName?: string | null;
  withdrawalBankName?: string | null;
  withdrawalAliasOrCbu?: string | null;
  pendingAmount: number;
  availableAmount: number;
  totalWithdrawn: number;
  toAccreditNow: number;
}

export interface OwnerAccreditationSummaryDto {
  totalPending: number;
  totalAvailable: number;
  totalWithdrawn: number;
  totalToAccreditNow: number;
  photographersCount: number;
  photographers: OwnerAccreditationPhotographerDto[];
}

export interface OwnerPhotoDeliveryFailureItemDto {
  externalReference: string;
  status: string;
  photographerId: number;
  studioName: string;
  studioEmail: string;
  eventId: number;
  eventName: string;
  buyerName: string;
  buyerEmail: string;
  photoCount: number;
  deliveryEmailStatus: string;
  deliveryEmailAttempts: number;
  deliveryEmailLastAttemptAt?: string | null;
  deliveryEmailSentAt?: string | null;
  deliveryEmailError?: string | null;
  paidAt?: string | null;
  createdAt: string;
}

export interface OwnerPhotoDeliveryFailuresDto {
  totalCount: number;
  items: OwnerPhotoDeliveryFailureItemDto[];
}
