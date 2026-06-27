export interface AuthProvider {
  provider: string;            // EmailPassword | MobileOtp | Google
  displayName: string | null;
  isEnabled: boolean;
  allowRegistration: boolean;
  displayOrder: number;
  clientId: string | null;
}

export interface AuthConfig {
  providers: AuthProvider[];
}

export interface AuthUser {
  userId: number;
  email: string | null;
  fullName: string | null;
  phoneNumber: string | null;
  roles: string[];
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAtUtc: string;
  user: AuthUser;
}

export interface RegisterRequest {
  email: string;
  password: string;
  fullName?: string | null;
  phoneNumber?: string | null;
}

export interface LoginRequest {
  email: string;
  password: string;
}
