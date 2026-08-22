export interface ApiKey {
  id: number;
  label: string;
  keyPrefix: string;
  scopes: string[];
  lastUsedAt: string | null;
  revokedAt: string | null;
  createdAt: string;
}

export interface CreatedApiKey {
  id: number;
  label: string;
  rawKey: string;
  scopes: string[];
  createdAt: string;
}

export interface CreateApiKeyRequest {
  label: string;
  scopes: string[];
}

export interface WebhookSubscription {
  id: number;
  url: string;
  events: string[];
  isActive: boolean;
  createdAt: string;
}

export interface CreatedWebhookSubscription {
  id: number;
  url: string;
  events: string[];
  secret: string;
  createdAt: string;
}

export interface CreateWebhookSubscriptionRequest {
  url: string;
  events: string[];
}
