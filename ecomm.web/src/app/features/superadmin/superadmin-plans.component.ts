import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SuperAdminService } from '../../core/services/superadmin.service';
import { CreditPack, PlanOption, PackUpsert, PlanUpsert } from '../../core/models/superadmin.model';

/** Manage subscription plans + AI credit packs (both were SQL-only before). */
@Component({
  selector: 'app-superadmin-plans',
  imports: [FormsModule],
  template: `
    <h1 class="text-xl font-bold text-slate-900 mb-1">Plans &amp; credits</h1>
    <p class="text-sm text-slate-500 mb-5">Subscription plans and buyable AI credit packs.</p>

    @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }
    @if (error()) { <div class="mb-4 rounded-lg bg-red-50 border border-red-200 text-red-700 text-sm px-3 py-2">{{ error() }}</div> }

    <!-- Plans -->
    <div class="bg-white border border-slate-200 rounded-xl p-4 mb-6">
      <div class="flex items-center justify-between mb-3">
        <h2 class="font-semibold text-slate-800">Subscription plans</h2>
        <button type="button" (click)="newPlan()" class="text-sm text-primary hover:underline">+ New plan</button>
      </div>
      <table class="w-full text-sm mb-3">
        <thead class="text-left text-slate-400 border-b border-slate-200"><tr><th class="py-1">Name</th><th class="text-right">₹/mo</th><th class="text-right">Products</th><th class="text-right">Orders</th><th class="text-right">AI</th><th></th><th></th></tr></thead>
        <tbody>
          @for (p of plans(); track p.planId) {
            <tr class="border-b border-slate-100">
              <td class="py-2 text-slate-800">{{ p.name }} <span class="text-xs text-slate-400">{{ p.slug }}</span></td>
              <td class="text-right">{{ p.monthlyPrice }}</td>
              <td class="text-right text-slate-500">{{ p.maxProducts ?? '∞' }}</td>
              <td class="text-right text-slate-500">{{ p.maxOrders ?? '∞' }}</td>
              <td class="text-right text-slate-500">{{ p.aiCredits }}</td>
              <td>@if (!p.isActive) { <span class="text-xs text-slate-400">inactive</span> }</td>
              <td class="text-right"><button type="button" (click)="editPlan(p)" class="text-blue-600 text-xs">Edit</button></td>
            </tr>
          }
        </tbody>
      </table>

      @if (planForm(); as f) {
        <div class="border-t border-slate-200 pt-3">
          <div class="text-sm font-medium text-slate-700 mb-2">{{ editingPlanId ? 'Edit plan' : 'New plan' }}</div>
          <div class="grid sm:grid-cols-4 gap-2">
            <label class="block"><span class="lbl">Name</span><input class="input" [(ngModel)]="f.name" /></label>
            <label class="block"><span class="lbl">Slug (optional)</span><input class="input" [(ngModel)]="f.slug" placeholder="auto" /></label>
            <label class="block"><span class="lbl">₹ / month</span><input type="number" class="input" [(ngModel)]="f.monthlyPrice" /></label>
            <label class="block"><span class="lbl">Display order</span><input type="number" class="input" [(ngModel)]="f.displayOrder" /></label>
            <label class="block"><span class="lbl">Max products (blank = ∞)</span><input type="number" class="input" [(ngModel)]="f.maxProducts" /></label>
            <label class="block"><span class="lbl">Max orders/mo (blank = ∞)</span><input type="number" class="input" [(ngModel)]="f.maxOrders" /></label>
            <label class="block"><span class="lbl">AI credits / cycle</span><input type="number" class="input" [(ngModel)]="f.aiCredits" /></label>
            <label class="flex items-center gap-2 mt-5"><input type="checkbox" [(ngModel)]="f.isActive" /> <span class="text-sm text-slate-600">Active</span></label>
          </div>
          <div class="flex gap-2 mt-3">
            <button type="button" (click)="savePlan()" class="btn-primary text-xs">{{ editingPlanId ? 'Save' : 'Create' }}</button>
            <button type="button" (click)="planForm.set(null)" class="px-3 py-1.5 rounded-lg border border-slate-300 text-xs hover:bg-slate-50">Cancel</button>
          </div>
        </div>
      }
    </div>

    <!-- Credit packs -->
    <div class="bg-white border border-slate-200 rounded-xl p-4 max-w-2xl">
      <div class="flex items-center justify-between mb-3">
        <h2 class="font-semibold text-slate-800">AI credit packs</h2>
        <button type="button" (click)="newPack()" class="text-sm text-primary hover:underline">+ New pack</button>
      </div>
      <table class="w-full text-sm mb-3">
        <thead class="text-left text-slate-400 border-b border-slate-200"><tr><th class="py-1">Name</th><th class="text-right">Credits</th><th class="text-right">₹</th><th></th><th></th></tr></thead>
        <tbody>
          @for (p of packs(); track p.aiCreditPackId) {
            <tr class="border-b border-slate-100">
              <td class="py-2 text-slate-800">{{ p.name }}</td>
              <td class="text-right">{{ p.credits }}</td>
              <td class="text-right">{{ p.priceInr }}</td>
              <td>@if (!p.isActive) { <span class="text-xs text-slate-400">inactive</span> }</td>
              <td class="text-right"><button type="button" (click)="editPack(p)" class="text-blue-600 text-xs">Edit</button></td>
            </tr>
          }
        </tbody>
      </table>

      @if (packForm(); as f) {
        <div class="border-t border-slate-200 pt-3">
          <div class="text-sm font-medium text-slate-700 mb-2">{{ editingPackId ? 'Edit pack' : 'New pack' }}</div>
          <div class="grid sm:grid-cols-4 gap-2">
            <label class="block"><span class="lbl">Name</span><input class="input" [(ngModel)]="f.name" /></label>
            <label class="block"><span class="lbl">Credits</span><input type="number" class="input" [(ngModel)]="f.credits" /></label>
            <label class="block"><span class="lbl">₹ price</span><input type="number" class="input" [(ngModel)]="f.priceInr" /></label>
            <label class="block"><span class="lbl">Display order</span><input type="number" class="input" [(ngModel)]="f.displayOrder" /></label>
            <label class="flex items-center gap-2 mt-5"><input type="checkbox" [(ngModel)]="f.isActive" /> <span class="text-sm text-slate-600">Active</span></label>
          </div>
          <div class="flex gap-2 mt-3">
            <button type="button" (click)="savePack()" class="btn-primary text-xs">{{ editingPackId ? 'Save' : 'Create' }}</button>
            <button type="button" (click)="packForm.set(null)" class="px-3 py-1.5 rounded-lg border border-slate-300 text-xs hover:bg-slate-50">Cancel</button>
          </div>
        </div>
      }
    </div>
  `,
})
export class SuperAdminPlansComponent implements OnInit {
  private readonly svc = inject(SuperAdminService);

