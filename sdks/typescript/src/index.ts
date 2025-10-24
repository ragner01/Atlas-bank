import fetch from "cross-fetch";
import { v4 as uuidv4 } from "uuid";

/**
 * Configuration for AtlasBank SDK
 */
export interface AtlasConfig {
  /** Base URL for the API (e.g., http://localhost:5191 for payments) */
  baseUrl: string;
  /** Tenant ID for multi-tenancy support */
  tenantId?: string;
  /** Base URL for limits service */
  limitsBase?: string;
  /** Base URL for trust badge service */
  trustBadgeBase?: string;
  /** Base URL for offline queue service */
  offlineBase?: string;
  /** API key for authentication */
  apiKey?: string;
  /** HMAC signing function for secure requests */
  sign?: (path: string, body?: any) => Promise<string> | string;
  /** Request timeout in milliseconds */
  timeout?: number;
  /** Retry configuration */
  retry?: {
    attempts: number;
    delay: number;
    backoff: number;
  };
}

/**
 * Standard API response format
 */
export interface ApiResponse<T = any> {
  data?: T;
  error?: {
    code: string;
    message: string;
    details?: any;
  };
  meta?: {
    requestId: string;
    timestamp: string;
    version: string;
  };
}

/**
 * Transfer request parameters
 */
export interface TransferRequest {
  SourceAccountId: string;
  DestinationAccountId: string;
  Minor: number;
  Currency: string;
  Narration?: string;
}

/**
 * Card charge request parameters
 */
export interface CardChargeRequest {
  amountMinor: number;
  currency: string;
  cardToken: string;
  merchantId: string;
  mcc: string;
  deviceId?: string;
  ip?: string;
}

/**
 * Offline operation parameters
 */
export interface OfflineOperation {
  tenantId: string;
  deviceId: string;
  kind: "transfer" | "card_charge" | "balance_check";
  payload: any;
  nonce: string;
  signature: string;
}

/**
 * Error class for API errors
 */
export class AtlasApiError extends Error {
  constructor(
    public status: number,
    public code: string,
    message: string,
    public details?: any
  ) {
    super(message);
    this.name = "AtlasApiError";
  }
}

/**
 * Main AtlasBank SDK client
 */
export class AtlasClient {
  private readonly config: Required<AtlasConfig>;

  constructor(config: AtlasConfig) {
    this.config = {
      tenantId: "tnt_demo",
      limitsBase: "",
      trustBadgeBase: "",
      offlineBase: "",
      apiKey: "",
      timeout: 30000,
      retry: {
        attempts: 3,
        delay: 1000,
        backoff: 2,
      },
      ...config,
    };

    if (!this.config.baseUrl) {
      throw new Error("baseUrl is required");
    }
  }

