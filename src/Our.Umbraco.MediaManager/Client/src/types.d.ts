export type ScanType =
  | "UnusedMedia"
  | "OrphanedFiles"
  | "BrokenMedia"
  | "Duplicates"
  | "StorageReport"
  | "Export";

export type MediaManagerTab = ScanType;

export type ScanJobState =
  | "Queued"
  | "Running"
  | "Completed"
  | "Cancelled"
  | "Failed";

export interface StartScanResponse {
  jobId: string;
}

export interface ScanJobStatus {
  id: string;
  type: ScanType;
  state: ScanJobState;
  processed: number;
  foundCount: number;
  error: string | null;
}

export interface ScanItem {
  id: string;
  name: string;
  path: string | null;
  sizeBytes: number;
}

export interface ScanResultSummary {
  jobId: string;
  type: ScanType;
  totalItems: number;
  reclaimableBytes: number;
  report: StorageReport | null;
  export: ExportInfo | null;
}

export interface ScanResultItems {
  total: number;
  items: ScanItem[];
}

export interface ReclaimableSpaceResponse {
  reclaimableBytes: number;
}

export interface ExportInfo {
  fileCount: number;
  zipSizeBytes: number;
  createdUtc: string;
  downloadToken: string;
  errors: string[];
  skippedCount: number;
}

export interface CleanupResult {
  affected: number;
  errors: string[];
}

export interface StorageTypeBreakdown {
  typeAlias: string;
  icon: string;
  count: number;
  bytes: number;
}

export interface StorageReport {
  totalBytes: number;
  totalCount: number;
  byType: StorageTypeBreakdown[];
  largest: ScanItem[];
}
