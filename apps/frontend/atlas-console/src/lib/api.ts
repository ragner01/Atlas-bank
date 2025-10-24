import axios, { AxiosInstance, AxiosResponse } from 'axios';
import Cookies from 'js-cookie';

// API Configuration - Matching Phase 24 mobile app
const API_CONFIG = {
  baseUrl: import.meta.env.VITE_PAYMENTS_API_URL || 'http://localhost:5191',
  ledger: import.meta.env.VITE_LEDGER_API_URL || 'http://localhost:5190',
  trust: import.meta.env.VITE_TRUST_API_URL || 'http://localhost:5802',
  cards: import.meta.env.VITE_CARDS_API_URL || 'http://localhost:5192',
  loans: import.meta.env.VITE_LOANS_API_URL || 'http://localhost:5193',
  agent: import.meta.env.VITE_AGENT_API_URL || 'http://localhost:5621',
  offline: import.meta.env.VITE_OFFLINE_API_URL || 'http://localhost:5622',
  tenantId: 'tnt_demo',
  timeout: 30000,
  retry: {
    attempts: 3,
    delay: 1000,
    backoff: 2,
  },
};

const TOKEN_KEY = import.meta.env.VITE_TOKEN_STORAGE_KEY || 'atlasbank_token';
const DEVICE_ID_KEY = 'atlasbank_device_id';

// Enhanced Axios client factory with comprehensive error handling
const createApiClient = (baseURL: string): AxiosInstance => {
  const client = axios.create({
    baseURL,
    timeout: 30000,
    headers: {
      'Content-Type': 'application/json',
    },
  });

  // Request interceptor for authentication and logging
  client.interceptors.request.use(
    (config) => {
      const token = Cookies.get(TOKEN_KEY);
      if (token) {
        config.headers.Authorization = `Bearer ${token}`;
      }
      
      // Add request ID for tracing
      config.headers['X-Request-ID'] = crypto.randomUUID();
      
      // Log request in development
      if (process.env.NODE_ENV === 'development') {
        console.log(`🚀 API Request: ${config.method?.toUpperCase()} ${config.url}`, {
          headers: config.headers,
          data: config.data
        });
      }
      
      return config;
    },
    (error) => {
      console.error('Request interceptor error:', error);
      return Promise.reject(AtlasApiError.fromAxiosError(error));
    }
  );

  // Response interceptor for error handling and logging
  client.interceptors.response.use(
    (response) => {
      // Log response in development
      if (process.env.NODE_ENV === 'development') {
        console.log(`✅ API Response: ${response.config.method?.toUpperCase()} ${response.config.url}`, {
          status: response.status,
          data: response.data
        });
      }
      
      return response;
    },
    (error) => {
      const apiError = AtlasApiError.fromAxiosError(error);
      
      // Handle specific error types
      if (apiError.isAuthError()) {
        // Clear invalid token and redirect to login
        Cookies.remove(TOKEN_KEY);
        if (typeof window !== 'undefined') {
          window.location.href = '/login';
        }
      }
      
      // Log error
      console.error('API Error:', {
        message: apiError.message,
        code: apiError.code,
        statusCode: apiError.statusCode,
        url: error.config?.url,
        method: error.config?.method
      });
      
      return Promise.reject(apiError);
    }
  );

  return client;
};

// API Clients
export const paymentsApi = createApiClient(API_CONFIG.baseUrl);
export const ledgerApi = createApiClient(API_CONFIG.ledger);
export const trustApi = createApiClient(API_CONFIG.trust);
export const cardsApi = createApiClient(API_CONFIG.cards);
export const loansApi = createApiClient(API_CONFIG.loans);
export const agentApi = createApiClient(API_CONFIG.agent);
export const offlineApi = createApiClient(API_CONFIG.offline);

// Validation functions - Matching Phase 24 mobile app
export const validateMsisdn = (msisdn: string): boolean => {
  // Remove any non-digit characters and check length
  const cleaned = msisdn.replace(/\D/g, '');
  return cleaned.length >= 10 && cleaned.length <= 15;
};

export const validateAccountId = (accountId: string): boolean => {
  // Support formats like: msisdn::2348100000001, account::1234567890::gtbank
  const patterns = [
    /^msisdn::\d{10,15}$/,
    /^account::\d{10}::[a-z]+$/,
    /^[a-zA-Z0-9:_-]+$/
  ];
  return patterns.some(pattern => pattern.test(accountId)) && accountId.length <= 100;
};

