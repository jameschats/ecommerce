import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { StoreSettings } from '../../../core/models/admin-catalog.model';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';

@Component({
  selector: 'app-admin-store-settings',
  imports: [FormsModule],
  template: `
    <div class="max-w-2xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Store settings</h1>
      <p class="text-sm text-slate-500 mb-5">Your store profile, tax display and checkout options.</p>

      @if (loading()) {
        <p class="text-slate-400 text-sm">Loading…</p>
      } @else {
        <form (ngSubmit)="save()" class="bg-white border border-slate-200 rounded-xl p-6 space-y-5">
          <!-- Store profile -->
          <div>
            <h2 class="text-sm font-semibold text-slate-800 mb-2">Store profile</h2>
            <div class="grid sm:grid-cols-2 gap-4">
              <div><label class="lbl">Contact email</label><input class="input" type="email" [(ngModel)]="form.storeEmail" name="email" placeholder="hello@yourstore.com" /></div>
              <div><label class="lbl">Contact phone</label><input class="input" [(ngModel)]="form.storePhone" name="phone" placeholder="+91…" /></div>
              <div class="sm:col-span-2"><label class="lbl">Store address</label><textarea class="input" rows="2" [(ngModel)]="form.storeAddress" name="address" placeholder="Street, city, state, PIN"></textarea></div>
              <div>
                <label class="lbl">Timezone</label>
                <select class="input" [(ngModel)]="form.timezone" name="timezone">
                  <option value="">Select…</option>
                  @for (tz of timezones; track tz) { <option [value]="tz">{{ tz }}</option> }
                </select>
              </div>
            </div>
            <p class="text-xs text-slate-400 mt-2">Used on invoices, notifications and your contact page.</p>
          </div>

          <div class="border-t border-slate-100 pt-4"></div>

          <!-- Tax mode -->
          <div>
            <label class="lbl">GST / Tax mode</label>
            <div class="space-y-2 mt-1">
              @for (m of modes; track m.value) {
                <label class="flex gap-3 p-3 rounded-lg border cursor-pointer" [class]="form.taxMode === m.value ? 'border-primary bg-primary/5' : 'border-slate-200'">
                  <input type="radio" name="taxMode" [value]="m.value" [(ngModel)]="form.taxMode" class="mt-1" />
                  <div>
                    <div class="text-sm font-medium text-slate-800">{{ m.label }}</div>
                    <div class="text-xs text-slate-500">{{ m.hint }}</div>
                  </div>
                </label>
              }
            </div>
            @if (form.taxMode === 'Inclusive') {
              <p class="text-xs text-amber-600 mt-2">⚠️ Listed prices become the final price (GST no longer added on top). Make sure product prices already include GST.</p>
            }
          </div>

          <div class="grid sm:grid-cols-2 gap-4">
            <div><label class="lbl">Store legal name</label><input class="input" [(ngModel)]="form.storeLegalName" name="legalName" /></div>
            <div><label class="lbl">GSTIN</label><input class="input" [(ngModel)]="form.storeGstin" name="gstin" /></div>
            <div><label class="lbl">Store state</label><input class="input" [(ngModel)]="form.storeState" name="state" placeholder="e.g. Tamil Nadu" /></div>
          </div>
          <p class="text-xs text-slate-400">Store state decides intra-state (CGST+SGST) vs inter-state (IGST) on tax invoices.</p>

          <div class="border-t border-slate-100 pt-4">
            <label class="flex items-center gap-3 cursor-pointer">
              <input type="checkbox" [(ngModel)]="form.codEnabled" name="codEnabled" class="w-4 h-4" />
              <span>
                <span class="text-sm font-medium text-slate-800">Cash on delivery (COD)</span>
                <span class="block text-xs text-slate-400">When on, customers can place COD orders at checkout without paying online.</span>
              </span>
            </label>
          </div>

          <div>
            <label class="flex items-center gap-3 cursor-pointer">
              <input type="checkbox" [(ngModel)]="form.abandonedCartRecovery" name="abandonedCartRecovery" class="w-4 h-4" />
              <span>
                <span class="text-sm font-medium text-slate-800">Abandoned-cart recovery emails</span>
                <span class="block text-xs text-slate-400">When on, shoppers who leave items in their cart get a reminder email (once per cart, after a few hours). Requires store email to be set up.</span>
              </span>
            </label>
          </div>

          <div class="flex items-center gap-3 pt-1">
            <button type="submit" [disabled]="saving()" class="btn-primary px-5 py-2.5">{{ saving() ? 'Saving…' : 'Save settings' }}</button>
            @if (saved()) { <span class="text-sm text-green-600">✓ Saved</span> }
            @if (error()) { <span class="text-sm text-red-600">{{ error() }}</span> }
          </div>
        </form>
      }
    </div>
  `,
})
export class AdminStoreSettingsComponent implements OnInit {
  private readonly api = inject(AdminCatalogService);

  readonly modes = [
    { value: 'Exclusive', label: 'Exclusive (GST added on top)', hint: 'Shows CGST/SGST/IGST as separate lines. Default.' },
    { value: 'Inclusive', label: 'Inclusive (prices include GST)', hint: 'Shows "inclusive of all taxes"; GST reverse-calculated on the invoice.' },
    { value: 'None', label: 'None (no GST)', hint: 'Unregistered/composition seller — invoice is a Bill of Supply.' },
  ];

  readonly timezones = [
    'Asia/Kolkata', 'Asia/Dubai', 'Asia/Singapore', 'Europe/London', 'America/New_York', 'America/Los_Angeles', 'UTC',
  ];

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly error = signal<string | null>(null);
  form: StoreSettings = {
    taxMode: 'Exclusive', storeState: '', storeGstin: '', storeLegalName: '', codEnabled: false,
    storeEmail: '', storePhone: '', storeAddress: '', timezone: '', abandonedCartRecovery: false,
  };

  ngOnInit(): void {
    this.api.getStoreSettings().subscribe({
      next: (s) => { this.form = { ...s }; this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  save(): void {
    this.saving.set(true);
    this.saved.set(false);
    this.error.set(null);
    this.api.updateStoreSettings(this.form).subscribe({
      next: (s) => { this.form = { ...s }; this.saving.set(false); this.saved.set(true); setTimeout(() => this.saved.set(false), 2500); },
      error: (e: unknown) => { this.saving.set(false); this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not save.'); },
    });
  }
}
