import { DecimalPipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  SaveShippingMethod, SaveShippingZone, ShippingAdminService, ShippingMethod, ShippingZone,
} from '../../../core/services/shipping-admin.service';

@Component({
  selector: 'app-admin-shipping',
  imports: [FormsModule, DecimalPipe],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Shipping</h1>
      <p class="text-sm text-slate-500 mb-5">Set your delivery rate and where you ship. Checkout uses your first active method; zones control per-pincode serviceability and rates.</p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <!-- Live courier rates (Shiprocket) -->
      <div class="mb-6 rounded-xl border px-4 py-3 text-sm flex items-center gap-2"
           [class]="shiprocket() ? 'border-green-200 bg-green-50 text-green-700' : 'border-slate-200 bg-slate-50 text-slate-500'">
        <span>🚚</span>
        @if (shiprocket()) {
          <span><span class="font-medium">Live courier rates on.</span> Shiprocket is providing real-time rates and delivery estimates at checkout.</span>
        } @else {
          <span><span class="font-medium">Live courier rates off.</span> Checkout uses your manual rates below. Shiprocket can be enabled by the platform to fetch live courier rates automatically.</span>
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

  readonly methods = signal<ShippingMethod[]>([]);
  readonly zones = signal<ShippingZone[]>([]);
  readonly shiprocket = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  readonly mForm = signal<(SaveShippingMethod & { id?: number }) | null>(null);
  readonly zForm = signal<(SaveShippingZone & { id?: number }) | null>(null);

  ngOnInit(): void {
    this.api.integration().subscribe((i) => this.shiprocket.set(i.shiprocketEnabled));
    this.load();
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