  /**
   * Make a request to the API with retry logic and error handling
   */
  private async request<T = any>(
    path: string,
    init: RequestInit = {}
  ): Promise<T> {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), this.config.timeout);

    try {
      const headers: Record<string, string> = {
        "Content-Type": "application/json",
        "X-Tenant-Id": this.config.tenantId,
        "X-Request-ID": uuidv4(),
        "User-Agent": "@atlasbank/sdk/0.1.0",
        ...(this.config.apiKey ? { "X-API-Key": this.config.apiKey } : {}),
      };

      if (this.config.sign) {
        headers["X-Signature"] = await this.config.sign(path, init.body);
      }

      const response = await fetch(`${this.config.baseUrl}${path}`, {
        ...init,
        headers: { ...headers, ...(init.headers as Record<string, string>) },
        signal: controller.signal,
      });

      clearTimeout(timeoutId);

      if (!response.ok) {
        const errorText = await response.text();
        let errorData: any;
        
        try {
          errorData = JSON.parse(errorText);
        } catch {
          errorData = { message: errorText };
        }

        throw new AtlasApiError(
          response.status,
          errorData.code || "UNKNOWN_ERROR",
          errorData.message || `HTTP ${response.status}`,
          errorData.details
        );
      }

      const contentType = response.headers.get("content-type") || "";
      if (contentType.includes("application/json")) {
        return await response.json();
      }
      return (await response.text()) as T;
    } catch (error) {
      clearTimeout(timeoutId);
      
      if (error instanceof AtlasApiError) {
        throw error;
      }
      
      if (error instanceof Error) {
        if (error.name === "AbortError") {
          throw new AtlasApiError(408, "TIMEOUT", "Request timeout");
        }
        throw new AtlasApiError(0, "NETWORK_ERROR", error.message);
      }
      
      throw new AtlasApiError(0, "UNKNOWN_ERROR", "An unknown error occurred");
    }
  }

  /**
   * Make a request with retry logic
   */
  private async requestWithRetry<T = any>(
    path: string,
    init: RequestInit = {}
  ): Promise<T> {
    let lastError: Error;
    
    for (let attempt = 1; attempt <= this.config.retry.attempts; attempt++) {
      try {
        return await this.request<T>(path, init);
      } catch (error) {
        lastError = error as Error;
        
        // Don't retry on client errors (4xx) or certain server errors
        if (error instanceof AtlasApiError) {
          if (error.status >= 400 && error.status < 500) {
            throw error; // Don't retry client errors
          }
          if (error.status === 408 || error.status === 429) {
            throw error; // Don't retry timeout or rate limit errors
          }
        }
        
        if (attempt === this.config.retry.attempts) {
          throw lastError;
        }
        
        // Wait before retry with exponential backoff
        const delay = this.config.retry.delay * Math.pow(this.config.retry.backoff, attempt - 1);
        await new Promise(resolve => setTimeout(resolve, delay));
      }
    }
    
    throw lastError!;
  }

  /**
   * Charge a card with limits enforcement
   */
  async chargeCardEnforced(args: CardChargeRequest): Promise<ApiResponse> {
    const idempotencyKey = uuidv4();
    const params = new URLSearchParams({
      amountMinor: args.amountMinor.toString(),
      currency: args.currency,
      cardToken: args.cardToken,
      merchantId: args.merchantId,
      mcc: args.mcc,
    });

    const headers: Record<string, string> = {
      "Idempotency-Key": idempotencyKey,
    };

    if (args.deviceId) {
      headers["X-Device-Id"] = args.deviceId;
    }
    if (args.ip) {
      headers["X-IP"] = args.ip;
    }

    return this.requestWithRetry<ApiResponse>(
      `/payments/cnp/charge/enforced?${params.toString()}`,
      {
        method: "POST",
        headers,
      }
    );
  }

  /**
   * Transfer money with risk assessment
   */
  async transferWithRisk(body: TransferRequest): Promise<ApiResponse> {
    const idempotencyKey = uuidv4();
    
    return this.requestWithRetry<ApiResponse>(`/payments/transfers/with-risk`, {
      method: "POST",
      headers: {
        "Idempotency-Key": idempotencyKey,
      },
      body: JSON.stringify(body),
    });
  }

  /**
   * Get account balance
   */
  async getBalance(accountId: string, currency?: string): Promise<ApiResponse> {
    const params = currency ? `?currency=${encodeURIComponent(currency)}` : "";
    return this.requestWithRetry<ApiResponse>(`/ledger/accounts/${encodeURIComponent(accountId)}/balance/global${params}`);
  }

  /**
   * Get trust badge URL
   */
  trustBadgeUrl(entityId: string): string {
    const base = this.config.trustBadgeBase;
    if (!base) {
      throw new Error("trustBadgeBase not configured");
    }
    return `${base}/badge/${encodeURIComponent(entityId)}.svg`;
  }

  /**
   * Enqueue offline operation
   */
  async offlineEnqueue(op: OfflineOperation): Promise<ApiResponse> {
    const base = this.config.offlineBase;
    if (!base) {
      throw new Error("offlineBase not configured");
    }
    
    return this.requestAbs(base, `/offline/ops`, {
      method: "POST",
      body: JSON.stringify(op),
    });
  }

  /**
   * Sync offline operations
   */
  async offlineSync(deviceId: string, max = 20): Promise<ApiResponse> {
    const base = this.config.offlineBase;
    if (!base) {
      throw new Error("offlineBase not configured");
    }
    
    const params = new URLSearchParams({
      deviceId: encodeURIComponent(deviceId),
      max: max.toString(),
    });
    
    return this.requestAbs(base, `/offline/sync?${params.toString()}`, {
      method: "POST",
    });
  }

  /**
   * Make request to different base URL
   */
  private async requestAbs<T = any>(
    base: string,
    path: string,
    init: RequestInit = {}
  ): Promise<T> {
    const headers: Record<string, string> = {
      "Content-Type": "application/json",
      "X-Tenant-Id": this.config.tenantId,
      "X-Request-ID": uuidv4(),
      "User-Agent": "@atlasbank/sdk/0.1.0",
    };

    const response = await fetch(`${base}${path}`, {
      ...init,
      headers: { ...headers, ...(init.headers as Record<string, string>) },
    });

    if (!response.ok) {
      const errorText = await response.text();
      throw new AtlasApiError(
        response.status,
        "HTTP_ERROR",
        errorText || `HTTP ${response.status}`
      );
    }

    const contentType = response.headers.get("content-type") || "";
    if (contentType.includes("application/json")) {
      return await response.json();
    }
    return (await response.text()) as T;
  }
}

/**
 * Generate HMAC signature for offline operations
 */
export async function hmacHex(
  secret: string,
  deviceId: string,
  payload: any,
  nonce: string,
  tenantId: string
): Promise<string> {
  try {
    const message = JSON.stringify(payload) + nonce + tenantId;
    const keyData = `${secret}:${deviceId}`;
    
    const encoder = new TextEncoder();
    const messageBuffer = encoder.encode(message);
    const keyBuffer = encoder.encode(keyData);
    
    // Use Web Crypto API for HMAC-SHA256
    const cryptoKey = await crypto.subtle.importKey(
      "raw",
      keyBuffer,
      { name: "HMAC", hash: "SHA-256" },
      false,
      ["sign"]
    );
    
    const signature = await crypto.subtle.sign("HMAC", cryptoKey, messageBuffer);
    const hashArray = Array.from(new Uint8Array(signature));
    return hashArray.map(b => b.toString(16).padStart(2, "0")).join("");
  } catch (error) {
    throw new Error(`HMAC generation failed: ${error instanceof Error ? error.message : "Unknown error"}`);
  }
}

/**
 * Validate MSISDN format
 */
export function validateMsisdn(msisdn: string): boolean {
  const cleaned = msisdn.replace(/[^\d+]/g, "");
  return /^\+?[1-9]\d{1,14}$/.test(cleaned) && cleaned.length >= 10 && cleaned.length <= 20;
}

/**
 * Validate account ID format
 */
export function validateAccountId(accountId: string): boolean {
  return /^[a-zA-Z0-9:_-]+$/.test(accountId) && accountId.length <= 100;
}

/**
 * Validate amount (must be positive integer)
 */
export function validateAmount(amount: number): boolean {
  return Number.isInteger(amount) && amount > 0;
}

/**
 * Default configuration for development
 */
export const defaultConfig: Partial<AtlasConfig> = {
  tenantId: "tnt_demo",
  timeout: 30000,
  retry: {
    attempts: 3,
    delay: 1000,
    backoff: 2,
  },
};
