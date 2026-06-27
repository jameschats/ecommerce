import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AttributeDef, SaveAttributeRequest } from '../../../core/models/admin-catalog.model';
import { AdminCatalogService } from '../../../core/services/admin-catalog.service';

@Component({
  selector: 'app-admin-attributes',
  imports: [FormsModule],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Attributes</h1>
      <p class="text-sm text-slate-500 mb-4">Reusable product specs (e.g. Warranty, Voltage). Add fixed values or leave free-text.</p>
      @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

      <!-- New attribute -->
      <div class="bg-white border border-slate-200 rounded-xl p-4 mb-6 flex flex-wrap items-end gap-3">
        <div class="flex-1 min-w-[160px]">
          <label class="block text-xs text-slate-500 mb-1">Name</label>
          <input [(ngModel)]="form.name" placeholder="Warranty" class="input" />
        </div>
        <div>
          <label class="block text-xs text-slate-500 mb-1">Type</label>
          <select [(ngModel)]="form.dataType" class="input">
            <option value="string">Text</option><option value="int">Number</option>
            <option value="decimal">Decimal</option><option value="bool">Yes/No</option>
          </select>
        </div>
        <label class="flex items-center gap-2 text-sm text-slate-600 pb-2"><input type="checkbox" [(ngModel)]="form.isFilterable" /> Filterable</label>
        <button type="button" (click)="create()" [disabled]="saving()" class="btn-primary">Add attribute</button>
      </div>

      <!-- List -->
      @if (loading()) { <div class="p-8 text-center text-slate-400">Loading…</div> }
      @else {
        <div class="space-y-3">
          @for (a of attributes(); track a.attributeId) {
            <div class="bg-white border border-slate-200 rounded-xl p-4">
              <div class="flex items-center justify-between">
                <div>
                  <span class="font-medium text-slate-800">{{ a.name }}</span>
                  <span class="text-xs text-slate-400 ml-2">{{ a.code }} · {{ a.dataType }}{{ a.isFilterable ? ' · filterable' : '' }}</span>
                </div>
                <button type="button" (click)="remove(a)" class="text-red-600 hover:underline text-sm">Delete</button>
              </div>
              <div class="mt-3 flex flex-wrap items-center gap-2">
                @for (v of a.values; track v.attributeValueId) {
                  <span class="inline-flex items-center gap-1 bg-slate-100 rounded-full pl-3 pr-1 py-1 text-xs text-slate-600">
                    {{ v.value }}
                    <button type="button" (click)="removeValue(a, v.attributeValueId)" class="w-4 h-4 rounded-full hover:bg-slate-300 text-slate-500">×</button>
                  </span>
                }
                <input [(ngModel)]="valueText[a.attributeId]" (keyup.enter)="addValue(a)" placeholder="+ add value"
                  class="text-xs border border-slate-300 rounded-full px-3 py-1 w-28 focus:outline-none focus:ring-1 focus:ring-blue-500" />
              </div>
            </div>
          }
          @if (attributes().length === 0) { <div class="p-8 text-center text-slate-400">No attributes yet.</div> }
        </div>
      }
    </div>
  `,
})
export class AdminAttributesComponent implements OnInit {
  private readonly api = inject(AdminCatalogService);

  readonly attributes = signal<AttributeDef[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);

  form: SaveAttributeRequest = { name: '', dataType: 'string', isFilterable: false, isActive: true };
  valueText: Record<number, string> = {};

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.api.listAttributes().subscribe({
      next: (a) => { this.attributes.set(a); this.loading.set(false); },
      error: () => this.loading.set(false),
    });
  }

  create(): void {
    if (!this.form.name.trim()) return;
    this.saving.set(true);
    this.error.set(null);
    this.api.createAttribute(this.form).subscribe({
      next: () => { this.saving.set(false); this.form = { name: '', dataType: 'string', isFilterable: false, isActive: true }; this.load(); },
      error: (e) => { this.saving.set(false); this.error.set(e?.error?.message ?? 'Failed.'); },
    });
  }

  remove(a: AttributeDef): void {
    if (!confirm(`Delete attribute "${a.name}"?`)) return;
    this.api.deleteAttribute(a.attributeId).subscribe({
      next: () => this.load(),
      error: (e) => this.error.set(e?.error?.message ?? 'Delete failed.'),
    });
  }

  addValue(a: AttributeDef): void {
    const text = (this.valueText[a.attributeId] ?? '').trim();
    if (!text) return;
    this.api.addAttributeValue(a.attributeId, text).subscribe({
      next: () => { this.valueText[a.attributeId] = ''; this.load(); },
      error: (e) => this.error.set(e?.error?.message ?? 'Failed.'),
    });
  }

  removeValue(a: AttributeDef, valueId: number): void {
    this.api.deleteAttributeValue(a.attributeId, valueId).subscribe({
      next: () => this.load(),
      error: (e) => this.error.set(e?.error?.message ?? 'Failed.'),
    });
  }
}
