import { HttpClient } from '@angular/common/http';
import { DatePipe } from '@angular/common';
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
  imports: [FormsModule, DatePipe],
  template: `
    <div class="max-w-5xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">Contacts</h1>
        <div class="flex items-center gap-2">
          <button type="button" (click)="startCreate()"
                  class="text-sm px-3 py-1.5 rounded-lg bg-slate-900 text-white font-medium hover:bg-slate-800">Add contact</button>
          <a [href]="exportUrl()" class="text-sm px-3 py-1.5 rounded-lg border border-slate-300 hover:bg-slate-50">CSV</a>
        </div>
      </div>
      <p class="text-sm text-slate-500 mb-5">Enquiries from the contact form, and anyone you add by hand.</p>

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
  readonly error = signal<string | null>(null);
  search = '';

  readonly pages = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  /** Export follows the filter on screen, so what downloads is what you were looking at. */
  readonly exportUrl = computed(() =>
    `${this.base}/export?status=${encodeURIComponent(this.status())}&search=${encodeURIComponent(this.search)}`);

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

  remove(c: Contact): void {
    if (!confirm(`Delete the enquiry from ${c.name}? This cannot be undone.`)) return;

    this.error.set(null);
    this.http.delete<ApiResponse<unknown>>(`${this.base}/${c.contactId}`).subscribe({
      next: () => this.load(),
      error: (e) => this.error.set(e?.error?.message ?? 'Could not delete that contact.'),
    });
  }
}
