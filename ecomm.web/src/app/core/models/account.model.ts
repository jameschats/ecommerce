export interface Profile {
  userId: number;
  email: string | null;
  fullName: string | null;
  phoneNumber: string | null;
  roles: string[];
  isEmailVerified: boolean;
}

export interface UpdateProfileRequest {
  fullName?: string | null;
  phoneNumber?: string | null;
}

export interface Address {
  customerAddressId: number;
  label: string | null;
  recipientName: string | null;
  phone: string | null;
  line1: string;
  line2: string | null;
  city: string;
  state: string;
  pincode: string;
  country: string;
  addressType: string;
  isDefault: boolean;
}

export interface SaveAddressRequest {
  label?: string | null;
  recipientName?: string | null;
  phone?: string | null;
  line1: string;
  line2?: string | null;
  city: string;
  state: string;
  pincode: string;
  country?: string | null;
  addressType?: string | null;
  isDefault: boolean;
}
