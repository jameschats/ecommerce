import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SaveSupplierRequest, Supplier } from '../../../core/models/supplier.model';
import { SupplierService } from '../../../core/services/supplier.service';

@Component({
  selector: 'app-admin-suppliers',
  imports: [FormsModule],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">Suppliers</h1>
        <button type="button" (click)="startNew()" class="btn-primary">+ New supplier</button>
      </div>
      <p class="text-sm text-slate-500 mb-4">Vendors you source products from. Assign a primary supplier + cost to products for profit-by-supplier reports.</p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      @if (editing()) {
        <div class="bg-white border border-slate-200 rounded-xl p-4 mb-5">
          <h2 class="font-semibold text-slate-800 mb-3">{{ form.supplierId ? 'Edit supplier' : 'New supplier' }}</h2>
          <div class="grid sm:grid-cols-2 gap-3">
            <label class="block"><span class="lbl">Name *</span><input [(ngModel)]="form.name" name="name" class="input w-full" /></label>
            <label class="block"><span class="lbl">Code</span><input [(ngModel)]="form.code" name="code" class="input w-full" /></label>
            <label class="block"><span class="lbl">Contact person</span><input [(ngModel)]="form.contactName" name="contact" class="input w-full" /></label>
            <label class="block"><span class="lbl">Email</span><input [(ngModel)]="form.email" name="email" class="input w-full" /></label>
            <label class="block"><span class="lbl">Phone</span><input [(ngModel)]="form.phone" name="phone" class="input w-full" /></label>
            <label class="block"><span class="lbl">GSTIN</span><input [(ngModel)]="form.gstin" name="gstin" class="input w-full" /></label>
            <label class="block"><span class="lbl">City</span><input [(ngModel)]="form.city" name="city" class="input w-full" /></label>
            <label class="block"><span class="lbl">State</span><input [(ngModel)]="form.state" name="state" class="input w-full" /></label>
            <label class="block"><span class="lbl">Payment terms</span><input [(ngModel)]="form.paymentTerms" name="terms" placeholder="e.g. Net 30" class="input w-full" /></label>
            <label class="block"><span class="lbl">Lead time (days)</span><input type="number" [(ngModel)]="form.leadTimeDays" name="lead" class="input w-full" /></label>
          </div>
          <label class="flex items-center gap-2 text-sm text-slate-600 mt-3"><input type="checkbox" [(ngModel)]="form.isActive" name="active" /> Active</label>
          <div class="flex gap-2 mt-4">
            <button type="button" (click)="save()" [disabled]="saving()" class="btn-primary">{{ saving() ? 'Saving…' : 'Save' }}</button>
            <button type="button" (click)="editing.set(false)" class="px-4 py-2 rounded-lg border border-slate-300 text-sm hover:bg-slate-50">Cancel</button>
          </div>
        </div>
      }

      @if (loading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
      @else if (!suppliers().length) { <div class="p-8 text-center text-slate-400">No suppliers yet.</div> }
      @else {
        <div class="overflow-x-auto">
          <table class="w-full text-sm">
            <thead class="text-left text-slate-400 border-b border-slate-200"><tr><th class="py-2">Name</th><th>Contact</th><th>City</th><th>Lead time</th><th>Status</th><th></th></tr></thead>
            <tbody>
              @for (s of suppliers(); track s.supplierId) {
                <tr class="border-b border-slate-100">
                  <td class="py-2 font-medium text-slate-800">{{ s.name }}<div class="text-xs text-slate-400 font-normal">{{ s.code }}</div></td>
                  <td class="text-slate-600">{{ s.contactName }}<div class="text-xs text-slate-400">{{ s.phone || s.email }}</div></td>
                  <td>{{ s.city }}{{ s.state ? ', ' + s.state : '' }}</td>
                  <td>{{ s.leadTimeDays ? s.leadTimeDays + 'd' : '—' }}</td>
                  <td><span class="text-xs px-1.5 py-0.5 rounded" [class]="s.isActive ? 'bg-green-50 text-green-700 border border-green-200' : 'bg-slate-100 text-slate-500'">{{ s.isActive ? 'Active' : 'Off' }}</span></td>
                  <td class="text-right whitespace-nowrap"><button type="button" (click)="edit(s)" class="text-blue-600 hover:underline text-xs mr-3">Edit</button><button type="button" (click)="remove(s)" class="text-red-500 hover:underline text-xs">Delete</button></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      }
    </div>
  `,
})
export class AdminSuppliersComponent implements OnInit {
  private readonly svc = inject(SupplierService);

  readonly suppliers = signal<Supplier[]>([]);
  readonly loading = signal(true);
  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  form: SaveSupplierRequest & { supplierId?: number } = this.blank();

  ngOnInit(): void { this.load(); }

  private blank(): SaveSupplierRequest & { supplierId?: number } {
    return { name: '', code: null, contactName: null, email: null, phone: null, gstin: null,
      addressLine1: null, city: null, state: null, pincode: null, paymentTerms: null, leadTimeDays: null, notes: null, isActive: true };
  }

  private load(): void {
    this.loading.set(true);
    this.svc.list().subscribe({ next: (s) => { this.suppliers.set(s); this.loading.set(false); }, error: () => this.loading.set(false) });
  }

  startNew(): void { this.form = this.blank(); this.editing.set(true); this.message.set(null); this.error.set(null); }
  edit(s: Supplier): void { this.form = { ...this.blank(), ...s }; this.editing.set(true); this.message.set(null); this.error.set(null); }

  save(): void {
    this.saving.set(true); this.error.set(null);
    const body: SaveSupplierRequest = { ...this.form };
    const req = this.form.supplierId ? this.svc.update(this.form.supplierId, body) : this.svc.create(body);
    req.subscribe({
      next: () => { this.saving.set(false); this.editing.set(false); this.message.set('Supplier saved.'); this.load(); },
      error: (e) => { this.saving.set(false); this.error.set(e?.error?.message ?? 'Save failed.'); },
    });
  }

  remove(s: Supplier): void {
    if (!confirm(`Delete supplier "${s.name}"? This also removes it from any products.`)) return;
    this.svc.remove(s.supplierId).subscribe({ next: () => { this.message.set('Supplier deleted.'); this.load(); } });
  }
}
