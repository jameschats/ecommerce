import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ColorSwatch, SaveColorSwatchRequest } from '../../../core/models/color-swatch.model';
import { ColorSwatchService } from '../../../core/services/color-swatch.service';

@Component({
  selector: 'app-admin-color-swatches',
  imports: [FormsModule],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <div class="flex items-center justify-between mb-1">
        <h1 class="text-xl font-bold text-slate-900">Colour swatches</h1>
        <button type="button" (click)="startNew()" class="btn-primary">+ New colour</button>
      </div>
      <p class="text-sm text-slate-500 mb-4">
        Maps a variant colour name (e.g. "Rose Gold") to a hex code, so product cards can show an accurate
        swatch dot. Names match variant option values case-insensitively; unmapped colours show a neutral dot.
      </p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      @if (editing()) {
        <div class="bg-white border border-slate-200 rounded-xl p-4 mb-5">
          <h2 class="font-semibold text-slate-800 mb-3">{{ form.colorSwatchId ? 'Edit colour' : 'New colour' }}</h2>
          <div class="grid sm:grid-cols-[1fr_auto] gap-3 items-end">
            <label class="block"><span class="lbl">Name *</span><input [(ngModel)]="form.name" name="name" placeholder="e.g. Rose Gold" class="input w-full" /></label>
            <label class="block">
              <span class="lbl">Hex code *</span>
              <div class="flex items-center gap-2">
                <input type="color" [(ngModel)]="form.hexCode" name="hexColor" class="h-9 w-10 rounded border border-slate-300 p-0.5" />
                <input [(ngModel)]="form.hexCode" name="hex" placeholder="#RRGGBB" class="input w-28" />
              </div>
            </label>
          </div>
          <div class="flex gap-2 mt-4">
            <button type="button" (click)="save()" [disabled]="saving()" class="btn-primary">{{ saving() ? 'Saving…' : 'Save' }}</button>
            <button type="button" (click)="editing.set(false)" class="px-4 py-2 rounded-lg border border-slate-300 text-sm hover:bg-slate-50">Cancel</button>
          </div>
        </div>
      }

      @if (loading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
      @else if (!swatches().length) { <div class="p-8 text-center text-slate-400">No colours yet.</div> }
      @else {
        <div class="grid sm:grid-cols-2 gap-2">
          @for (c of swatches(); track c.colorSwatchId) {
            <div class="flex items-center justify-between border border-slate-200 rounded-lg px-3 py-2">
              <div class="flex items-center gap-2">
                <span class="h-5 w-5 rounded-full border border-slate-300 shrink-0" [style.background-color]="c.hexCode"></span>
                <span class="text-sm text-slate-800">{{ c.name }}</span>
                <span class="text-xs text-slate-400">{{ c.hexCode }}</span>
              </div>
              <div class="whitespace-nowrap">
                <button type="button" (click)="edit(c)" class="text-blue-600 hover:underline text-xs mr-3">Edit</button>
                <button type="button" (click)="remove(c)" class="text-red-500 hover:underline text-xs">Delete</button>
              </div>
            </div>
          }
        </div>
      }
    </div>
  `,
})
export class AdminColorSwatchesComponent implements OnInit {
  private readonly svc = inject(ColorSwatchService);

  readonly swatches = signal<ColorSwatch[]>([]);
  readonly loading = signal(true);
  readonly editing = signal(false);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  form: SaveColorSwatchRequest & { colorSwatchId?: number } = this.blank();

  ngOnInit(): void { this.load(); }

  private blank(): SaveColorSwatchRequest & { colorSwatchId?: number } {
    return { name: '', hexCode: '#000000' };
  }

  private load(): void {
    this.loading.set(true);
    this.svc.adminList().subscribe({ next: (s) => { this.swatches.set(s); this.loading.set(false); }, error: () => this.loading.set(false) });
  }

  startNew(): void { this.form = this.blank(); this.editing.set(true); this.message.set(null); this.error.set(null); }
  edit(c: ColorSwatch): void { this.form = { ...c }; this.editing.set(true); this.message.set(null); this.error.set(null); }

  save(): void {
    this.saving.set(true); this.error.set(null);
    const body: SaveColorSwatchRequest = { name: this.form.name, hexCode: this.form.hexCode };
    const req = this.form.colorSwatchId ? this.svc.update(this.form.colorSwatchId, body) : this.svc.create(body);
    req.subscribe({
      next: () => { this.saving.set(false); this.editing.set(false); this.message.set('Colour saved.'); this.load(); },
      error: (e) => { this.saving.set(false); this.error.set(e?.error?.message ?? 'Save failed.'); },
    });
  }

  remove(c: ColorSwatch): void {
    if (!confirm(`Delete colour "${c.name}"?`)) return;
    this.svc.remove(c.colorSwatchId).subscribe({ next: () => { this.message.set('Colour deleted.'); this.load(); } });
  }
}
