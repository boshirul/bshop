export interface ApiResponse<T> {
  succeeded: boolean;
  data: T | null;
  message: string | null;
  errors: string[];
}

export interface CurrentUser {
  id: string;
  email: string;
  fullName: string;
  roles: string[];
  permissions: string[];
}

export interface AuthResponse {
  accessToken: string;
  accessTokenExpiresOn: string;
  user: CurrentUser;
}

export interface LoginRequest {
  email: string;
  password: string;
}
