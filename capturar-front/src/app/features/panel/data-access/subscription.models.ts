export interface PlanStatus {
  planType: 'FREE' | 'PRO' | 'PRO_TRIAL' | string;
  isProActive: boolean;
  trialUsed: boolean;
  canActivateTrial: boolean;
  trialStartDate?: string | null;
  trialEndDate?: string | null;
  trialDaysRemaining: number;
  proSubscriptionStartDate?: string | null;
  proSubscriptionEndDate?: string | null;
  proDaysRemaining: number;
  monthlyPrice: number;
  annualPrice: number;
  currency: string;
  priceDisplay: string;
  annualPriceDisplay: string;
  paymentEnabled: boolean;
  mercadoPagoEnabled: boolean;
}

export interface MercadoPagoCheckoutResponse {
  success: boolean;
  message: string;
  checkoutUrl?: string | null;
  preferenceId?: string | null;
  amount: number;
  currency: string;
}

export interface PaymentConfirmationResponse {
  success: boolean;
  message: string;
}