  readonly plans = signal<PlanOption[]>([]);
  readonly packs = signal<CreditPack[]>([]);
  readonly planForm = signal<PlanUpsert | null>(null);
  readonly packForm = signal<PackUpsert | null>(null);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  editingPlanId: number | null = null;
  editingPackId: number | null = null;

  ngOnInit(): void { this.loadPlans(); this.loadPacks(); }
  loadPlans(): void { this.svc.plans().subscribe((p) => this.plans.set(p)); }
  loadPacks(): void { this.svc.packs().subscribe((p) => this.packs.set(p)); }

  newPlan(): void {
    this.editingPlanId = null;
    this.planForm.set({ name: '', slug: null, monthlyPrice: 0, maxProducts: null, maxOrders: null, aiCredits: 0, features: null, isActive: true, displayOrder: 0 });
  }
  editPlan(p: PlanOption): void {
    this.editingPlanId = p.planId;
    this.planForm.set({ name: p.name, slug: p.slug, monthlyPrice: p.monthlyPrice, maxProducts: p.maxProducts, maxOrders: p.maxOrders, aiCredits: p.aiCredits, features: p.features, isActive: p.isActive, displayOrder: p.displayOrder });
  }
  savePlan(): void {
    const f = this.planForm();
    if (!f || !f.name.trim()) return;
    const done = () => { this.planForm.set(null); this.loadPlans(); this.toast('Plan saved.'); };
    const op = this.editingPlanId ? this.svc.updatePlan(this.editingPlanId, f) : this.svc.createPlan(f);
    op.subscribe({ next: done, error: (e) => this.fail(e) });
  }

  newPack(): void {
    this.editingPackId = null;
    this.packForm.set({ name: '', credits: 100, priceInr: 0, isActive: true, displayOrder: 0 });
  }
  editPack(p: CreditPack): void {
    this.editingPackId = p.aiCreditPackId;
    this.packForm.set({ name: p.name, credits: p.credits, priceInr: p.priceInr, isActive: p.isActive, displayOrder: p.displayOrder });
  }
  savePack(): void {
    const f = this.packForm();
    if (!f || !f.name.trim()) return;
    const done = () => { this.packForm.set(null); this.loadPacks(); this.toast('Pack saved.'); };
    const op = this.editingPackId ? this.svc.updatePack(this.editingPackId, f) : this.svc.createPack(f);
    op.subscribe({ next: done, error: (e) => this.fail(e) });
  }

  private toast(m: string): void { this.message.set(m); this.error.set(null); setTimeout(() => this.message.set(null), 3000); }
  private fail(e: unknown): void { this.error.set((e as { error?: { message?: string } })?.error?.message ?? 'Could not save.'); }
}
