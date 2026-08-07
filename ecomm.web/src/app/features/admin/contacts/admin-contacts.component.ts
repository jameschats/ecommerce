import { HttpClient } from '@angular/common/http';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse, PagedResult } from '../../../core/models/api-response.model';

interface Contact {
  contactId: number;
  name: string;
  email: string | null;
  phone: string | null;
  subject: string | null;
  message: string | null;
  source: string;
  sourcePage: string | null;
  status: string;
  adminNotes: string | null;
  subscribedToEmails: boolean;
  createdAt: string;
}

interface AdminCustomer {
  userId: number; fullName: string | null; email: string | null; phoneNumber: string | null;
  isActive: boolean; orders: number; totalSpent: number; lastOrderAt: string | null; createdAt: string;
}

interface EmailTemplate { code: string; subject: string | null; body: string | null; }

/** contactId null ⇒ adding one by hand rather than editing an enquiry that came in. */
interface ContactForm {
  contactId: number | null;
  name: string;
  email: string;
  phone: string;
  subject: string;
  message: string;
  subscribeToEmails: boolean;
}

/**
 * The enquiry inbox. Until now the contact form threw submissions away, so this is the
 * first place they have ever been readable.
 *
 * A working list rather than a CRM: read it, reply outside the app, mark it done. Anything
 * more — assignment, threads, SLAs — would be guessing at a workflow nobody has asked for.
 */
