import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { CustomerAdminService } from '../../../core/services/customer-admin.service';
import { CustomerDetail } from '../../../core/models/customer.model';

@Component({
  selector: 'app-admin-customer-detail',
  imports: [FormsModule, RouterLink, DecimalPipe, DatePipe],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <a routerLink="/admin/customers" class="text-sm text-slate-500 hover:text-slate-800">← Customers</a>

      @if (loading()) { <p class="text-slate-400 text-sm mt-4">Loading…</p> }
      @else if (c(); as cust) {
        <div class="flex items-center justify-between mt-2 mb-5">
          <div>
            <h1 class="text-xl font-bold text-slate-900">{{ cust.fullName || 'No name' }}</h1>
            <p class="text-sm text-slate-500">{{ cust.email || '—' }}{{ cust.phoneNumber ? ' · ' + cust.phoneNumber : '' }}
              · Customer since {{ cust.createdAt | date:'mediumDate' }}</p>
          </div>
        </div>

        <!-- lifetime tiles -->
        <div class="grid grid-cols-3 gap-4 mb-6">
          <div class="bg-white border border-slate-200 rounded-xl p-4"><div class="text-xs text-slate-500">Orders</div><div class="text-2xl font-bold text-slate-900">{{ cust.orderCount }}</div></div>
          <div class="bg-white border border-slate-200 rounded-xl p-4"><div class="text-xs text-slate-500">Lifetime spend</div><div class="text-2xl font-bold text-slate-900">₹{{ cust.totalSpent | number:'1.0-0' }}</div></div>
          <div class="bg-white border border-slate-200 rounded-xl p-4"><div class="text-xs text-slate-500">Last order</div><div class="text-lg font-semibold text-slate-800 mt-1">{{ cust.lastOrderAt ? (cust.lastOrderAt | date:'mediumDate') : '—' }}</div></div>
        </div>

        <div class="grid lg:grid-cols-3 gap-6">
          <!-- editable CRM -->
          <div class="lg:col-span-2 space-y-6">
            <div class="bg-white border border-slate-200 rounded-xl p-5">
              <h2 class="font-semibold text-slate-800 mb-3">Details</h2>
              <div class="grid sm:grid-cols-2 gap-4">
                <div><label class="lbl">Full name</label><input class="input" [(ngModel)]="form.fullName" name="name" /></div>
                <div><label class="lbl">Phone</label><input class="input" [(ngModel)]="form.phoneNumber" name="phone" /></div>
              </div>
              <p class="text-xs text-slate-400 mt-2">Email ({{ cust.email || 'none' }}) is the customer's login and can't be changed here.</p>
            </div>

            <div class="bg-white border border-slate-200 rounded-xl p-5">
              <h2 class="font-semibold text-slate-800 mb-3">Marketing consent</h2>
              <label class="flex items-center gap-2 mb-2"><input type="checkbox" [(ngModel)]="form.acceptsEmailMarketing" name="em" /> <span class="text-sm text-slate-700">Email marketing</span></label>
              <label class="flex items-center gap-2 mb-2"><input type="checkbox" [(ngModel)]="form.acceptsSmsMarketing" name="sms" /> <span class="text-sm text-slate-700">SMS marketing</span></label>
              <label class="flex items-center gap-2"><input type="checkbox" [(ngModel)]="form.acceptsWhatsappMarketing" name="wa" /> <span class="text-sm text-slate-700">WhatsApp marketing</span></label>
              <p class="text-xs text-slate-400 mt-2">Only message customers who have opted in.</p>
            </div>

            <div class="bg-white border border-slate-200 rounded-xl p-5">
              <h2 class="font-semibold text-slate-800 mb-3">Notes & tags</h2>
              <label class="lbl">Tags (comma-separated)</label>
              <input class="input" [(ngModel)]="form.tags" name="tags" placeholder="vip, wholesale" />
              @if (suggestedTags().length) {
                <div class="flex flex-wrap gap-1.5 mt-2 mb-3">
                  @for (t of suggestedTags(); track t) {
                    <button type="button" (click)="addTag(t)" class="text-xs px-2 py-0.5 rounded-full border border-slate-200 text-slate-500 hover:bg-slate-50">+ {{ t }}</button>
                  }
                </div>
              } @else { <div class="mb-3"></div> }
              <label class="lbl">Private notes</label>
              <textarea class="input" rows="3" [(ngModel)]="form.notes" name="notes" placeholder="Only visible to your team."></textarea>
            </div>

            <div class="flex items-center gap-3">
              <button type="button" (click)="save()" [disabled]="saving()" class="btn-primary px-5 py-2.5">{{ saving() ? 'Saving…' : 'Save changes' }}</button>
              @if (saved()) { <span class="text-sm text-green-600">✓ Saved</span> }
              @if (error()) { <span class="text-sm text-red-600">{{ error() }}</span> }
            </div>
          </div>

          <!-- addresses + orders -->
          <div class="space-y-6">
            <div class="bg-white border border-slate-200 rounded-xl p-5">
              <h2 class="font-semibold text-slate-800 mb-3">Addresses</h2>
              @for (a of cust.addresses; track a.customerAddressId) {
                <div class="text-sm text-slate-600 mb-3 last:mb-0">
                  @if (a.isDefault) { <span class="text-[10px] bg-slate-100 text-slate-500 px-1.5 py-0.5 rounded mr-1">Default</span> }
                  {{ a.recipientName || cust.fullName }}<br />
                  {{ a.line1 }}{{ a.line2 ? ', ' + a.line2 : '' }}<br />
                  {{ a.city }}, {{ a.state }} {{ a.pincode }}
                </div>
              }
              @if (!cust.addresses.length) { <p class="text-sm text-slate-400">No addresses.</p> }
            </div>

            <div class="bg-white border border-slate-200 rounded-xl p-5">
              <h2 class="font-semibold text-slate-800 mb-3">Recent orders</h2>
              @for (o of cust.recentOrders; track o.orderId) {
                <div class="flex items-center justify-between text-sm py-1.5 border-b border-slate-50 last:border-0">
                  <div><div class="font-medium text-slate-700">{{ o.orderNumber }}</div><div class="text-xs text-slate-400">{{ o.status }} · {{ o.placedAt ? (o.placedAt | date:'shortDate') : '—' }}</div></div>
                  <span class="font-medium text-slate-800">₹{{ o.totalAmount | number:'1.0-0' }}</span>
                </div>
              }
              @if (!cust.recentOrders.length) { <p class="text-sm text-slate-400">No orders yet.</p> }
            </div>
          </div>
        </div>
      } @else { <p class="text-slate-400 text-sm mt-4">Customer not found.</p> }
    </div>
  `,
})
export class AdminCustomerDetailComponent implements OnInit {
  private readonly api = inject(CustomerAdminService);
  private readonly route = inject(ActivatedRoute);

  readonly c = signal<CustomerDetail | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly error = signal<string | null>(null);
  readonly allTags = signal<string[]>([]);
  private id = 0;

  // Method (not computed) so it re-evaluates as the plain ngModel-bound tags field changes.
  suggestedTags(): string[] {
    const used = new Set((this.form.tags ?? '').split(',').map((t) => t.trim().toLowerCase()).filter(Boolean));
    return this.allTags().filter((t) => !used.has(t.toLowerCase())).slice(0, 12);
  }

  addTag(t: string): void {
    const current = (this.form.tags ?? '').trim();
    this.form.tags = current ? `${current.replace(/,\s*$/, '')}, ${t}` : t;
  }

  form = {
    fullName: '' as string | null, phoneNumber: '' as string | null,
    acceptsEmailMarketing: false, acceptsSmsMarketing: false, acceptsWhatsappMarketing: false,
    notes: '' as string | null, tags: '' as string | null,
  };

  ngOnInit(): void {
    this.id = Number(this.route.snapshot.paramMap.get('id'));
    this.api.tags().subscribe((t) => this.allTags.set(t.map((x) => x.tag)));
    this.api.get(this.id).subscribe({
      next: (cust) => {
        this.c.set(cust);
        this.form = {
          fullName: cust.fullName, phoneNumber: cust.phoneNumber,
          acceptsEmailMarketing: cust.acceptsEmailMarketing, acceptsSmsMarketing: cust.acceptsSmsMarketing,
          acceptsWhatsappMarketing: cust.acceptsWhatsappMarketing, notes: cust.notes, tags: cust.tags.join(', '),
        };
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  save(): void {
    this.saving.set(true);
    this.saved.set(false);
    this.error.set(null);
    this.api.update(this.id, this.form).subscribe({
      next: (cust) => { this.c.set(cust); this.saving.set(false); this.saved.set(true); setTimeout(() => this.saved.set(false), 2500); },
      error: (e: unknown) => { this.saving.set(false); this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not save.'); },
    });
  }
}
