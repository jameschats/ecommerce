export interface StaffMember {
  userId: number;
  fullName: string | null;
  email: string | null;
  accessLevel: string;   // Owner | Admin | Staff | Viewer
  status: string;        // Active | Disabled
  lastLoginAt: string | null;
  createdAt: string;
  isYou: boolean;
}

export interface StaffRoleInfo {
  key: string;
  label: string;
  description: string;
  canManageStaff: boolean;
}

export interface InviteStaffRequest {
  fullName: string | null;
  email: string | null;
  accessLevel: string;
  password: string | null;
}
