export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  fullName?: string;
  phoneNumber?: string;
  publicSlug?: string;
}

export interface UserDto {
  id: number;
  email: string;
  fullName?: string | null;
  publicSlug?: string | null;
  phoneNumber?: string | null;
  withdrawalHolderName?: string | null;
  withdrawalBankName?: string | null;
  withdrawalAliasOrCbu?: string | null;
  isActive: boolean;
  emailVerified: boolean;
  isReadOnly: boolean;
  role: string;
  isAdmin: boolean;
  planType: string;
  usageTypeName: string;
}

export interface LoginResponse {
  token: string;
  refreshToken: string;
  user: UserDto;
}

export interface RegisterResponse {
  message: string;
  code: string;
  email?: string;
  verificationUrl?: string;
}

export interface ForgotPasswordRequest {
  email: string;
}

export interface ResetPasswordRequest {
  token: string;
  newPassword: string;
}

export interface AuthConfigResponse {
  googleAuthEnabled: boolean;
  registrationEnabled: boolean;
}

export interface AuthAvailabilityResponse {
  emailAvailable: boolean;
  publicSlugAvailable: boolean;
}
