export interface MercadoPagoConnectResponse {
  authorizationUrl: string;
}

export interface MercadoPagoConnectionStatusResponse {
  connected: boolean;
  mercadoPagoUserId?: string | null;
  tokenExpiration?: string | null;
  tokenExpired: boolean;
  publicKey?: string | null;
}
