import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { CustomerAdminService } from '../../../core/services/customer-admin.service';

@Component({
  selector: 'app-admin-customer-new',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="max-w-2xl mx-auto p-6">
      <a routerLink="/admin/customers" class="text-sm text-slate-500 hover:text-slate-800">← Customers</a>
      <h1 class="text-xl font-bold text-slate-900 mt-2 mb-1">Add customer</h1>
      <p class="text-sm text-slate-500 mb-5">Create a customer to take a manual/phone order or record a walk-in. Provide at least an email or phone.</p>

      <form (ngSubmit)="save()" class="bg-white border border-slate-200 rounded-xl p-6 space-y-5">
        <div class="grid sm:grid-cols-2 gap-4">
          <div><label class="lbl">Full name</label><input class="input" [(ngModel)]="form.fullName" name="name" /></div>
          <div><label class="lbl">Phone</label><input class="input" [(ngModel)]="form.phoneNumber" name="phone" placeholder="+91…" /></div>
          <div class="sm:col-span-2"><label class="lbl">Email</label><input class="input" type="email" [(ngModel)]="form.email" name="email" placeholder="customer@email.com" /></div>
        </div>

        <div class="border-t border-slate-100 pt-4">
          <h2 class="text-sm font-semibold text-slate-800 mb-2">Marketing consent</h2>
          <label class="flex items-center gap-2 mb-2"><input type="checkbox" [(ngModel)]="form.acceptsEmailMarketing" name="em" /> <span class="text-sm text-slate-700">Email marketing</span></label>
          <label class="flex items-center gap-2 mb-2"><input type="checkbox" [(ngModel)]="form.acceptsSmsMarketing" name="sms" /> <span class="text-sm text-slate-700">SMS marketing</span></label>
          <label class="flex items-center gap-2"><input type="checkbox" [(ngModel)]="form.acceptsWhatsappMarketing" name="wa" /> <span class="text-sm text-slate-700">WhatsApp marketing</span></label>
        </div>

        <div class="border-t border-slate-100 pt-4">
          <label class="lbl">Tags (comma-separated)</label>
          <input class="input mb-3" [(ngModel)]="form.tags" name="tags" placeholder="vip, wholesale" />
          <label class="lbl">Private notes</label>
          <textarea class="input" rows="2" [(ngModel)]="form.notes" name="notes"></textarea>
        </div>

        <div class="flex items-center gap-3">
          <button type="submit" [disabled]="saving()" class="btn-primary px-5 py-2.5">{{ saving() ? 'Saving…' : 'Add customer' }}</button>
          @if (error()) { <span class="text-sm text-red-600">{{ error() }}</span> }
        </div>
      </form>
    </div>
  `,
})
export class AdminCustomerNewComponent {
  private readonly api = inject(CustomerAdminService);
  private readonly router = inject(Router);

  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  form = {
    fullName: '' as string | null, email: '' as string | null, phoneNumber: '' as string | null,
    acceptsEmailMarketing: false, acceptsSmsMarketing: false, acceptsWhatsappMarketing: false,
    notes: '' as string | null, tags: '' as string | null,
  };

  save(): void {
    this.saving.set(true);
    this.error.set(null);
    this.api.create(this.form).subscribe({
      next: (c) => this.router.navigate(['/admin/customers', c.userId]),
      error: (e: unknown) => { this.saving.set(false); this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not add customer.'); },
    });
  }
}
