import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminCoupon, SaveCouponRequest } from '../../../core/models/coupon.model';
import { CouponService } from '../../../core/services/coupon.service';

@Component({
  selector: 'app-admin-coupons',
  imports: [FormsModule, DatePipe],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">Discounts</h1>
        <button type="button" (click)="startNew()" class="btn-primary">+ New discount</button>
      </div>
      <p class="text-sm text-slate-500 mb-4">Discount codes customers enter, or automatic offers applied at checkout.</p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      @if (editing()) {
        <div class="bg-white border border-slate-200 rounded-xl p-4 mb-5">
          <h2 class="font-semibold text-slate-800 mb-3">{{ form.couponId ? 'Edit discount' : 'New discount' }}</h2>
          <div class="grid sm:grid-cols-2 gap-3">
            <label class="block"><span class="lbl">Method</span>
              <select [(ngModel)]="form.method" name="method" class="input w-full">
                <option value="Code">Discount code (customer types it)</option>
                <option value="Automatic">Automatic (applied at checkout)</option>
              </select></label>
            <label class="block"><span class="lbl">{{ form.method === 'Automatic' ? 'Name' : 'Code' }}</span>
              <input [(ngModel)]="form.code" name="code" [placeholder]="form.method === 'Automatic' ? 'AUTUMN-SALE' : 'SAVE10'" class="input w-full uppercase" /></label>
            <label class="block"><span class="lbl">Description</span>
              <input [(ngModel)]="form.description" name="desc" placeholder="10% off" class="input w-full" /></label>
            <label class="block"><span class="lbl">Type</span>
              <select [(ngModel)]="form.discountType" name="type" class="input w-full">
                <option value="Flat">Flat (₹)</option>
                <option value="Percentage">Percentage (%)</option>
              </select></label>
            <label class="block"><span class="lbl">Value {{ form.freeShipping ? '(optional)' : '' }}</span>
              <input type="number" [(ngModel)]="form.discountValue" name="val" class="input w-full" /></label>
            @if (form.discountType === 'Percentage') {
              <label class="block"><span class="lbl">Max discount (₹, optional)</span>
                <input type="number" [(ngModel)]="form.maxDiscountAmount" name="max" class="input w-full" /></label>
            }
            <label class="block"><span class="lbl">Min order (₹, optional)</span>
              <input type="number" [(ngModel)]="form.minOrderAmount" name="min" class="input w-full" /></label>
            <label class="block"><span class="lbl">Total usage limit (optional)</span>
              <input type="number" [(ngModel)]="form.usageLimit" name="ul" class="input w-full" /></label>
            <label class="block"><span class="lbl">Per-user limit (optional)</span>
              <input type="number" [(ngModel)]="form.perUserLimit" name="pul" class="input w-full" /></label>
            <label class="block"><span class="lbl">Starts (optional)</span>
              <input type="date" [(ngModel)]="form.startsAt" name="sa" class="input w-full" /></label>
            <label class="block"><span class="lbl">Ends (optional)</span>
              <input type="date" [(ngModel)]="form.endsAt" name="ea" class="input w-full" /></label>
          </div>
          <div class="flex flex-wrap gap-x-6 gap-y-2 mt-3">
            <label class="flex items-center gap-2 text-sm text-slate-600">
              <input type="checkbox" [(ngModel)]="form.freeShipping" name="freeship" /> Also give free shipping
            </label>
            <label class="flex items-center gap-2 text-sm text-slate-600">
              <input type="checkbox" [(ngModel)]="form.isActive" name="active" /> Active
            </label>
          </div>
          <div class="flex gap-2 mt-4">
            <button type="button" (click)="save()" [disabled]="saving()" class="btn-primary">{{ saving() ? 'Saving…' : 'Save' }}</button>
            <button type="button" (click)="editing.set(false)" class="px-4 py-2 rounded-lg border border-slate-300 text-sm hover:bg-slate-50">Cancel</button>
          </div>
        </div>
      }

      @if (loading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
      @else if (!coupons().length) { <div class="p-8 text-center text-slate-400">No coupons yet.</div> }
      @else {
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="text-left text-slate-400 border-b border-slate-200">
              <tr><th class="py-2">Code / name</th><th>Discount</th><th>Min order</th><th>Used</th><th>Window</th><th>Status</th><th></th></tr>
            </thead>
            <tbody>
              @for (c of coupons(); track c.couponId) {
                <tr class="border-b border-slate-100">
                  <td class="py-2 font-medium text-slate-800">{{ c.code }}
                    @if (c.method === 'Automatic') { <span class="text-[10px] bg-blue-50 text-blue-600 border border-blue-200 px-1 py-0.5 rounded ml-1 align-middle">auto</span> }
                    <div class="text-xs text-slate-400 font-normal">{{ c.description }}</div></td>
                  <td>
                    @if (c.discountValue > 0) { {{ c.discountType === 'Percentage' ? c.discountValue + '%' : ('₹' + c.discountValue) }}<span class="text-xs text-slate-400">{{ c.discountType === 'Percentage' && c.maxDiscountAmount ? ' (max ₹' + c.maxDiscountAmount + ')' : '' }}</span> }
                    @if (c.freeShipping) { <span class="text-xs text-green-600">{{ c.discountValue > 0 ? ' + ' : '' }}free ship</span> }
                  </td>
                  <td>{{ c.minOrderAmount ? ('₹' + c.minOrderAmount) : '—' }}</td>
                  <td>{{ c.usedCount }}{{ c.usageLimit ? ' / ' + c.usageLimit : '' }}</td>
                  <td class="text-xs text-slate-500">{{ c.startsAt ? (c.startsAt | date:'dd MMM') : '—' }} → {{ c.endsAt ? (c.endsAt | date:'dd MMM') : '—' }}</td>
                  <td><span class="text-xs font-medium px-1.5 py-0.5 rounded" [class]="c.isActive ? 'bg-green-50 text-green-700 border border-green-200' : 'bg-slate-100 text-slate-500'">{{ c.isActive ? 'Active' : 'Off' }}</span></td>
                  <td class="text-right whitespace-nowrap">
                    <button type="button" (click)="edit(c)" class="text-blue-600 hover:underline text-xs mr-3">Edit</button>
                    <button type="button" (click)="remove(c)" class="text-red-500 hover:underline text-xs">Delete</button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
})
export class AdminCouponsComponent implements OnInit {
  private readonly svc = inject(CouponService);

  readonly coupons = signal<AdminCoupon[]>([]);
  readonly loading = signal(true);
  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  form: SaveCouponRequest & { couponId?: number } = this.blank();

  ngOnInit(): void { this.load(); }

  private blank(): SaveCouponRequest & { couponId?: number } {
    return { code: '', method: 'Code', description: null, discountType: 'Flat', discountValue: 0, freeShipping: false,
      maxDiscountAmount: null, minOrderAmount: null, usageLimit: null, perUserLimit: null, startsAt: null, endsAt: null, isActive: true };
  }

  private load(): void {
    this.loading.set(true);
    this.svc.list().subscribe({
      next: (c) => { this.coupons.set(c); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  startNew(): void { this.form = this.blank(); this.editing.set(true); this.message.set(null); this.error.set(null); }

  edit(c: AdminCoupon): void {
    this.form = { ...c, startsAt: c.startsAt?.slice(0, 10) ?? null, endsAt: c.endsAt?.slice(0, 10) ?? null };
    this.editing.set(true);
    this.message.set(null);
    this.error.set(null);
  }

  save(): void {
    this.saving.set(true);
    this.error.set(null);
    const body: SaveCouponRequest = { ...this.form,
      startsAt: this.form.startsAt || null, endsAt: this.form.endsAt || null };
    const req = this.form.couponId ? this.svc.update(this.form.couponId, body) : this.svc.create(body);
    req.subscribe({
      next: () => { this.saving.set(false); this.editing.set(false); this.message.set('Coupon saved.'); this.load(); },
      error: (e) => { this.saving.set(false); this.error.set(e?.error?.message ?? 'Save failed.'); },
    });
  }

  remove(c: AdminCoupon): void {
    if (!confirm(`Delete coupon ${c.code}?`)) return;
    this.svc.remove(c.couponId).subscribe({ next: () => { this.message.set('Coupon deleted.'); this.load(); } });
  }
}
