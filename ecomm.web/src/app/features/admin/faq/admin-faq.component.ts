import { HttpClient } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { API_BASE_URL } from '../../../core/api.config';
import { ApiResponse } from '../../../core/models/api-response.model';

interface Faq {
  faqId: number; question: string; answer: string;
  category: string | null; displayOrder: number; isPublished: boolean;
}
type Draft = Omit<Faq, 'faqId'> & { faqId: number | null };

@Component({
  selector: 'app-admin-faq',
  imports: [FormsModule],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <div class="flex items-start justify-between gap-4 mb-6">
        <div>
          <h1 class="text-xl font-bold text-slate-900">FAQs</h1>
          <p class="text-sm text-slate-500">Shown on your storefront FAQ page. Clear answers here mean fewer questions in your inbox.</p>
        </div>
        <button type="button" (click)="startNew()" class="btn-primary shrink-0">Add question</button>
      </div>

      @if (draft(); as d) {
        <div class="bg-white border border-primary/30 ring-1 ring-primary/10 rounded-xl p-5 mb-5">
          <h2 class="font-semibold text-slate-800 mb-3">{{ d.faqId ? 'Edit question' : 'New question' }}</h2>
          <div class="space-y-3">
            <div><label class="lbl">Question</label><input [(ngModel)]="d.question" name="q" class="input" /></div>
            <div><label class="lbl">Answer</label><textarea [(ngModel)]="d.answer" name="a" rows="4" class="input"></textarea></div>
            <div class="grid sm:grid-cols-3 gap-3">
              <div><label class="lbl">Category</label><input [(ngModel)]="d.category" name="c" class="input" placeholder="Delivery" /></div>
              <div><label class="lbl">Order</label><input [(ngModel)]="d.displayOrder" name="o" type="number" class="input" /></div>
              <div class="flex items-end pb-2">
                <label class="flex items-center gap-2 text-sm text-slate-700">
                  <input type="checkbox" [(ngModel)]="d.isPublished" name="p" /> Published
                </label>
              </div>
            </div>
          </div>
          @if (error()) { <p class="text-sm text-red-600 mt-2">{{ error() }}</p> }
          <div class="flex gap-2 mt-4">
            <button type="button" (click)="save(d)" [disabled]="busy()" class="btn-primary disabled:opacity-60">
              {{ busy() ? 'Saving…' : 'Save' }}
            </button>
            <button type="button" (click)="draft.set(null)" class="btn-ghost border border-slate-300">Cancel</button>
          </div>
        </div>
      }

      @if (loading()) {
        <p class="text-slate-400 text-sm">Loading…</p>
      } @else if (items().length === 0) {
        <div class="bg-white border border-slate-200 rounded-xl p-8 text-center">
          <div class="text-3xl">❓</div>
          <p class="text-slate-600 font-medium mt-2">No FAQs yet</p>
          <p class="text-slate-400 text-sm mt-1">Add the questions customers ask you most.</p>
        </div>
      } @else {
        <div class="bg-white border border-slate-200 rounded-xl divide-y divide-slate-100">
          @for (f of items(); track f.faqId) {
            <div class="p-4">
              <div class="flex items-start justify-between gap-3">
                <div class="min-w-0">
                  <div class="text-sm font-medium text-slate-800">
                    {{ f.question }}
                    @if (!f.isPublished) {
                      <span class="ml-1.5 text-[10px] px-1.5 py-0.5 rounded-full bg-slate-100 text-slate-500 align-middle">HIDDEN</span>
                    }
                  </div>
                  <div class="text-sm text-slate-600 mt-1 whitespace-pre-line">{{ f.answer }}</div>
                  <div class="text-xs text-slate-400 mt-1">{{ f.category || 'Uncategorised' }} · order {{ f.displayOrder }}</div>
                </div>
                <div class="flex gap-2 shrink-0">
                  <button type="button" (click)="edit(f)" class="text-sm text-primary hover:underline">Edit</button>
                  <button type="button" (click)="remove(f)" class="text-sm text-red-600 hover:underline">Delete</button>
                </div>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AdminFaqComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly base = `${API_BASE_URL}/admin/faq`;

  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly items = signal<Faq[]>([]);
  readonly draft = signal<Draft | null>(null);

  ngOnInit(): void { this.load(); }

  startNew(): void {
    this.error.set(null);
    this.draft.set({
      faqId: null, question: '', answer: '', category: null,
      displayOrder: (this.items().at(-1)?.displayOrder ?? 0) + 1, isPublished: true,
    });
  }

  edit(f: Faq): void {
    this.error.set(null);
    this.draft.set({ ...f });
  }

  save(d: Draft): void {
    if (!d.question.trim() || !d.answer.trim()) {
      this.error.set('A question and an answer are both required.');
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const body = {
      question: d.question, answer: d.answer, category: d.category,
      displayOrder: d.displayOrder, isPublished: d.isPublished,
    };
    const call = d.faqId
      ? this.http.put<ApiResponse<Faq>>(`${this.base}/${d.faqId}`, body)
      : this.http.post<ApiResponse<Faq>>(this.base, body);

    call.subscribe({
      next: () => { this.draft.set(null); this.busy.set(false); this.load(); },
      error: (e) => { this.error.set(e?.error?.message ?? 'Could not save that.'); this.busy.set(false); },
    });
  }

  remove(f: Faq): void {
    this.http.delete<ApiResponse<unknown>>(`${this.base}/${f.faqId}`).subscribe(() => this.load());
  }

  private load(): void {
    this.loading.set(true);
    this.http.get<ApiResponse<Faq[]>>(this.base).subscribe({
      next: (r) => { this.items.set(r.data ?? []); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }
}
