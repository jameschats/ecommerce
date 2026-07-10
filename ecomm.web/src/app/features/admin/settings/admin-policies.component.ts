import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PolicyAdminService, StorePolicy } from '../../../core/services/policy-admin.service';

@Component({
  selector: 'app-admin-policies',
  imports: [FormsModule],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Policies</h1>
      <p class="text-sm text-slate-500 mb-4">Your store's legal pages. Ones you fill in are shown on the storefront and linked in the footer.</p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      <div class="grid sm:grid-cols-2 gap-6">
        <!-- list -->
        <div class="bg-white border border-slate-200 rounded-xl divide-y divide-slate-100 self-start">
          @for (p of policies(); track p.handle) {
            <button type="button" (click)="pick(p)" class="w-full text-left px-4 py-3 hover:bg-slate-50 flex items-center justify-between"
              [class.bg-slate-50]="active()?.handle === p.handle">
              <span class="text-sm font-medium text-slate-800">{{ p.title }}</span>
              @if (p.hasContent) { <span class="text-xs text-green-600">✓</span> } @else { <span class="text-xs text-slate-300">empty</span> }
            </button>
          }
        </div>

        <!-- editor -->
        @if (active(); as p) {
          <div class="bg-white border border-slate-200 rounded-xl p-5">
            <label class="lbl">Title</label>
            <input class="input mb-3" [(ngModel)]="title" />
            <label class="lbl">Content (HTML — scripts are stripped)</label>
            <textarea class="input font-mono text-xs" rows="12" [(ngModel)]="body" placeholder="<p>Your policy…</p>"></textarea>
            <button type="button" (click)="save()" [disabled]="saving()" class="btn-primary mt-3">{{ saving() ? 'Saving…' : 'Save policy' }}</button>
          </div>
        } @else {
          <div class="text-slate-400 text-sm p-6 text-center border border-dashed border-slate-200 rounded-xl">Pick a policy to edit.</div>
        }
      </div>
    </div>
  `,
})
export class AdminPoliciesComponent implements OnInit {
  private readonly api = inject(PolicyAdminService);
  readonly policies = signal<StorePolicy[]>([]);
  readonly active = signal<StorePolicy | null>(null);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);
  title = '';
  body = '';

  ngOnInit(): void { this.load(); }
  private load(): void { this.api.list().subscribe((p) => this.policies.set(p)); }

  pick(p: StorePolicy): void { this.active.set(p); this.title = p.title; this.body = p.bodyHtml ?? ''; }

  save(): void {
    const p = this.active(); if (!p) return;
    this.saving.set(true);
    this.api.save(p.handle, this.title, this.body).subscribe({
      next: () => { this.saving.set(false); this.message.set('Policy saved.'); setTimeout(() => this.message.set(null), 2500); this.load(); },
      error: () => this.saving.set(false),
    });
  }
}
