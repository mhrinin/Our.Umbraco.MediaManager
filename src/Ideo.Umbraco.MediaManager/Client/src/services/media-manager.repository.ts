import { tryExecute } from "@umbraco-cms/backoffice/resources";
import { umbHttpClient } from "@umbraco-cms/backoffice/http-client";
import type { UmbControllerHost } from "@umbraco-cms/backoffice/controller-api";
import type {
  CleanupResult,
  ScanJobStatus,
  ScanResult,
  ScanType,
  StartScanResponse,
} from "../types.d.js";

const BEARER = [{ scheme: "bearer", type: "http" }] as const;

/**
 * Thin HTTP layer over the Media Manager API. Never raises notifications itself — the context is
 * the single place user-facing messages come from — and every call is abortable so polling stops
 * the moment the dashboard is destroyed or a scan is restarted.
 */
export class MediaManagerRepository {
  private readonly apiBaseUrl = "/umbraco/media-manager/api/v1";

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

  async getResult(jobId: string, signal?: AbortSignal): Promise<ScanResult | null> {
    const { data } = await tryExecute(
      this.host,
      umbHttpClient.get<ScanResult>({
        url: `${this.apiBaseUrl}/scan/${jobId}/result`,
        security: [...BEARER],
      }),
      { disableNotifications: true, abortSignal: signal },
    );
    return data ?? null;
  }

  async deleteMedia(keys: string[], dryRun: boolean): Promise<CleanupResult | null> {
    const { data, error } = await tryExecute(
      this.host,
      umbHttpClient.post<CleanupResult>({
        url: `${this.apiBaseUrl}/cleanup/media`,
        body: { keys, dryRun },
        security: [...BEARER],
      }),
      { disableNotifications: true },
    );
    if (error) throw error;
    return data ?? null;
  }

  async deleteFiles(jobId: string, paths: string[], dryRun: boolean): Promise<CleanupResult | null> {
    const { data, error } = await tryExecute(
      this.host,
      umbHttpClient.post<CleanupResult>({
        url: `${this.apiBaseUrl}/cleanup/files`,
        body: { jobId, paths, dryRun },
        security: [...BEARER],
      }),
      { disableNotifications: true },
    );
    if (error) throw error;
    return data ?? null;
  }
}
