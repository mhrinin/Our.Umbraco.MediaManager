import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { umbHttpClient } from "@umbraco-cms/backoffice/http-client";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type {
  CleanupResult,
  ReclaimableSpaceResponse,
  ScanJobStatus,
  ScanResultItems,
  ScanResultSummary,
  ScanType,
  StartScanResponse,
} from "../types.d.js";

const BEARER = [{ scheme: "bearer", type: "http" }] as const;

export const API_BASE_URL = "/umbraco/media-manager/api/v1";

/**
 * Builds the capability URL for a finished export. The token IS the credential (the download
 * endpoint is anonymous but validates jobId + token), which is what lets a plain anchor download
 * a multi-GB zip with native browser resume — bearer headers cannot ride an <a href>.
 */
export function exportDownloadUrl(jobId: string, token: string): string {
  return `${API_BASE_URL}/export/${jobId}/file?token=${encodeURIComponent(token)}`;
}

/**
 * Thin HTTP layer over the Media Manager API. Never raises notifications itself — the context is
 * the single place user-facing messages come from — and every call is abortable so polling stops
 * the moment the dashboard is destroyed or a scan is restarted.
 */
export class MediaManagerRepository {
  private readonly apiBaseUrl = API_BASE_URL;

  constructor(private host: UmbControllerHost) {}

  async startScan(type: ScanType, signal?: AbortSignal): Promise<string> {
    const { data, error } = await tryExecute(
      this.host,
      umbHttpClient.post<StartScanResponse>({
        url: `${this.apiBaseUrl}/scan?type=${type}`,
        security: [...BEARER],
      }),
      { disableNotifications: true, abortSignal: signal },
    );
    if (error) throw error;
    if (!data) throw new Error("Failed to start scan");
    return data.jobId;
  }

  async getStatus(jobId: string, signal?: AbortSignal): Promise<ScanJobStatus | null> {
    const { data } = await tryExecute(
      this.host,
      umbHttpClient.get<ScanJobStatus>({
        url: `${this.apiBaseUrl}/scan/${jobId}/status`,
        security: [...BEARER],
      }),
      { disableNotifications: true, abortSignal: signal },
    );
    return data ?? null;
  }

  async getResult(jobId: string, signal?: AbortSignal): Promise<ScanResultSummary | null> {
    const { data } = await tryExecute(
      this.host,
      umbHttpClient.get<ScanResultSummary>({
        url: `${this.apiBaseUrl}/scan/${jobId}/result`,
        security: [...BEARER],
      }),
      { disableNotifications: true, abortSignal: signal },
    );
    return data ?? null;
  }

  async getLatestResult(type: ScanType, signal?: AbortSignal): Promise<ScanResultSummary | null> {
    const { data } = await tryExecute(
      this.host,
      umbHttpClient.get<ScanResultSummary>({
        url: `${this.apiBaseUrl}/scan/latest?type=${type}`,
        security: [...BEARER],
      }),
      { disableNotifications: true, abortSignal: signal },
    );
    return data ?? null;
  }

  async getResultItems(
    jobId: string,
    skip: number,
    take: number,
    signal?: AbortSignal,
  ): Promise<ScanResultItems | null> {
    const { data } = await tryExecute(
      this.host,
      umbHttpClient.get<ScanResultItems>({
        url: `${this.apiBaseUrl}/scan/${jobId}/result/items?skip=${skip}&take=${take}`,
        security: [...BEARER],
      }),
      { disableNotifications: true, abortSignal: signal },
    );
    return data ?? null;
  }

  async getReclaimableBytes(signal?: AbortSignal): Promise<number | null> {
    const { data } = await tryExecute(
      this.host,
      umbHttpClient.get<ReclaimableSpaceResponse>({
        url: `${this.apiBaseUrl}/scan/reclaimable`,
        security: [...BEARER],
      }),
      { disableNotifications: true, abortSignal: signal },
    );
    return data?.reclaimableBytes ?? null;
  }

  async deleteItems(jobId: string, ids: string[], dryRun: boolean): Promise<CleanupResult | null> {
    const { data, error } = await tryExecute(
      this.host,
      umbHttpClient.post<CleanupResult>({
        url: `${this.apiBaseUrl}/cleanup/scan/${jobId}`,
        body: { ids, dryRun },
        security: [...BEARER],
      }),
      { disableNotifications: true },
    );
    if (error) throw error;
    return data ?? null;
  }

  async deleteAll(jobId: string, dryRun: boolean): Promise<CleanupResult | null> {
    const { data, error } = await tryExecute(
      this.host,
      umbHttpClient.post<CleanupResult>({
        url: `${this.apiBaseUrl}/cleanup/scan/${jobId}/all`,
        body: { dryRun },
        security: [...BEARER],
      }),
      { disableNotifications: true },
    );
    if (error) throw error;
    return data ?? null;
  }
}