export const validateAmount = (amount: number): boolean => {
  return amount > 0 && amount <= 100000000; // Max 1M NGN in minor units
};

export const validatePin = (pin: string): boolean => {
  return pin.length >= 4 && pin.length <= 6 && /^\d+$/.test(pin);
};

// HMAC signing for offline operations - Matching Phase 24 mobile app
export const hmacHex = async (
  secret: string,
  deviceId: string,
  payload: any,
  nonce: string,
  tenantId: string
): Promise<string> => {
  const encoder = new TextEncoder();
  const key = await crypto.subtle.importKey(
    'raw',
    encoder.encode(secret),
    { name: 'HMAC', hash: 'SHA-256' },
    false,
    ['sign']
  );
  
  const message = `${deviceId}:${JSON.stringify(payload)}:${nonce}:${tenantId}`;
  const signature = await crypto.subtle.sign('HMAC', key, encoder.encode(message));
  const hashArray = Array.from(new Uint8Array(signature));
  return hashArray.map(b => b.toString(16).padStart(2, '0')).join('');
};

// Device ID generation - Matching Phase 24 mobile app
export const generateDeviceId = (): string => {
  const existingId = localStorage.getItem(DEVICE_ID_KEY);
  if (existingId) return existingId;
  
  const deviceId = `device_${Date.now()}_${Math.random().toString(36).substr(2, 9)}`;
  localStorage.setItem(DEVICE_ID_KEY, deviceId);
  return deviceId;
};

// Network status detection
export const isOnline = (): boolean => {
  return navigator.onLine;
};

// Custom error class - Matching Phase 24 mobile app
export class AtlasApiError extends Error {
  constructor(
    message: string,
    public code: string,
    public statusCode?: number,
    public details?: any
  ) {
    super(message);
    this.name = 'AtlasApiError';
  }

  static fromAxiosError(error: any): AtlasApiError {
    if (error.response) {
      // Server responded with error status
      const { status, data } = error.response;
      return new AtlasApiError(
        data?.message || data?.error || `Server error (${status})`,
        data?.code || 'SERVER_ERROR',
        status,
        data
      );
    } else if (error.request) {
      // Network error
      return new AtlasApiError(
        'Network error - please check your connection',
        'NETWORK_ERROR',
        0,
        { originalError: error.message }
      );
    } else {
      // Other error
      return new AtlasApiError(
        error.message || 'An unexpected error occurred',
        'UNKNOWN_ERROR',
        0,
        { originalError: error }
      );
    }
  }

  isNetworkError(): boolean {
    return this.code === 'NETWORK_ERROR';
  }

  isServerError(): boolean {
    return this.statusCode ? this.statusCode >= 500 : false;
  }

  isClientError(): boolean {
    return this.statusCode ? this.statusCode >= 400 && this.statusCode < 500 : false;
  }

  isAuthError(): boolean {
    return this.statusCode === 401 || this.code === 'UNAUTHORIZED';
  }

  isRateLimitError(): boolean {
    return this.statusCode === 429 || this.code === 'RATE_LIMITED';
  }
}

// Types
export interface User {
  id: string;
  msisdn: string;
  firstName: string;
  lastName: string;
  email?: string;
  isVerified: boolean;
}

export interface Account {
  id: string;
  userId: string;
  currency: string;
  balanceMinor: number;
  status: 'active' | 'suspended' | 'closed';
  createdAt: string;
}

export interface Transaction {
  id: string;
  accountId: string;
  amountMinor: number;
  currency: string;
  type: 'credit' | 'debit';
  status: 'pending' | 'completed' | 'failed';
  narration: string;
  createdAt: string;
  reference: string;
}

export interface TransferRequest {
  sourceAccountId: string;
  destinationAccountId: string;
  amountMinor: number;
  currency: string;
  narration?: string;
}

export interface Card {
  id: string;
  userId: string;
  last4: string;
  network: string;
  status: 'active' | 'blocked' | 'expired';
  expiryMonth: number;
  expiryYear: number;
  createdAt: string;
}

export interface Loan {
  id: string;
  userId: string;
  productId: string;
  amountMinor: number;
  currency: string;
  status: 'active' | 'completed' | 'defaulted';
  interestRate: number;
  termMonths: number;
  monthlyPaymentMinor: number;
  remainingBalanceMinor: number;
  createdAt: string;
}

