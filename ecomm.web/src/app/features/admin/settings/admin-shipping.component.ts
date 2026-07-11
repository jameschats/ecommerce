import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  SaveShippingMethod, SaveShippingZone, ShippingAdminService, ShippingMethod, ShippingZone,
} from '../../../core/services/shipping-admin.service';
import { FulfillmentService, ShiprocketSettings } from '../../../core/services/fulfillment.service';

@Component({
  selector: 'app-admin-shipping',
  imports: [FormsModule, DecimalPipe],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Shipping</h1>
      <p class="text-sm text-slate-500 mb-5">Set your delivery rate and where you ship. Checkout uses your first active method; zones control per-pincode serviceability and rates.</p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <!-- Fulfillment method: Self (manual) vs Shiprocket -->
      <div class="bg-white border border-slate-200 rounded-xl p-5 mb-6">
        <h2 class="font-semibold text-slate-800 mb-1">Fulfillment method</h2>
        <p class="text-xs text-slate-400 mb-3">Choose how you ship orders. Self = enter courier + tracking yourself with your manual rates below. Shiprocket = live rates at checkout, plus automatic AWB, pickup, label and tracking.</p>

        @if (sr(); as s) {
          <div class="space-y-2">
            <label class="flex items-start gap-2 p-3 rounded-lg border cursor-pointer" [class]="method() === 'Self' ? 'border-blue-500 ring-1 ring-blue-200' : 'border-slate-200'">
              <input type="radio" name="ffm" value="Self" [ngModel]="method()" (ngModelChange)="method.set($event)" class="mt-1" />
              <span><span class="font-medium text-slate-800">Self shipping</span><span class="block text-xs text-slate-500">You arrange the courier and enter the tracking number on each order.</span></span>
            </label>
            <label class="flex items-start gap-2 p-3 rounded-lg border cursor-pointer" [class]="method() === 'Shiprocket' ? 'border-blue-500 ring-1 ring-blue-200' : 'border-slate-200'">
              <input type="radio" name="ffm" value="Shiprocket" [ngModel]="method()" (ngModelChange)="method.set($event)" class="mt-1" />
              <span><span class="font-medium text-slate-800">Shiprocket</span>
                @if (s.isVerified && s.method === 'Shiprocket') { <span class="text-xs text-green-600 ml-1">● connected</span> }
                <span class="block text-xs text-slate-500">Live courier rates + auto AWB, pickup, label and tracking.</span></span>
            </label>
          </div>

          @if (method() === 'Shiprocket') {
            <div class="grid sm:grid-cols-2 gap-3 mt-3 border-t border-slate-100 pt-3">
              <label class="block sm:col-span-2"><span class="lbl">Shiprocket email (API user)</span><input class="input" [(ngModel)]="form.email" placeholder="you@store.com" /></label>
              <label class="block sm:col-span-2"><span class="lbl">Shiprocket password</span><input class="input" type="password" [(ngModel)]="form.password" [placeholder]="s.hasPassword ? '•••••••• (leave blank to keep)' : 'Your Shiprocket API password'" /></label>
              <label class="block"><span class="lbl">Pickup pincode</span><input class="input" [(ngModel)]="form.pickupPincode" placeholder="600001" /></label>
              <label class="block"><span class="lbl">Pickup location name</span><input class="input" [(ngModel)]="form.pickupLocation" placeholder="Primary" /></label>
              <p class="text-xs text-slate-400 sm:col-span-2">The pickup location must match a registered pickup address in your Shiprocket panel. We verify your credentials when you save.</p>
            </div>
          }

          <button type="button" (click)="saveFulfillment()" [disabled]="saving()" class="btn-primary text-sm mt-3 disabled:opacity-50">{{ saving() ? 'Saving…' : 'Save fulfillment method' }}</button>
        }
      </div>

      <!-- Methods -->
      <div class="bg-white border border-slate-200 rounded-xl p-5 mb-6">
        <div class="flex items-center justify-between mb-3">
          <h2 class="font-semibold text-slate-800">Delivery rates</h2>
          <button type="button" (click)="newMethod()" class="text-sm text-blue-600 hover:underline">+ Add method</button>
        </div>

        <div class="divide-y divide-slate-100">
          @for (m of methods(); track m.shippingMethodId) {
            <div class="flex items-center justify-between py-2 text-sm">
              <div>
                <span class="font-medium text-slate-800">{{ m.name }}</span>
                @if (!m.isActive) { <span class="text-xs text-slate-400 ml-1">(inactive)</span> }
                <div class="text-xs text-slate-500">₹{{ m.baseRate | number:'1.0-2' }}
                  @if (m.freeShippingThreshold != null) { · free over ₹{{ m.freeShippingThreshold | number:'1.0-0' }} }
                  @if (m.estimatedDays != null) { · {{ m.estimatedDays }}-day }
                </div>
              </div>
              <div class="flex gap-3">
                <button type="button" (click)="editMethod(m)" class="text-slate-500 hover:underline">Edit</button>
                <button type="button" (click)="deleteMethod(m)" class="text-red-500 hover:underline">Delete</button>
              </div>
            </div>
          }
          @if (!methods().length && !mForm()) { <p class="text-sm text-slate-400 py-2">No delivery methods yet.</p> }
        </div>

        @if (mForm(); as f) {
          <div class="border border-slate-200 rounded-lg p-4 mt-3 grid sm:grid-cols-2 gap-3">
            <label class="block sm:col-span-2"><span class="lbl">Name</span><input class="input" [(ngModel)]="f.name" placeholder="Standard delivery" /></label>
            <label class="block"><span class="lbl">Rate (₹)</span><input class="input" type="number" [(ngModel)]="f.baseRate" /></label>
            <label class="block"><span class="lbl">Free over (₹, optional)</span><input class="input" type="number" [(ngModel)]="f.freeShippingThreshold" /></label>
            <label class="block"><span class="lbl">Delivery days (optional)</span><input class="input" type="number" [(ngModel)]="f.estimatedDays" /></label>
            <label class="flex items-center gap-2 mt-6"><input type="checkbox" [(ngModel)]="f.isActive" /> <span class="text-sm text-slate-700">Active</span></label>
            <div class="sm:col-span-2 flex gap-2">
              <button type="button" (click)="saveMethod()" class="btn-primary text-sm">Save</button>
              <button type="button" (click)="mForm.set(null)" class="btn-ghost text-sm">Cancel</button>
            </div>
          </div>
        }
      </div>

      <!-- Zones -->
      <div class="bg-white border border-slate-200 rounded-xl p-5">
        <div class="flex items-center justify-between mb-3">
          <h2 class="font-semibold text-slate-800">Pincode zones</h2>
          <button type="button" (click)="newZone()" class="text-sm text-blue-600 hover:underline">+ Add zone</button>
        </div>
        <p class="text-xs text-slate-400 mb-3">Zones override serviceability + rate for a pincode range. Leave the range blank for a catch-all. No zones = you ship everywhere at the base rate.</p>

        <div class="divide-y divide-slate-100">
          @for (z of zones(); track z.shippingZoneId) {
            <div class="flex items-center justify-between py-2 text-sm">
              <div>
                <span class="font-medium text-slate-800">{{ z.name }}</span>
                @if (!z.isServiceable) { <span class="text-xs text-red-500 ml-1">(not serviceable)</span> }
                <div class="text-xs text-slate-500">
                  {{ z.pincodeStart && z.pincodeEnd ? z.pincodeStart + '–' + z.pincodeEnd : 'All pincodes' }} · ₹{{ z.rate | number:'1.0-2' }}
                </div>
              </div>
              <div class="flex gap-3">
                <button type="button" (click)="editZone(z)" class="text-slate-500 hover:underline">Edit</button>
                <button type="button" (click)="deleteZone(z)" class="text-red-500 hover:underline">Delete</button>
              </div>
            </div>
          }
          @if (!zones().length && !zForm()) { <p class="text-sm text-slate-400 py-2">No zones — you ship everywhere at the base rate.</p> }
        </div>

        @if (zForm(); as f) {
          <div class="border border-slate-200 rounded-lg p-4 mt-3 grid sm:grid-cols-2 gap-3">
            <label class="block sm:col-span-2"><span class="lbl">Name</span><input class="input" [(ngModel)]="f.name" placeholder="Metro cities" /></label>
            <label class="block"><span class="lbl">Pincode from</span><input class="input" [(ngModel)]="f.pincodeStart" placeholder="600001" /></label>
            <label class="block"><span class="lbl">Pincode to</span><input class="input" [(ngModel)]="f.pincodeEnd" placeholder="699999" /></label>
            <label class="block"><span class="lbl">Rate (₹)</span><input class="input" type="number" [(ngModel)]="f.rate" /></label>
            <label class="flex items-center gap-2 mt-6"><input type="checkbox" [(ngModel)]="f.isServiceable" /> <span class="text-sm text-slate-700">Serviceable</span></label>
            <div class="sm:col-span-2 flex gap-2">
              <button type="button" (click)="saveZone()" class="btn-primary text-sm">Save</button>
              <button type="button" (click)="zForm.set(null)" class="btn-ghost text-sm">Cancel</button>
            </div>
          </div>
        }
      </div>
    </div>
  `,
})
export class AdminShippingComponent implements OnInit {
  private readonly api = inject(ShippingAdminService);
  private readonly fulfillment = inject(FulfillmentService);

  readonly methods = signal<ShippingMethod[]>([]);
  readonly zones = signal<ShippingZone[]>([]);
  readonly sr = signal<ShiprocketSettings | null>(null);
  readonly method = signal<'Self' | 'Shiprocket'>('Self');
  readonly saving = signal(false);
  form: { email: string | null; password: string | null; pickupPincode: string | null; pickupLocation: string | null } =
    { email: '', password: '', pickupPincode: '', pickupLocation: '' };
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly mForm = signal<(SaveShippingMethod & { id?: number }) | null>(null);
  readonly zForm = signal<(SaveShippingZone & { id?: number }) | null>(null);

  ngOnInit(): void {
    this.loadFulfillment();
    this.load();
  }
  private loadFulfillment(): void {
    this.fulfillment.getSettings().subscribe((s) => {
      this.sr.set(s);
      this.method.set(s.method);
      this.form = { email: s.email, password: '', pickupPincode: s.pickupPincode, pickupLocation: s.pickupLocation };
    });
  }
  saveFulfillment(): void {
    this.saving.set(true);
    this.fulfillment.updateSettings({
      method: this.method(),
      email: this.form.email, password: this.form.password || null,
      pickupPincode: this.form.pickupPincode, pickupLocation: this.form.pickupLocation,
    }).subscribe({
      next: (s) => { this.sr.set(s); this.method.set(s.method); this.form.password = ''; this.saving.set(false); this.toast('Fulfillment settings saved.'); },
      error: (e: unknown) => { this.saving.set(false); this.fail(e); },
    });
  }
  private load(): void {
    this.api.listMethods().subscribe((m) => this.methods.set(m));
    this.api.listZones().subscribe((z) => this.zones.set(z));
  }
  private toast(m: string): void { this.message.set(m); this.error.set(null); setTimeout(() => this.message.set(null), 2500); }
  private fail(e: unknown): void { this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Something went wrong.'); }

  // methods
  newMethod(): void { this.mForm.set({ name: '', description: null, baseRate: 0, freeShippingThreshold: null, estimatedDays: null, isActive: true }); }
  editMethod(m: ShippingMethod): void { this.mForm.set({ id: m.shippingMethodId, name: m.name, description: m.description, baseRate: m.baseRate, freeShippingThreshold: m.freeShippingThreshold, estimatedDays: m.estimatedDays, isActive: m.isActive }); }
  saveMethod(): void {
    const f = this.mForm(); if (!f) return;
    const body: SaveShippingMethod = { name: f.name, description: f.description ?? null, baseRate: Number(f.baseRate) || 0, freeShippingThreshold: f.freeShippingThreshold != null && f.freeShippingThreshold !== ('' as unknown) ? Number(f.freeShippingThreshold) : null, estimatedDays: f.estimatedDays != null && f.estimatedDays !== ('' as unknown) ? Number(f.estimatedDays) : null, isActive: f.isActive };
    const obs = f.id ? this.api.updateMethod(f.id, body) : this.api.createMethod(body);
    obs.subscribe({ next: () => { this.mForm.set(null); this.toast('Saved.'); this.load(); }, error: (e: unknown) => this.fail(e) });
  }
  deleteMethod(m: ShippingMethod): void { if (!confirm(`Delete "${m.name}"?`)) return; this.api.deleteMethod(m.shippingMethodId).subscribe({ next: () => { this.toast('Deleted.'); this.load(); }, error: (e: unknown) => this.fail(e) }); }

  // zones
  newZone(): void { this.zForm.set({ name: '', pincodeStart: null, pincodeEnd: null, rate: 0, isServiceable: true }); }
  editZone(z: ShippingZone): void { this.zForm.set({ id: z.shippingZoneId, name: z.name, pincodeStart: z.pincodeStart, pincodeEnd: z.pincodeEnd, rate: z.rate, isServiceable: z.isServiceable }); }
  saveZone(): void {
    const f = this.zForm(); if (!f) return;
    const body: SaveShippingZone = { name: f.name, pincodeStart: f.pincodeStart?.trim() || null, pincodeEnd: f.pincodeEnd?.trim() || null, rate: Number(f.rate) || 0, isServiceable: f.isServiceable };
    const obs = f.id ? this.api.updateZone(f.id, body) : this.api.createZone(body);
    obs.subscribe({ next: () => { this.zForm.set(null); this.toast('Saved.'); this.load(); }, error: (e: unknown) => this.fail(e) });
  }
  deleteZone(z: ShippingZone): void { if (!confirm(`Delete "${z.name}"?`)) return; this.api.deleteZone(z.shippingZoneId).subscribe({ next: () => { this.toast('Deleted.'); this.load(); }, error: (e: unknown) => this.fail(e) }); }
}
