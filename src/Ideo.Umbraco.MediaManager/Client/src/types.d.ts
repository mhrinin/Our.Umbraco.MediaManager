export type ScanType =
  | "UnusedMedia"
  | "OrphanedFiles"
  | "BrokenMedia"
  | "Duplicates"
  | "StorageReport";

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

export interface MediaCandidate {
  key: string;
  name: string;
  path: string | null;
  sizeBytes: number;
}

export interface FileCandidate {
  path: string;
  sizeBytes: number;
}

export interface ScanResult {
  jobId: string;
  type: ScanType;
  media: MediaCandidate[];
  files: FileCandidate[];
  reclaimableBytes: number;
  report: StorageReport | null;
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
  largest: MediaCandidate[];
}