// Authentication Service
export class AuthService {
  static async login(msisdn: string, pin: string): Promise<{ token: string; user: User }> {
    // For demo purposes, we'll use mock authentication
    // In production, this would call the actual auth service
    if (msisdn === '2348100000001' && pin === '1234') {
      const token = btoa(`demo_token_${msisdn}_${Date.now()}`);
      
      // Store token in cookie
      Cookies.set(TOKEN_KEY, token, { expires: 7, secure: true, sameSite: 'strict' });
      
      const user: User = {
        msisdn: msisdn,
        firstName: 'John',
        lastName: 'Doe',
        email: 'john.doe@example.com'
      };
      
      return { token, user };
    } else {
      throw new AtlasApiError('Invalid credentials', 'INVALID_CREDENTIALS');
    }
  }

  static async logout(): Promise<void> {
    Cookies.remove(TOKEN_KEY);
  }

  static isAuthenticated(): boolean {
    const token = Cookies.get(TOKEN_KEY);
    return !!token;
  }

  static getToken(): string | undefined {
    return Cookies.get(TOKEN_KEY);
  }

  static isAuthenticated(): boolean {
    return !!Cookies.get(TOKEN_KEY);
  }
}

// Ledger Service
export class LedgerService {
  static async getAccountBalance(accountId: string, currency: string = 'NGN'): Promise<number> {
    const response = await ledgerApi.get(`/ledger/accounts/${accountId}/balance/global?currency=${currency}`);
    return response.data.balanceMinor;
  }

  static async getAccountTransactions(
    accountId: string,
    limit: number = 20,
    offset: number = 0
  ): Promise<Transaction[]> {
    const response = await ledgerApi.get(
      `/ledger/accounts/${accountId}/transactions?limit=${limit}&offset=${offset}`
    );
    return response.data.transactions;
  }

  static async getAccountDetails(accountId: string): Promise<Account> {
    const response = await ledgerApi.get(`/ledger/accounts/${accountId}`);
    return response.data;
  }
}

// Payments Service - Enhanced with Phase 24 patterns
export class PaymentsService {
  static async transferWithRisk(transferRequest: TransferRequest): Promise<{ reference: string; status: string }> {
    try {
      const response = await paymentsApi.post('/payments/transfers/with-risk', transferRequest);
      return response.data;
    } catch (error: any) {
      throw new AtlasApiError(
        error.response?.data?.message || 'Transfer failed',
        error.response?.data?.code || 'TRANSFER_FAILED',
        error.response?.status,
        error.response?.data
      );
    }
  }

  static async transferMoney(transferRequest: TransferRequest): Promise<{ reference: string; status: string }> {
    return this.transferWithRisk(transferRequest);
  }

  static async getTransferHistory(accountId: string, limit: number = 20): Promise<Transaction[]> {
    try {
      const response = await paymentsApi.get(`/payments/transfers/history?accountId=${accountId}&limit=${limit}`);
      return response.data.transfers;
    } catch (error: any) {
      throw new AtlasApiError(
        error.response?.data?.message || 'Failed to fetch transfer history',
        error.response?.data?.code || 'HISTORY_FAILED',
        error.response?.status
      );
    }
  }

  static async validateAccount(accountNumber: string, bankCode: string): Promise<{ name: string; bank: string }> {
    try {
      const response = await paymentsApi.post('/payments/validate-account', {
        accountNumber,
        bankCode,
      });
      return response.data;
    } catch (error: any) {
      throw new AtlasApiError(
        error.response?.data?.message || 'Account validation failed',
        error.response?.data?.code || 'VALIDATION_FAILED',
        error.response?.status
      );
    }
  }
}

// Cards Service
export class CardsService {
  static async getCards(): Promise<Card[]> {
    const response = await cardsApi.get('/cards');
    return response.data.cards;
  }

  static async createCard(): Promise<Card> {
    const response = await cardsApi.post('/cards');
    return response.data;
  }

  static async blockCard(cardId: string): Promise<void> {
    await cardsApi.post(`/cards/${cardId}/block`);
  }

  static async unblockCard(cardId: string): Promise<void> {
    await cardsApi.post(`/cards/${cardId}/unblock`);
  }
}

// Loans Service
export class LoansService {
  static async getLoans(): Promise<Loan[]> {
    const response = await loansApi.get('/loans');
    return response.data.loans;
  }

  static async getLoanProducts(): Promise<any[]> {
    const response = await loansApi.get('/loans/products');
    return response.data.products;
  }

  static async applyForLoan(productId: string, amountMinor: number): Promise<Loan> {
    const response = await loansApi.post('/loans/apply', {
      productId,
      amountMinor,
    });
    return response.data;
  }

