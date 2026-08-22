import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiKey, CreatedApiKey, CreatedWebhookSubscription, WebhookSubscription } from '../../../core/models/developer.model';
import { ApiKeysService, WebhooksService } from '../../../core/services/developer.service';

@Component({
  selector: 'app-admin-developer',
  imports: [FormsModule, DatePipe],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">API &amp; webhooks</h1>
      <p class="text-sm text-slate-500 mb-6">Give a third-party system read/write access to your store, or have it notified the moment an order or product changes.</p>

      <!-- ================= API Keys ================= -->
      <section class="mb-10">
        <div class="flex items-center justify-between mb-1">
          <h2 class="font-semibold text-slate-800">API keys</h2>
          <button type="button" (click)="startNewKey()" class="btn-primary">+ New key</button>
        </div>
        <p class="text-sm text-slate-500 mb-4">Used by external systems to call the WavCommerce public API. Send it as <code class="bg-slate-100 px-1 rounded">Authorization: Bearer &lt;key&gt;</code>.</p>

        @if (keyMessage()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ keyMessage() }}</div> }
        @if (keyError()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ keyError() }}</div> }

        @if (revealedKey(); as rk) {
          <div class="bg-amber-50 border border-amber-200 rounded-xl p-4 mb-5">
            <p class="text-sm font-medium text-amber-800 mb-2">Copy this key now — you won't be able to see it again.</p>
            <div class="flex items-center gap-2">
              <code class="flex-1 bg-white border border-amber-200 rounded px-2 py-1.5 text-sm break-all">{{ rk.rawKey }}</code>
              <button type="button" (click)="copy(rk.rawKey)" class="px-3 py-1.5 rounded-lg border border-amber-300 text-sm hover:bg-amber-100 whitespace-nowrap">Copy</button>
            </div>
            <button type="button" (click)="revealedKey.set(null)" class="text-xs text-amber-700 hover:underline mt-2">Done</button>
          </div>
        }

        @if (keyEditing()) {
          <div class="bg-white border border-slate-200 rounded-xl p-4 mb-5">
            <label class="block mb-3"><span class="lbl">Label</span><input [(ngModel)]="keyForm.label" name="label" placeholder="e.g. Inventory sync" class="input w-full" /></label>
            <span class="lbl block mb-1">Scopes</span>
            <div class="flex flex-wrap gap-2 mb-4">
              @for (s of availableScopes(); track s) {
                <label class="flex items-center gap-1.5 text-sm border border-slate-200 rounded-lg px-2.5 py-1.5 cursor-pointer" [class.bg-blue-50]="keyForm.scopes.includes(s)" [class.border-blue-300]="keyForm.scopes.includes(s)">
                  <input type="checkbox" [checked]="keyForm.scopes.includes(s)" (change)="toggleScope(s)" /> {{ s }}
                </label>
              }
            </div>
            <div class="flex gap-2">
              <button type="button" (click)="saveKey()" [disabled]="keySaving()" class="btn-primary">{{ keySaving() ? 'Creating…' : 'Create key' }}</button>
              <button type="button" (click)="keyEditing.set(false)" class="px-4 py-2 rounded-lg border border-slate-300 text-sm hover:bg-slate-50">Cancel</button>
            </div>
          </div>
        }

        @if (keysLoading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
        @else if (!keys().length) { <div class="p-8 text-center text-slate-400 border border-dashed border-slate-200 rounded-xl">No API keys yet.</div> }
        @else {
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead class="text-left text-slate-400 border-b border-slate-200"><tr><th class="py-2">Label</th><th>Key</th><th>Scopes</th><th>Last used</th><th>Status</th><th></th></tr></thead>
              <tbody>
                @for (k of keys(); track k.id) {
                  <tr class="border-b border-slate-100">
                    <td class="py-2 font-medium text-slate-800">{{ k.label }}</td>
                    <td class="text-slate-500"><code class="text-xs">{{ k.keyPrefix }}…</code></td>
                    <td class="text-slate-600 text-xs">{{ k.scopes.join(', ') }}</td>
                    <td class="text-slate-500 text-xs">{{ k.lastUsedAt ? (k.lastUsedAt | date: 'medium') : 'Never' }}</td>
                    <td><span class="text-xs px-1.5 py-0.5 rounded" [class]="k.revokedAt ? 'bg-slate-100 text-slate-500' : 'bg-green-50 text-green-700 border border-green-200'">{{ k.revokedAt ? 'Revoked' : 'Active' }}</span></td>
                    <td class="text-right whitespace-nowrap">
                      @if (!k.revokedAt) { <button type="button" (click)="revokeKey(k)" class="text-red-500 hover:underline text-xs">Revoke</button> }
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>

      <!-- ================= Webhooks ================= -->
      <section>
        <div class="flex items-center justify-between mb-1">
          <h2 class="font-semibold text-slate-800">Webhooks</h2>
          <button type="button" (click)="startNewHook()" class="btn-primary">+ New subscription</button>
        </div>
        <p class="text-sm text-slate-500 mb-4">We'll POST a signed JSON payload to your URL when a subscribed event happens, retrying with backoff on failure.</p>

        @if (hookMessage()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ hookMessage() }}</div> }
        @if (hookError()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ hookError() }}</div> }

        @if (revealedHook(); as rh) {
          <div class="bg-amber-50 border border-amber-200 rounded-xl p-4 mb-5">
            <p class="text-sm font-medium text-amber-800 mb-2">Copy this signing secret now — you won't be able to see it again. Use it to verify the <code>X-WavCommerce-Signature</code> header.</p>
            <div class="flex items-center gap-2">
              <code class="flex-1 bg-white border border-amber-200 rounded px-2 py-1.5 text-sm break-all">{{ rh.secret }}</code>
              <button type="button" (click)="copy(rh.secret)" class="px-3 py-1.5 rounded-lg border border-amber-300 text-sm hover:bg-amber-100 whitespace-nowrap">Copy</button>
            </div>
            <button type="button" (click)="revealedHook.set(null)" class="text-xs text-amber-700 hover:underline mt-2">Done</button>
          </div>
        }

        @if (hookEditing()) {
          <div class="bg-white border border-slate-200 rounded-xl p-4 mb-5">
            <label class="block mb-3"><span class="lbl">Endpoint URL</span><input [(ngModel)]="hookForm.url" name="url" placeholder="https://example.com/webhooks/wavcommerce" class="input w-full" /></label>
            <span class="lbl block mb-1">Events</span>
            <div class="flex flex-wrap gap-2 mb-4">
              @for (e of availableEvents(); track e) {
                <label class="flex items-center gap-1.5 text-sm border border-slate-200 rounded-lg px-2.5 py-1.5 cursor-pointer" [class.bg-blue-50]="hookForm.events.includes(e)" [class.border-blue-300]="hookForm.events.includes(e)">
                  <input type="checkbox" [checked]="hookForm.events.includes(e)" (change)="toggleEvent(e)" /> {{ e }}
                </label>
              }
            </div>
            <div class="flex gap-2">
              <button type="button" (click)="saveHook()" [disabled]="hookSaving()" class="btn-primary">{{ hookSaving() ? 'Creating…' : 'Create subscription' }}</button>
              <button type="button" (click)="hookEditing.set(false)" class="px-4 py-2 rounded-lg border border-slate-300 text-sm hover:bg-slate-50">Cancel</button>
            </div>
          </div>
        }

        @if (hooksLoading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
        @else if (!hooks().length) { <div class="p-8 text-center text-slate-400 border border-dashed border-slate-200 rounded-xl">No webhook subscriptions yet.</div> }
        @else {
          <div class="overflow-x-auto">
            <table class="w-full text-sm">
              <thead class="text-left text-slate-400 border-b border-slate-200"><tr><th class="py-2">URL</th><th>Events</th><th>Status</th><th></th></tr></thead>
              <tbody>
                @for (h of hooks(); track h.id) {
                  <tr class="border-b border-slate-100">
                    <td class="py-2 font-medium text-slate-800 break-all">{{ h.url }}</td>
                    <td class="text-slate-600 text-xs">{{ h.events.join(', ') }}</td>
                    <td><span class="text-xs px-1.5 py-0.5 rounded" [class]="h.isActive ? 'bg-green-50 text-green-700 border border-green-200' : 'bg-slate-100 text-slate-500'">{{ h.isActive ? 'Active' : 'Paused' }}</span></td>
                    <td class="text-right whitespace-nowrap">
                      <button type="button" (click)="toggleHookActive(h)" class="text-blue-600 hover:underline text-xs mr-3">{{ h.isActive ? 'Pause' : 'Resume' }}</button>
                      <button type="button" (click)="removeHook(h)" class="text-red-500 hover:underline text-xs">Delete</button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        }
      </section>
    </div>
  `,
})
export class AdminDeveloperComponent implements OnInit {
  private readonly apiKeys = inject(ApiKeysService);
  private readonly webhooks = inject(WebhooksService);

  // --- keys ---
  readonly keys = signal<ApiKey[]>([]);
  readonly keysLoading = signal(true);
  readonly availableScopes = signal<string[]>([]);
  readonly keyEditing = signal(false);
  readonly keySaving = signal(false);
  readonly keyMessage = signal<string | null>(null);
  readonly keyError = signal<string | null>(null);
  readonly revealedKey = signal<CreatedApiKey | null>(null);
  keyForm: { label: string; scopes: string[] } = { label: '', scopes: [] };

  // --- webhooks ---
  readonly hooks = signal<WebhookSubscription[]>([]);
  readonly hooksLoading = signal(true);
  readonly availableEvents = signal<string[]>([]);
  readonly hookEditing = signal(false);
  readonly hookSaving = signal(false);
  readonly hookMessage = signal<string | null>(null);
  readonly hookError = signal<string | null>(null);
  readonly revealedHook = signal<CreatedWebhookSubscription | null>(null);
  hookForm: { url: string; events: string[] } = { url: '', events: [] };

  ngOnInit(): void {
    this.loadKeys();
    this.apiKeys.scopes().subscribe((s) => this.availableScopes.set(s));
    this.loadHooks();
    this.webhooks.events().subscribe((e) => this.availableEvents.set(e));
  }

  private loadKeys(): void {
    this.keysLoading.set(true);
    this.apiKeys.list().subscribe({ next: (k) => { this.keys.set(k); this.keysLoading.set(false); }, error: () => this.keysLoading.set(false) });
  }
  private loadHooks(): void {
    this.hooksLoading.set(true);
    this.webhooks.list().subscribe({ next: (h) => { this.hooks.set(h); this.hooksLoading.set(false); }, error: () => this.hooksLoading.set(false) });
  }

  startNewKey(): void { this.keyForm = { label: '', scopes: [] }; this.keyEditing.set(true); this.keyMessage.set(null); this.keyError.set(null); }
  toggleScope(s: string): void {
    this.keyForm.scopes = this.keyForm.scopes.includes(s) ? this.keyForm.scopes.filter((x) => x !== s) : [...this.keyForm.scopes, s];
  }
  saveKey(): void {
    if (!this.keyForm.label.trim()) { this.keyError.set('A label is required.'); return; }
    if (!this.keyForm.scopes.length) { this.keyError.set('Pick at least one scope.'); return; }
    this.keySaving.set(true); this.keyError.set(null);
    this.apiKeys.create(this.keyForm).subscribe({
      next: (created) => {
        this.keySaving.set(false); this.keyEditing.set(false);
        this.revealedKey.set(created);
        this.keyMessage.set(null);
        this.loadKeys();
      },
      error: (e) => { this.keySaving.set(false); this.keyError.set(e?.error?.message ?? 'Could not create key.'); },
    });
  }
  revokeKey(k: ApiKey): void {
    if (!confirm(`Revoke "${k.label}"? Any system using this key will immediately lose access.`)) return;
    this.apiKeys.revoke(k.id).subscribe({ next: () => { this.keyMessage.set('Key revoked.'); this.loadKeys(); } });
  }

  startNewHook(): void { this.hookForm = { url: '', events: [] }; this.hookEditing.set(true); this.hookMessage.set(null); this.hookError.set(null); }
  toggleEvent(e: string): void {
    this.hookForm.events = this.hookForm.events.includes(e) ? this.hookForm.events.filter((x) => x !== e) : [...this.hookForm.events, e];
  }
  saveHook(): void {
    if (!this.hookForm.url.trim()) { this.hookError.set('An endpoint URL is required.'); return; }
    if (!this.hookForm.events.length) { this.hookError.set('Pick at least one event.'); return; }
    this.hookSaving.set(true); this.hookError.set(null);
    this.webhooks.create(this.hookForm).subscribe({
      next: (created) => {
        this.hookSaving.set(false); this.hookEditing.set(false);
        this.revealedHook.set(created);
        this.hookMessage.set(null);
        this.loadHooks();
      },
      error: (e) => { this.hookSaving.set(false); this.hookError.set(e?.error?.message ?? 'Could not create subscription.'); },
    });
  }
  toggleHookActive(h: WebhookSubscription): void {
    this.webhooks.setActive(h.id, !h.isActive).subscribe({ next: () => this.loadHooks() });
  }
  removeHook(h: WebhookSubscription): void {
    if (!confirm(`Delete this webhook subscription? Deliveries to ${h.url} will stop immediately.`)) return;
    this.webhooks.remove(h.id).subscribe({ next: () => { this.hookMessage.set('Subscription removed.'); this.loadHooks(); } });
  }

  copy(text: string): void { navigator.clipboard?.writeText(text); }
}