@Component({
  selector: 'app-admin-contacts',
  imports: [FormsModule, DatePipe, CurrencyPipe],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">Contacts</h1>
        <div class="flex items-center gap-2">
          @if (view() === 'enquiries') {
            <button type="button" (click)="startCreate()"
                    class="text-sm px-3 py-1.5 rounded-lg bg-slate-900 text-white font-medium hover:bg-slate-800">Add contact</button>
            <label class="text-sm px-3 py-1.5 rounded-lg border border-slate-300 hover:bg-slate-50 cursor-pointer">
              {{ importing() ? 'Importing…' : 'Import' }}
              <input type="file" accept=".csv,.xlsx" hidden (change)="importCsv($event)" />
            </label>
            <button type="button" (click)="exportCsv()" [disabled]="exporting()"
                    class="text-sm px-3 py-1.5 rounded-lg border border-slate-300 hover:bg-slate-50 disabled:opacity-50">
              {{ exporting() ? 'Preparing…' : 'Export CSV' }}
            </button>
          }
        </div>
      </div>
      <p class="text-sm text-slate-500 mb-5">Enquiries from the contact form, and anyone you add by hand.</p>

      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      @if (form(); as f) {
        <div class="mb-4 bg-white border border-slate-200 rounded-xl p-4">
          <h2 class="font-semibold text-slate-900 mb-3">{{ f.contactId ? 'Edit contact' : 'New contact' }}</h2>
          <div class="grid gap-3 sm:grid-cols-2">
            <label class="text-sm">
              <span class="block text-slate-600 mb-1">Name <span class="text-red-500">*</span></span>
              <input [(ngModel)]="f.name" class="input w-full" placeholder="Who got in touch" />
            </label>
            <label class="text-sm">
              <span class="block text-slate-600 mb-1">Subject</span>
              <input [(ngModel)]="f.subject" class="input w-full" placeholder="What it is about" />
            </label>
            <label class="text-sm">
              <span class="block text-slate-600 mb-1">Email</span>
              <input [(ngModel)]="f.email" type="email" class="input w-full" placeholder="name@example.com" />
            </label>
            <label class="text-sm">
              <span class="block text-slate-600 mb-1">Phone</span>
              <input [(ngModel)]="f.phone" class="input w-full" placeholder="10-digit mobile" />
            </label>
          </div>
          <label class="text-sm block mt-3">
            <span class="block text-slate-600 mb-1">Message</span>
            <textarea [(ngModel)]="f.message" rows="3" class="input w-full" placeholder="What they said"></textarea>
          </label>
          <label class="flex items-center gap-2 text-sm text-slate-600 mt-3">
            <input type="checkbox" [(ngModel)]="f.subscribeToEmails" class="w-4 h-4" />
            They agreed to receive emails
          </label>
          <p class="text-xs text-slate-400 mt-1">Only tick this if they actually said yes — campaigns send to opted-in contacts only.</p>
          <div class="flex items-center gap-2 mt-4">
            <button type="button" (click)="save()" [disabled]="saving()"
                    class="px-4 py-2 rounded-lg bg-slate-900 text-white text-sm font-medium disabled:opacity-50">
              {{ saving() ? 'Saving…' : 'Save' }}
            </button>
            <button type="button" (click)="form.set(null)" class="px-4 py-2 rounded-lg border border-slate-300 text-sm">Cancel</button>
          </div>
        </div>
      }

      <!-- Two different lists that people conflate: someone who wrote in, and someone who
           bought. Tabs rather than one merged list, because the useful columns differ. -->
      <div class="flex gap-1 p-1 mb-4 bg-slate-100 rounded-lg text-sm font-medium w-fit">
        @for (v of views; track v.key) {
          <button type="button" (click)="setView(v.key)" class="px-4 py-2 rounded-md transition"
                  [class]="view() === v.key ? 'bg-white shadow-sm text-slate-900' : 'text-slate-500'">{{ v.label }}</button>
        }
      </div>

      @if (view() === 'customers') {
        <div class="flex flex-wrap items-center gap-2 mb-4">
          <label class="flex items-center gap-2 text-sm text-slate-600">
            <input type="checkbox" [(ngModel)]="withOrders" (ngModelChange)="loadCustomers()" class="w-4 h-4" />
            only those who have ordered
          </label>
          <input [(ngModel)]="customerSearch" (keyup.enter)="loadCustomers()"
                 placeholder="Search name, email, phone…" class="input max-w-xs ml-auto" />
        </div>

        @if (loadingCustomers()) {
          <div class="p-10 text-center text-slate-400">Loading…</div>
        } @else if (!customers().length) {
          <div class="bg-white border border-slate-200 rounded-xl p-12 text-center text-slate-500">No customers found.</div>
        } @else {
          <div class="bg-white border border-slate-200 rounded-xl overflow-x-auto">
            <table class="w-full text-sm">
              <thead class="text-left text-slate-400 border-b border-slate-100">
                <tr>
                  <th class="px-4 py-2">Customer</th>
                  <th class="px-2 py-2 text-right">Orders</th>
                  <th class="px-2 py-2 text-right">Spent</th>
                  <th class="px-2 py-2">Last order</th>
                </tr>
              </thead>
              <tbody>
                @for (u of customers(); track u.userId) {
                  <tr class="border-b border-slate-50" [class.opacity-60]="!u.isActive">
                    <td class="px-4 py-2">
                      <div class="text-slate-800">
                        {{ u.fullName || 'Unnamed' }}
                        @if (!u.isActive) { <span class="ml-1 text-[10px] text-amber-700 bg-amber-50 border border-amber-200 rounded px-1">switched off</span> }
                      </div>
                      <div class="text-xs text-slate-500">
                        @if (u.email) { <a [href]="'mailto:' + u.email" class="text-primary hover:underline">{{ u.email }}</a> }
                        @if (u.email && u.phoneNumber) { <span class="mx-1 text-slate-300">·</span> }
                        @if (u.phoneNumber) { <a [href]="'tel:' + u.phoneNumber" class="text-primary hover:underline">{{ u.phoneNumber }}</a> }
                      </div>
                    </td>
                    <td class="px-2 py-2 text-right">{{ u.orders }}</td>
                    <td class="px-2 py-2 text-right">{{ u.totalSpent | currency:'INR':'symbol':'1.0-0' }}</td>
                    <td class="px-2 py-2 text-slate-500">{{ u.lastOrderAt ? (u.lastOrderAt | date: 'dd MMM yyyy') : '—' }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <p class="text-xs text-slate-400 mt-2">
            Spend counts settled orders only — a pending order is not money taken.
          </p>
        }
      } @else {

      <div class="flex flex-wrap items-center gap-2 mb-4">
        @for (s of statuses; track s.key) {
          <button type="button" (click)="setStatus(s.key)"
                  class="px-3 py-1.5 rounded-lg text-sm border"
                  [class]="status() === s.key ? 'bg-blue-600 text-white border-blue-600' : 'bg-white text-slate-600 border-slate-200 hover:bg-slate-50'">
            {{ s.label }}
          </button>
        }
        <input [(ngModel)]="search" (keyup.enter)="load()" placeholder="Search name, email, message…"
               class="input max-w-xs ml-auto" />
      </div>

      @if (loading()) {
        <div class="p-10 text-center text-slate-400">Loading…</div>
      } @else if (!rows().length) {
        <div class="bg-white border border-slate-200 rounded-xl p-12 text-center text-slate-500">
          No enquiries here yet.
        </div>
      } @else {
        <div class="space-y-3">
          @for (c of rows(); track c.contactId) {
            <div class="bg-white border rounded-xl p-4"
                 [class]="c.status === 'New' ? 'border-blue-200' : 'border-slate-200'">
              <div class="flex flex-wrap items-start gap-x-3 gap-y-1">
                <span class="font-semibold text-slate-900">{{ c.name }}</span>
                @if (c.email) { <a [href]="'mailto:' + c.email" class="text-sm text-primary hover:underline">{{ c.email }}</a> }
                @if (c.phone) { <a [href]="'tel:' + c.phone" class="text-sm text-primary hover:underline">{{ c.phone }}</a> }
                @if (c.subscribedToEmails) {
                  <span class="text-[11px] bg-emerald-50 text-emerald-700 border border-emerald-200 rounded px-1.5 py-0.5">opted in</span>
                }
                <span class="text-xs text-slate-400 ml-auto">{{ c.createdAt | date: 'dd MMM yyyy, HH:mm' }}</span>
                <span class="flex items-center gap-1.5 text-xs">
                  @if (c.email) {
                    <button type="button" (click)="startEmail(c)" class="text-blue-600 hover:underline">Email</button>
                    <span class="text-slate-300">·</span>
                  }
                  <button type="button" (click)="startEdit(c)" class="text-blue-600 hover:underline">Edit</button>
                  <span class="text-slate-300">·</span>
                  <button type="button" (click)="remove(c)" class="text-red-600 hover:underline">Delete</button>
                </span>
              </div>

              @if (c.subject) { <p class="text-sm font-medium text-slate-700 mt-2">{{ c.subject }}</p> }
              <p class="text-sm text-slate-600 mt-1 whitespace-pre-wrap">{{ c.message }}</p>

              <div class="flex flex-wrap items-center gap-2 mt-3 pt-3 border-t border-slate-100">
                <select [ngModel]="c.status" (ngModelChange)="setRowStatus(c, $event)"
                        class="h-8 rounded-lg border border-slate-300 text-sm px-2 bg-white">
                  @for (s of editableStatuses; track s) { <option [value]="s">{{ s }}</option> }
                </select>
                <input [ngModel]="c.adminNotes" (ngModelChange)="c.adminNotes = $event"
                       (blur)="saveNotes(c)" placeholder="Internal note…"
                       class="input h-8 flex-1 min-w-[200px]" />
                @if (savedId() === c.contactId) { <span class="text-xs text-emerald-600">Saved</span> }
              </div>
            </div>
          }
        </div>

        @if (totalPages() > 1) {
          <div class="flex justify-center gap-1 mt-6">
            @for (p of pages(); track p) {
              <button type="button" (click)="goTo(p)" class="w-9 h-9 rounded-lg text-sm border"
                      [class]="p === page() ? 'bg-blue-600 text-white border-blue-600' : 'bg-white text-slate-600 border-slate-300'">{{ p }}</button>
            }
          </div>
        }
      }
      }

      <!-- Reply to one person. Separate from campaigns on purpose: answering someone who
           wrote in is not marketing, and does not wait on a marketing opt-in. -->
      @if (emailTo(); as t) {
        <div class="fixed inset-0 bg-black/30 flex items-start justify-center p-6 overflow-y-auto z-50" (click)="emailTo.set(null)">
          <div class="bg-white rounded-xl border border-slate-200 p-5 w-full max-w-lg mt-10" (click)="$event.stopPropagation()">
            <h2 class="font-semibold text-slate-900">Email {{ t.name }}</h2>
            <p class="text-xs text-slate-500 mb-4">{{ t.email }}</p>

            @if (templates().length) {
              <label class="text-sm block mb-3">
                <span class="block text-slate-600 mb-1">Start from a template</span>
                <select (change)="applyTemplate($any($event.target).value)"
                        class="h-9 w-full rounded-lg border border-slate-300 text-sm px-2 bg-white">
                  <option value="">— write my own —</option>
                  @for (tpl of templates(); track tpl.code) { <option [value]="tpl.code">{{ tpl.code }}</option> }
                </select>
              </label>
            }

            <label class="text-sm block mb-3">
              <span class="block text-slate-600 mb-1">Subject</span>
              <input [(ngModel)]="emailSubject" class="input w-full" />
            </label>
            <label class="text-sm block">
              <span class="block text-slate-600 mb-1">Message</span>
              <textarea [(ngModel)]="emailBody" rows="8" class="input w-full"></textarea>
            </label>
            <p class="text-[11px] text-slate-400 mt-1">Basic HTML is allowed.</p>

            @if (emailError()) { <p class="text-sm text-red-600 mt-2">{{ emailError() }}</p> }

            <div class="flex items-center gap-2 mt-4">
              <button type="button" (click)="sendEmail()" [disabled]="sendingEmail()"
                      class="px-4 py-2 rounded-lg bg-slate-900 text-white text-sm font-medium disabled:opacity-50">
                {{ sendingEmail() ? 'Sending…' : 'Send' }}
              </button>
              <button type="button" (click)="emailTo.set(null)" class="px-4 py-2 rounded-lg border border-slate-300 text-sm">Cancel</button>
            </div>
          </div>
        </div>
      }
    </div>
  `,
})
export class AdminContactsComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/contacts`;

  readonly statuses = [
    { key: '', label: 'All' },
    { key: 'New', label: 'New' },
    { key: 'Open', label: 'Open' },
    { key: 'Closed', label: 'Closed' },
    { key: 'Spam', label: 'Spam' },
  ];
  readonly editableStatuses = ['New', 'Open', 'Closed', 'Spam'];

  readonly rows = signal<Contact[]>([]);
  readonly loading = signal(true);
  readonly status = signal('');
  readonly page = signal(1);
  readonly totalPages = signal(1);
  readonly savedId = signal<number | null>(null);
  readonly form = signal<ContactForm | null>(null);
  readonly saving = signal(false);
  readonly exporting = signal(false);
  readonly importing = signal(false);
  readonly error = signal<string | null>(null);
  search = '';

  readonly views = [
    { key: 'enquiries' as const, label: 'Website enquiries' },
    { key: 'customers' as const, label: 'Customers' },
  ];
  readonly view = signal<'enquiries' | 'customers'>('enquiries');

  readonly customers = signal<AdminCustomer[]>([]);
  readonly loadingCustomers = signal(false);
  customerSearch = '';
  withOrders = false;

  readonly message = signal<string | null>(null);
  readonly emailTo = signal<Contact | null>(null);
  readonly templates = signal<EmailTemplate[]>([]);
  readonly sendingEmail = signal(false);
  readonly emailError = signal<string | null>(null);
  emailSubject = '';
  emailBody = '';

  readonly pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  /**
   * Fetched through HttpClient rather than followed as a link.
   *
   * This used to be an `<a href>` straight at the API, which never worked: the bearer token is
   * attached by an HTTP interceptor, and a browser navigation does not pass through it. The
   * endpoint answered 401 every time. Every other export in the admin already does it this way.
   *
   * The filter on screen is carried through, so what downloads is what you were looking at.
   */
  exportCsv(): void {
    this.exporting.set(true);
    this.error.set(null);

    const url = `${this.base}/export?status=${encodeURIComponent(this.status())}`
      + `&search=${encodeURIComponent(this.search)}`;

    this.http.get(url, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        this.exporting.set(false);
        const href = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = href;
        a.download = `contacts-${new Date().toISOString().slice(0, 10)}.csv`;
        a.click();
        URL.revokeObjectURL(href);
      },
      error: () => {
        this.exporting.set(false);
        this.error.set('Could not download the CSV.');
      },
    });
  }

  ngOnInit(): void { this.load(); }

  setStatus(s: string): void { this.status.set(s); this.page.set(1); this.load(); }
  goTo(p: number): void { this.page.set(p); this.load(); }

  load(): void {
    this.loading.set(true);
    const url = `${this.base}?status=${encodeURIComponent(this.status())}`
      + `&search=${encodeURIComponent(this.search)}&page=${this.page()}&pageSize=25`;
    this.http.get<ApiResponse<PagedResult<Contact>>>(url).subscribe({
      next: (r) => {
        this.rows.set(r.data?.items ?? []);
        this.totalPages.set(r.data?.totalPages ?? 1);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  setRowStatus(c: Contact, status: string): void {
    c.status = status;
    this.patchRow(c);
  }

  saveNotes(c: Contact): void { this.patchRow(c); }

  /** The inline status/notes patch. Sends only those fields, so it cannot blank the details. */
  private patchRow(c: Contact): void {
    this.error.set(null);
    this.http.put<ApiResponse<Contact>>(`${this.base}/${c.contactId}`, {
      status: c.status,
      adminNotes: c.adminNotes,
      subscribedToEmails: c.subscribedToEmails,
    }).subscribe({
      next: () => {
        this.savedId.set(c.contactId);
        setTimeout(() => this.savedId.set(null), 1500);
        // Re-filtering on a status change would make the row vanish under the cursor,
        // which reads as data loss rather than as a filter doing its job.
      },
      error: (e) => this.error.set(e?.error?.message ?? 'Could not save that change.'),
    });
  }

  // --- Add / edit / delete ---

  startCreate(): void {
    this.error.set(null);
    this.form.set({ contactId: null, name: '', email: '', phone: '', subject: '', message: '', subscribeToEmails: false });
  }

  startEdit(c: Contact): void {
    this.error.set(null);
    this.form.set({
      contactId: c.contactId, name: c.name, email: c.email ?? '', phone: c.phone ?? '',
      subject: c.subject ?? '', message: c.message ?? '', subscribeToEmails: c.subscribedToEmails,
    });
  }

  save(): void {
    const f = this.form();
    if (!f) return;
    if (!f.name.trim()) { this.error.set('Name is required.'); return; }

    this.saving.set(true);
    this.error.set(null);

    const body = {
      name: f.name, email: f.email, phone: f.phone, subject: f.subject, message: f.message,
      // On create the API names it subscribeToEmails; on update it is subscribedToEmails.
      subscribeToEmails: f.subscribeToEmails, subscribedToEmails: f.subscribeToEmails,
    };
    const req = f.contactId
      ? this.http.put<ApiResponse<Contact>>(`${this.base}/${f.contactId}`, body)
      : this.http.post<ApiResponse<Contact>>(this.base, body);

    req.subscribe({
      next: () => { this.saving.set(false); this.form.set(null); this.load(); },
      error: (e) => { this.saving.set(false); this.error.set(e?.error?.message ?? 'Could not save.'); },
    });
  }

  private flash(msg: string): void {
    this.message.set(msg);
    setTimeout(() => this.message.set(null), 3000);
  }

  // --- Customers tab ---

  setView(v: 'enquiries' | 'customers'): void {
    this.view.set(v);
    this.error.set(null);
    if (v === 'customers' && !this.customers().length) this.loadCustomers();
  }

  loadCustomers(): void {
    this.loadingCustomers.set(true);
    const url = `${API_BASE_URL}/admin/customers?withOrders=${this.withOrders}`
      + `&search=${encodeURIComponent(this.customerSearch)}&page=1&pageSize=100`;
    this.http.get<ApiResponse<PagedResult<AdminCustomer>>>(url).subscribe({
      next: (r) => { this.customers.set(r.data?.items ?? []); this.loadingCustomers.set(false); },
      error: () => { this.loadingCustomers.set(false); this.error.set('Could not load customers.'); },
    });
  }

  // --- Reply by email ---

  startEmail(c: Contact): void {
    this.emailError.set(null);
    this.emailSubject = c.subject ? `Re: ${c.subject}` : 'About your enquiry';
    this.emailBody = '';
    this.emailTo.set(c);
    if (!this.templates().length) {
      this.http.get<ApiResponse<EmailTemplate[]>>(`${this.base}/email-templates`)
        .subscribe({ next: (r) => this.templates.set(r.data ?? []), error: () => {} });
    }
  }

  applyTemplate(code: string): void {
    const t = this.templates().find((x) => x.code === code);
    if (!t) return;
    // Only fills what the template actually carries, so picking one never wipes a subject
    // that was already right.
    if (t.subject) this.emailSubject = t.subject;
    if (t.body) this.emailBody = t.body;
  }

  sendEmail(): void {
    const c = this.emailTo();
    if (!c) return;
    if (!this.emailSubject.trim()) { this.emailError.set('Give the email a subject.'); return; }
    if (!this.emailBody.trim()) { this.emailError.set('The message is empty.'); return; }

    this.sendingEmail.set(true);
    this.emailError.set(null);
    this.http.post<ApiResponse<unknown>>(`${this.base}/${c.contactId}/email`,
      { subject: this.emailSubject, body: this.emailBody }).subscribe({
        next: (r) => {
          this.sendingEmail.set(false);
          this.emailTo.set(null);
          this.flash(r.message ?? 'Email sent.');
          this.load();   // sending moves a New enquiry to Open
        },
        error: (e) => {
          this.sendingEmail.set(false);
          this.emailError.set(e?.error?.message ?? 'Could not send the email.');
        },
      });
  }

  // --- Import ---

  importCsv(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;

    this.importing.set(true);
    this.error.set(null);

    const body = new FormData();
    body.append('file', file);
    this.http.post<ApiResponse<{ added: number; updated: number; skipped: number; errors: string[] }>>(
      `${this.base}/import`, body).subscribe({
        next: (r) => {
          this.importing.set(false);
          const d = r.data;
          this.flash(r.message ?? 'Imported.');
          if (d?.errors?.length) this.error.set(`Some rows were skipped: ${d.errors.slice(0, 5).join(' ')}`);
          this.load();
        },
        error: (e) => {
          this.importing.set(false);
          this.error.set(e?.error?.message ?? 'Could not import that file.');
        },
      });
    input.value = '';   // so re-picking the same file fires change again
  }

  remove(c: Contact): void {
    if (!confirm(`Delete the enquiry from ${c.name}? This cannot be undone.`)) return;

    this.error.set(null);
    this.http.delete<ApiResponse<unknown>>(`${this.base}/${c.contactId}`).subscribe({
      next: () => this.load(),
      error: (e) => this.error.set(e?.error?.message ?? 'Could not delete that contact.'),
    });
  }
}