  static async getLoanSchedule(loanId: string): Promise<any[]> {
    const response = await loansApi.get(`/loans/${loanId}/schedule`);
    return response.data.schedule;
  }
}

// Trust Service - Enhanced with Phase 24 patterns
export class TrustService {
  static async getTrustScore(entityId: string): Promise<{ score: number; level: string }> {
    try {
      const response = await trustApi.get(`/trust/score/${entityId}`);
      return response.data;
    } catch (error: any) {
      throw new AtlasApiError(
        error.response?.data?.message || 'Failed to fetch trust score',
        error.response?.data?.code || 'TRUST_FAILED',
        error.response?.status
      );
    }
  }

  static async getTrustBadge(entityId: string): Promise<string> {
    try {
      const response = await trustApi.get(`/trust/badge/${entityId}`);
      return response.data.badgeUrl;
    } catch (error: any) {
      throw new AtlasApiError(
        error.response?.data?.message || 'Failed to fetch trust badge',
        error.response?.data?.code || 'BADGE_FAILED',
        error.response?.status
      );
    }
  }

  // Generate trust badge URL - Matching Phase 24 mobile app
  static trustBadgeUrl(entityId: string): string {
    return `${API_CONFIG.trust}/trust/badge/${entityId}`;
  }
}

// Agent Service - Enhanced with Phase 24 patterns
export class AgentService {
  static async cashIn(agentCode: string, amountMinor: number, msisdn: string): Promise<{ reference: string }> {
    try {
      const response = await agentApi.post('/agent/cashin/intent', {
        agentCode,
        amountMinor,
        msisdn,
        currency: 'NGN',
      });
      return response.data;
    } catch (error: any) {
      throw new AtlasApiError(
        error.response?.data?.message || 'Failed to create cash-in intent',
        error.response?.data?.code || 'CASHIN_FAILED',
        error.response?.status
      );
    }
  }

  static async cashOut(agentCode: string, amountMinor: number, msisdn: string): Promise<{ reference: string }> {
    try {
      const response = await agentApi.post('/agent/withdraw/intent', {
        agentCode,
        amountMinor,
        msisdn,
        currency: 'NGN',
      });
      return response.data;
    } catch (error: any) {
      throw new AtlasApiError(
        error.response?.data?.message || 'Failed to create cash-out intent',
        error.response?.data?.code || 'CASHOUT_FAILED',
        error.response?.status
      );
    }
  }

  // Alternative method using URL parameters (matching mobile app)
  static async cashInIntent(msisdn: string, agent: string, minor: number, currency: string = 'NGN'): Promise<{ reference: string }> {
    try {
      const response = await agentApi.post(
        `/agent/cashin/intent?msisdn=${encodeURIComponent(msisdn)}&agent=${encodeURIComponent(agent)}&minor=${encodeURIComponent(minor)}&currency=${encodeURIComponent(currency)}`,
        {}
      );
      return response.data;
    } catch (error: any) {
      throw new AtlasApiError(
        error.response?.data?.message || 'Failed to create cash-in intent',
        error.response?.data?.code || 'CASHIN_FAILED',
        error.response?.status
      );
    }
  }

  static async cashOutIntent(msisdn: string, agent: string, minor: number, currency: string = 'NGN'): Promise<{ reference: string }> {
    try {
      const response = await agentApi.post(
        `/agent/withdraw/intent?msisdn=${encodeURIComponent(msisdn)}&agent=${encodeURIComponent(agent)}&minor=${encodeURIComponent(minor)}&currency=${encodeURIComponent(currency)}`,
        {}
      );
      return response.data;
    } catch (error: any) {
      throw new AtlasApiError(
        error.response?.data?.message || 'Failed to create cash-out intent',
        error.response?.data?.code || 'CASHOUT_FAILED',
        error.response?.status
      );
    }
  }
}

// Offline Service - Enhanced with Phase 24 patterns
export class OfflineService {
  static async offlineEnqueue(request: {
    tenantId: string;
    deviceId: string;
    kind: string;
    payload: any;
    nonce: string;
    signature: string;
  }): Promise<{ id: string }> {
    try {
      const response = await offlineApi.post('/offline/ops', request);
      return response.data;
    } catch (error: any) {
      throw new AtlasApiError(
        error.response?.data?.message || 'Failed to queue offline operation',
        error.response?.data?.code || 'QUEUE_FAILED',
        error.response?.status
      );
    }
  }

