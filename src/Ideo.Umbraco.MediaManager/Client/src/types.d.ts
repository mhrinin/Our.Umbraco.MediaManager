export type ScanType = "OrphanedMedia" | "OrphanedFiles" | "BrokenMedia";

export type ScanState =
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
  state: ScanState;
  processed: number;
  total: number;
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
}

export interface CleanupResult {
  affected: number;
  errors: string[];
}
