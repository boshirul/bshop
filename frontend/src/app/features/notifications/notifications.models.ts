export type NotificationKind =
  | 'LowStock'
  | 'DailySalesSummary'
  | 'CustomerDueReminder'
  | 'WarrantyExpiryReminder'
  | 'OnlineOrder'
  | 'JobFailure';

export type NotificationStatus = 'Draft' | 'Copied' | 'SentExternally' | 'Dismissed';
export type NotificationChannel = 'Sms' | 'WhatsApp' | 'Internal';
export type JobRunStatus = 'Running' | 'Completed' | 'Failed';

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

export interface NotificationMessageItem {
  id: string;
  kind: NotificationKind;
  channel: NotificationChannel;
  status: NotificationStatus;
  title: string;
  message: string;
  recipientName: string | null;
  recipientPhone: string | null;
  generatedOn: string;
}

export interface BackgroundJobRunItem {
  id: string;
  jobName: string;
  runKey: string;
  status: JobRunStatus;
  startedOn: string;
  completedOn: string | null;
  createdCount: number;
  error: string | null;
}

export interface NotificationGenerationResult {
  jobName: string;
  createdCount: number;
}
