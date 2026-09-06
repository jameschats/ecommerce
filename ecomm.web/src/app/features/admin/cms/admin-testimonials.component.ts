import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AdminTestimonial } from '../../../core/models/testimonial.model';
import { TestimonialService } from '../../../core/services/testimonial.service';

@Component({
  selector: 'app-admin-testimonials',
  imports: [FormsModule],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">Testimonials</h1>
        <button type="button" (click)="add()" [disabled]="busy()" class="btn-primary">+ Add testimonial</button>
      </div>
      <p class="text-sm text-slate-500 mb-6">
        Shown in the "What our customers say" section on the home page. Independent of real
        product reviews — add the quotes you want to feature here. Hidden ones aren't shown.
      </p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      @if (loading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
      @else if (!items().length) { <div class="p-8 text-center text-slate-400">No testimonials yet. Click "Add testimonial" to create one.</div> }
      @else {
        <div class="space-y-3">
          @for (t of items(); track t.testimonialId; let i = $index) {
            <div class="bg-white border border-slate-200 rounded-xl p-3 flex gap-3" [class.opacity-60]="!t.isActive">
              <!-- reorder -->
              <div class="flex flex-col justify-center">
                <button type="button" (click)="move(i, -1)" [disabled]="i === 0" class="text-slate-400 hover:text-slate-700 disabled:opacity-30 leading-none">▲</button>
                <button type="button" (click)="move(i, 1)" [disabled]="i === items().length - 1" class="text-slate-400 hover:text-slate-700 disabled:opacity-30 leading-none">▼</button>
              </div>

              <!-- photo -->
              <div class="shrink-0">
                <div class="w-20 h-20 rounded-full overflow-hidden bg-slate-100 grid place-items-center">
                  @if (t.photoUrl) { <img [src]="t.photoUrl" alt="" class="w-full h-full object-cover" /> }
                  @else { <span class="text-xs text-slate-400">No photo</span> }
                </div>
                <label class="mt-1.5 block text-center text-xs text-blue-600 hover:underline cursor-pointer">
                  {{ t.hasUpload ? 'Replace' : 'Upload' }}
                  <input type="file" accept="image/*" class="hidden" (change)="onFile(t, $event)" />
                </label>
              </div>

              <!-- fields -->
              <div class="flex-1 space-y-1.5">
                <div class="flex gap-2">
                  <input [(ngModel)]="t.name" [name]="'nm' + t.testimonialId" placeholder="Customer name" class="input flex-1" />
                  <input [(ngModel)]="t.roleOrCompany" [name]="'ro' + t.testimonialId" placeholder="Role / company (optional)" class="input flex-1" />
                  <select [(ngModel)]="t.rating" [name]="'rt' + t.testimonialId" class="input w-24">
                    @for (r of [5, 4, 3, 2, 1]; track r) { <option [ngValue]="r">{{ r }}★</option> }
                  </select>
                </div>
                <textarea [(ngModel)]="t.quote" [name]="'qt' + t.testimonialId" rows="2" placeholder="Quote" class="input w-full"></textarea>
                <input [(ngModel)]="t.photoUrl" [name]="'im' + t.testimonialId" placeholder="…or paste a photo URL"
                  [disabled]="t.hasUpload" class="input w-full text-xs" />
                <div class="flex items-center justify-between pt-0.5">
                  <label class="flex items-center gap-2 text-sm text-slate-600">
                    <input type="checkbox" [(ngModel)]="t.isActive" [name]="'a' + t.testimonialId" /> Visible
                  </label>
                  <button type="button" (click)="remove(t)" class="text-sm text-red-500 hover:text-red-700">Delete</button>
                </div>
              </div>
            </div>
          }
        </div>
        <button type="button" (click)="save()" [disabled]="busy()" class="btn-primary mt-4">{{ busy() ? 'Saving…' : 'Save changes' }}</button>
      }
    </div>
  `,
})
export class AdminTestimonialsComponent implements OnInit {
  private readonly svc = inject(TestimonialService);

  readonly items = signal<AdminTestimonial[]>([]);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);

  ngOnInit(): void { this.reload(); }

  private reload(): void {
    this.loading.set(true);
    this.svc.listAdmin().subscribe({
      next: (t) => { this.items.set(t); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  add(): void {
    this.busy.set(true);
    this.svc.create({
      name: 'New customer', roleOrCompany: null, quote: 'Great experience — highly recommend!', rating: 5,
      photoUrl: null, displayOrder: this.items().length + 1, isActive: true,
    }).subscribe({
      next: () => { this.busy.set(false); this.flash('Testimonial added.'); this.reload(); },
      error: () => { this.busy.set(false); this.flash('Add failed.'); },
    });
  }

  move(index: number, delta: number): void {
    const arr = [...this.items()];
    const target = index + delta;
    if (target < 0 || target >= arr.length) return;
    [arr[index], arr[target]] = [arr[target], arr[index]];
    this.items.set(arr);
  }

  onFile(t: AdminTestimonial, event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.busy.set(true);
    this.svc.uploadPhoto(t.testimonialId, file).subscribe({
      next: () => { this.busy.set(false); this.flash('Photo uploaded.'); this.reload(); },
      error: () => { this.busy.set(false); this.flash('Upload failed (max 5 MB, image files only).'); },
    });
    input.value = '';
  }

  remove(t: AdminTestimonial): void {
    if (!confirm('Delete this testimonial?')) return;
    this.busy.set(true);
    this.svc.remove(t.testimonialId).subscribe({
      next: () => { this.busy.set(false); this.flash('Testimonial deleted.'); this.reload(); },
      error: () => { this.busy.set(false); this.flash('Delete failed.'); },
    });
  }

  save(): void {
    this.busy.set(true);
    this.message.set(null);
    const updates = this.items().map((t, i) =>
      this.svc.update(t.testimonialId, {
        name: t.name, roleOrCompany: t.roleOrCompany, quote: t.quote, rating: t.rating,
        photoUrl: t.hasUpload ? null : t.photoUrl,
        displayOrder: i + 1, isActive: t.isActive,
      }),
    );
    let done = 0, failed = false;
    updates.forEach((o) => o.subscribe({
      next: () => { if (++done === updates.length) this.finishSave(failed); },
      error: () => { failed = true; if (++done === updates.length) this.finishSave(failed); },
    }));
    if (!updates.length) this.finishSave(false);
  }

  private finishSave(failed: boolean): void {
    this.busy.set(false);
    this.flash(failed ? 'Some testimonials failed to save.' : 'Testimonials saved.');
    this.reload();
  }

  private flash(msg: string): void {
    this.message.set(msg);
  }
}
