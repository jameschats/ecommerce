import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Address, SaveAddressRequest } from '../../core/models/account.model';
import { AccountService } from '../../core/services/account.service';

@Component({
  selector: 'app-addresses',
  imports: [FormsModule],
  template: `
    <div class="space-y-4">
      <div class="flex items-center justify-between">
        <h2 class="font-semibold text-slate-800">Saved addresses</h2>
        @if (!showForm()) {
          <button type="button" (click)="addNew()" class="btn-primary px-4 py-2 text-sm">+ Add address</button>
        }
      </div>

      @if (showForm()) {
        <form (ngSubmit)="save()" class="bg-white rounded-xl border border-slate-200 p-5 grid sm:grid-cols-2 gap-4">
          <div class="sm:col-span-2 font-medium text-slate-700">{{ editId() ? 'Edit address' : 'New address' }}</div>
          <div><label class="lbl">Label</label><input class="input" [(ngModel)]="form.label" name="label" placeholder="Home / Work" /></div>
          <div><label class="lbl">Recipient name</label><input class="input" [(ngModel)]="form.recipientName" name="recipientName" /></div>
          <div><label class="lbl">Phone</label><input class="input" [(ngModel)]="form.phone" name="phone" /></div>
          <div>
            <label class="lbl">Address type</label>
            <select class="input" [(ngModel)]="form.addressType" name="addressType">
              <option value="Both">Billing &amp; Shipping</option>
              <option value="Shipping">Shipping only</option>
              <option value="Billing">Billing only</option>
            </select>
          </div>
          <div class="sm:col-span-2"><label class="lbl">Address line 1 *</label><input class="input" [(ngModel)]="form.line1" name="line1" required /></div>
          <div class="sm:col-span-2"><label class="lbl">Address line 2</label><input class="input" [(ngModel)]="form.line2" name="line2" /></div>
          <div><label class="lbl">City *</label><input class="input" [(ngModel)]="form.city" name="city" required /></div>
          <div><label class="lbl">State *</label><input class="input" [(ngModel)]="form.state" name="state" required /></div>
          <div><label class="lbl">Pincode *</label><input class="input" [(ngModel)]="form.pincode" name="pincode" required /></div>
          <div><label class="lbl">Country</label><input class="input" [(ngModel)]="form.country" name="country" /></div>
          <label class="sm:col-span-2 flex items-center gap-2 text-sm text-slate-600"><input type="checkbox" [(ngModel)]="form.isDefault" name="isDefault" /> Set as default address</label>
          @if (error(); as e) { <p class="sm:col-span-2 text-sm text-red-600">{{ e }}</p> }
          <div class="sm:col-span-2 flex gap-3">
            <button type="submit" [disabled]="saving()" class="btn-primary px-5 py-2.5">{{ saving() ? 'Saving…' : 'Save address' }}</button>
            <button type="button" (click)="cancel()" class="px-5 py-2.5 border border-slate-300 rounded-lg text-slate-700 hover:bg-slate-50">Cancel</button>
          </div>
        </form>
      }

      @if (loading()) {
        <p class="text-slate-400 text-sm">Loading…</p>
      } @else if (addresses().length === 0 && !showForm()) {
        <div class="bg-white rounded-xl border border-slate-200 p-10 text-center text-slate-500">No addresses yet. Add one to speed up checkout.</div>
      } @else {
        <div class="grid sm:grid-cols-2 gap-4">
          @for (a of addresses(); track a.customerAddressId) {
            <div class="bg-white rounded-xl border p-4" [class.border-primary]="a.isDefault" [class.border-slate-200]="!a.isDefault">
              <div class="flex items-center gap-2 mb-1">
                <span class="font-medium text-slate-800">{{ a.label || a.addressType }}</span>
                @if (a.isDefault) { <span class="text-[10px] uppercase tracking-wide bg-primary/10 text-primary px-2 py-0.5 rounded-full">Default</span> }
              </div>
              @if (a.recipientName) { <div class="text-sm text-slate-700">{{ a.recipientName }} @if (a.phone) { · {{ a.phone }} }</div> }
              <div class="text-sm text-slate-500 mt-0.5">{{ a.line1 }}@if (a.line2) {, {{ a.line2 }}}</div>
              <div class="text-sm text-slate-500">{{ a.city }}, {{ a.state }} {{ a.pincode }}</div>
              <div class="text-sm text-slate-400">{{ a.country }}</div>
              <div class="flex gap-3 mt-3 text-sm">
                <button type="button" (click)="edit(a)" class="text-primary hover:underline">Edit</button>
                @if (!a.isDefault) {
                  <button type="button" (click)="makeDefault(a)" class="text-slate-500 hover:text-slate-800">Set default</button>
                  <button type="button" (click)="del(a)" class="text-slate-500 hover:text-red-600">Delete</button>
                }
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AddressesComponent implements OnInit {
  private readonly account = inject(AccountService);

  readonly addresses = signal<Address[]>([]);
  readonly loading = signal(true);
  readonly showForm = signal(false);
  readonly saving = signal(false);
  readonly editId = signal<number | null>(null);
  readonly error = signal<string | null>(null);

  form: SaveAddressRequest = this.empty();

  ngOnInit(): void { this.load(); }

  private load(): void {
    this.loading.set(true);
    this.account.listAddresses().subscribe({
      next: (a) => { this.addresses.set(a); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  private empty(): SaveAddressRequest {
    return { label: '', recipientName: '', phone: '', line1: '', line2: '', city: '', state: '', pincode: '', country: 'India', addressType: 'Both', isDefault: false };
  }

  addNew(): void { this.editId.set(null); this.form = this.empty(); this.error.set(null); this.showForm.set(true); }

  edit(a: Address): void {
    this.editId.set(a.customerAddressId);
    this.form = { label: a.label ?? '', recipientName: a.recipientName ?? '', phone: a.phone ?? '', line1: a.line1, line2: a.line2 ?? '', city: a.city, state: a.state, pincode: a.pincode, country: a.country, addressType: a.addressType, isDefault: a.isDefault };
    this.error.set(null);
    this.showForm.set(true);
  }

  cancel(): void { this.showForm.set(false); this.error.set(null); }

  save(): void {
    this.saving.set(true);
    this.error.set(null);
    const id = this.editId();
    const req = id ? this.account.updateAddress(id, this.form) : this.account.createAddress(this.form);
    req.subscribe({
      next: () => { this.saving.set(false); this.showForm.set(false); this.load(); },
      error: (e: unknown) => {
        this.saving.set(false);
        this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not save the address.');
      },
    });
  }

  makeDefault(a: Address): void {
    this.account.setDefaultAddress(a.customerAddressId).subscribe(() => this.load());
  }

  del(a: Address): void {
    this.account.deleteAddress(a.customerAddressId).subscribe(() => this.load());
  }
}