  static async offlineSync(deviceId: string, limit: number = 50): Promise<{ synced: number; results: any[] }> {
    try {
      const response = await offlineApi.post('/offline/sync', { deviceId, limit });
      return response.data;
    } catch (error: any) {
      throw new AtlasApiError(
        error.response?.data?.message || 'Failed to sync offline operations',
        error.response?.data?.code || 'SYNC_FAILED',
        error.response?.status
      );
    }
  }

  static async getQueuedOperations(deviceId: string): Promise<any[]> {
    try {
      const response = await offlineApi.get(`/offline/ops?deviceId=${deviceId}`);
      return response.data.operations;
    } catch (error: any) {
      throw new AtlasApiError(
        error.response?.data?.message || 'Failed to fetch queued operations',
        error.response?.data?.code || 'FETCH_FAILED',
        error.response?.status
      );
    }
  }

  // Legacy methods for backward compatibility
  static async queueTransaction(transaction: any): Promise<{ id: string }> {
    const deviceId = generateDeviceId();
    const nonce = `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
    const signature = await hmacHex('dev-secret', deviceId, transaction, nonce, API_CONFIG.tenantId);
    
    return this.offlineEnqueue({
      tenantId: API_CONFIG.tenantId,
      deviceId,
      kind: 'transfer',
      payload: transaction,
      nonce,
      signature,
    });
  }

  static async syncTransactions(): Promise<{ synced: number }> {
    const deviceId = generateDeviceId();
    const result = await this.offlineSync(deviceId);
    return { synced: result.synced };
  }

  static async getQueuedTransactions(): Promise<any[]> {
    const deviceId = generateDeviceId();
    return this.getQueuedOperations(deviceId);
  }
}

// AtlasClient - Main client class matching Phase 24 SDK
export class AtlasClient {
  private config: typeof API_CONFIG;
  private deviceId: string;

  constructor(config: Partial<typeof API_CONFIG> = {}) {
    this.config = { ...API_CONFIG, ...config };
    this.deviceId = generateDeviceId();
  }

  // Transfer with risk assessment - Matching Phase 24 mobile app
  async transferWithRisk(request: {
    SourceAccountId: string;
    DestinationAccountId: string;
    Minor: number;
    Currency: string;
    Narration: string;
  }): Promise<{ reference: string; status: string }> {
    return PaymentsService.transferWithRisk(request);
  }

  // Offline operations - Matching Phase 24 mobile app
  async offlineEnqueue(request: {
    tenantId: string;
    deviceId: string;
    kind: string;
    payload: any;
    nonce: string;
    signature: string;
  }): Promise<{ id: string }> {
    return OfflineService.offlineEnqueue(request);
  }

  async offlineSync(deviceId: string, limit: number = 50): Promise<{ synced: number; results: any[] }> {
    return OfflineService.offlineSync(deviceId, limit);
  }

  // Trust badge URL generation - Matching Phase 24 mobile app
  trustBadgeUrl(entityId: string): string {
    return TrustService.trustBadgeUrl(entityId);
  }

  // Agent operations - Matching Phase 24 mobile app
  async cashInIntent(msisdn: string, agent: string, minor: number, currency: string = 'NGN'): Promise<{ reference: string }> {
    return AgentService.cashInIntent(msisdn, agent, minor, currency);
  }

  async cashOutIntent(msisdn: string, agent: string, minor: number, currency: string = 'NGN'): Promise<{ reference: string }> {
    return AgentService.cashOutIntent(msisdn, agent, minor, currency);
  }

  // Get device ID
  getDeviceId(): string {
    return this.deviceId;
  }

  // Get configuration
  getConfig(): typeof API_CONFIG {
    return this.config;
  }
}

// Utility functions
export const formatCurrency = (amountMinor: number, currency: string = 'NGN'): string => {
  const amount = amountMinor / 100;
  return new Intl.NumberFormat('en-NG', {
    style: 'currency',
    currency: currency,
  }).format(amount);
};

export const formatDate = (dateString: string): string => {
  return new Intl.DateTimeFormat('en-NG', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(dateString));
};

export const maskAccountNumber = (accountNumber: string): string => {
  if (accountNumber.length <= 4) return accountNumber;
  return '*'.repeat(accountNumber.length - 4) + accountNumber.slice(-4);
};

export const maskCardNumber = (cardNumber: string): string => {
  if (cardNumber.length <= 4) return cardNumber;
  return '*'.repeat(cardNumber.length - 4) + cardNumber.slice(-4);
};
