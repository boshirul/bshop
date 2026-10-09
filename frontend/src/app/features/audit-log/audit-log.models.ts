export interface ApiResponse<T> {
  succeeded: boolean;
  data: T | null;
  message: string | null;
  errors: string[];
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface AuditLogItem {
  id: string;
  occurredOn: string;
  performedBy: string | null;
  action: string;
  entityType: string;
  entityId: string | null;
  description: string | null;
}
