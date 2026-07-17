import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { SuperAdminService } from '../../core/services/superadmin.service';
import { BlocklistEntry } from '../../core/models/superadmin.model';

/** Signup blocklist: block/unblock bad actors by email / GSTIN / phone. */
@Component({
  selector: 'app-superadmin-blocklist',
  imports: [FormsModule],
  template: `
    <h1 class="text-xl font-bold text-slate-900 mb-1">Blocklist</h1>
    <p class="text-sm text-slate-500 mb-4">Blocked values can't be used to sign up a new store.</p>

    <div class="bg-white border border-slate-200 rounded-xl p-4 max-w-2xl">
      <div class="grid sm:grid-cols-5 gap-2 mb-3">
        <select [(ngModel)]="blockType" class="input"><option>Email</option><option>Gstin</option><option>Phone</option></select>
        <input [(ngModel)]="blockValue" placeholder="value" class="input sm:col-span-2" />
        <input [(ngModel)]="blockReason" placeholder="reason (optional)" class="input" />
        <button type="button" (click)="add()" class="btn-primary text-xs">Block</button>
      </div>
      @for (b of blocks(); track b.signupBlocklistId) {
        <div class="flex items-center justify-between text-sm border-t border-slate-100 py-1.5">
          <span><span class="text-slate-400">{{ b.type }}</span> {{ b.value }} <span class="text-xs text-slate-400">{{ b.reason }}</span></span>
          <button type="button" (click)="remove(b.signupBlocklistId)" class="text-red-500 hover:underline text-xs">Remove</button>
        </div>
      }
      @if (!blocks().length) { <div class="text-slate-400 text-sm text-center py-6">Nothing blocked.</div> }
    </div>
  `,
})
export class SuperAdminBlocklistComponent implements OnInit {
  private readonly svc = inject(SuperAdminService);
  readonly blocks = signal<BlocklistEntry[]>([]);
  blockType = 'Email';
  blockValue = '';
  blockReason = '';

  ngOnInit(): void { this.load(); }
  load(): void { this.svc.blocklist().subscribe((b) => this.blocks.set(b)); }

  add(): void {
    if (!this.blockValue.trim()) return;
    this.svc.addBlock(this.blockType, this.blockValue.trim(), this.blockReason.trim() || null)
      .subscribe(() => { this.blockValue = ''; this.blockReason = ''; this.load(); });
  }
  remove(id: number): void { this.svc.removeBlock(id).subscribe(() => this.load()); }
}
