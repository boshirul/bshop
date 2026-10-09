export type DataExchangeKind = 'Products' | 'Customers' | 'Suppliers' | 'OpeningStock';

export interface ApiResponse<T> {
  succeeded: boolean;
  data: T | null;
  message: string | null;
  errors: string[];
}

export interface ImportPreviewRow {
  rowNumber: number;
  isValid: boolean;
  errors: string[];
  values: Record<string, string>;
}

export interface ImportPreviewResult {
  kind: DataExchangeKind;
  totalRows: number;
  validRows: number;
  invalidRows: number;
  rows: ImportPreviewRow[];
}

export interface CsvFileItem {
  fileName: string;
  contentType: string;
  content: string;
}

export interface ImportCommitResult {
  batchId: string;
  batchNumber: string;
  kind: DataExchangeKind;
  totalRows: number;
  importedRows: number;
  invalidRows: number;
  errorFile: CsvFileItem | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ExportLogItem {
  id: string;
  kind: DataExchangeKind;
  fileName: string;
  rowCount: number;
  exportedOn: string;
  exportedBy: string | null;
}
