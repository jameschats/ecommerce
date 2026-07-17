import { Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { SuperAdminService } from '../../core/services/superadmin.service';
import { AuditEntry } from '../../core/models/superadmin.model';

/** Platform access log — every super-admin action across all stores. */
@Component({
  selector: 'app-superadmin-audit',
  imports: [DatePipe, RouterLink],
  template: `
    <h1 class="text-xl font-bold text-slate-900 mb-1">Audit log</h1>
    <p class="text-sm text-slate-500 mb-4">Every platform-admin action, newest first.</p>

    <div class="bg-white border border-slate-200 rounded-xl overflow-hidden">
      <table class="w-full text-sm">
        <thead class="bg-slate-50 text-slate-400 text-left"><tr><th class="px-4 py-2">When</th><th class="px-4 py-2">Action</th><th class="px-4 py-2">Store</th><th class="px-4 py-2">Detail</th></tr></thead>
        <tbody class="divide-y divide-slate-100">
          @for (a of entries(); track a.platformAccessLogId) {
            <tr class="hover:bg-slate-50">
              <td class="px-4 py-2 text-slate-500 whitespace-nowrap">{{ a.createdAt | date:'short' }}</td>
              <td class="px-4 py-2 font-medium text-slate-700">{{ a.action }}</td>
              <td class="px-4 py-2">@if (a.tenantId) { <a [routerLink]="['/superadmin/tenants', a.tenantId]" class="text-blue-600 hover:underline">#{{ a.tenantId }}</a> } @else { <span class="text-slate-300">—</span> }</td>
              <td class="px-4 py-2 text-slate-500">{{ a.detail || '—' }}</td>
            </tr>
          }
          @if (!entries().length) { <tr><td colspan="4" class="px-4 py-8 text-center text-slate-400">No activity yet.</td></tr> }
        </tbody>
      </table>
    </div>
  `,
})
export class SuperAdminAuditComponent implements OnInit {
  private readonly svc = inject(SuperAdminService);
  readonly entries = signal<AuditEntry[]>([]);
  ngOnInit(): void { this.svc.audit(undefined, 200).subscribe((e) => this.entries.set(e)); }
}
