export interface ApiResponse<T> {
  succeeded: boolean;
  data: T | null;
  message: string | null;
  errors: string[];
}

export interface UserSummary {
  id: string;
  email: string;
  fullName: string;
  phoneNumber: string | null;
  isActive: boolean;
  roles: string[];
}

export interface CreateUserRequest {
  fullName: string;
  email: string;
  phoneNumber: string | null;
  password: string;
  roles: string[];
}

export interface UpdateUserRequest {
  fullName: string;
  phoneNumber: string | null;
  isActive: boolean;
  roles: string[];
}

export interface RoleSummary {
  id: string;
  name: string;
  permissions: string[];
}

export interface PermissionSummary {
  id: string;
  name: string;
  displayName: string;
  group: string;
}
