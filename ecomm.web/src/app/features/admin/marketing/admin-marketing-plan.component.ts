import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { MarketingPlanSettings, MarketingStudioService } from '../../../core/services/marketing-studio.service';

/**
 * MS2 sub-step 1 — weekly-plan preferences. The merchant sets how many of each creative type per week,
 * when posts go out, whether next week is auto-drafted, and — per connected channel — which creative
 * types are allowed (the "uncheck poster for Instagram" matrix). These prefs drive the AI plan
 * proposer built in the next sub-step.
 */
@Component({
  selector: 'app-admin-marketing-plan',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900">Marketing Studio — Weekly plan</h1>
      <p class="text-sm text-slate-500 mb-6">
        Tell the studio your rhythm. It proposes a week of posts from this — you review and confirm before anything is generated.
      </p>

      @if (s(); as m) {
        <section class="bg-white border border-slate-200 rounded-xl p-5 space-y-4">
          <h2 class="font-semibold text-slate-900">How much per week</h2>
          <div class="grid sm:grid-cols-3 gap-4">
            <div>
              <label class="lbl">Text posts</label>
              <input type="number" min="0" max="50" [(ngModel)]="m.textPerWeek" name="textPerWeek" class="input" />
            </div>
            <div>
              <label class="lbl">Posters</label>
              <input type="number" min="0" max="50" [(ngModel)]="m.postersPerWeek" name="postersPerWeek" class="input" />
            </div>
            <div>
              <label class="lbl">Videos <span class="text-slate-400 font-normal">(soon)</span></label>
              <input type="number" min="0" max="50" [(ngModel)]="m.videosPerWeek" name="videosPerWeek" class="input" />
            </div>
          </div>
          <div class="grid sm:grid-cols-2 gap-4">
            <div>
              <label class="lbl">Week starts on</label>
              <select [(ngModel)]="m.weekStartDay" name="weekStartDay" class="input">
                @for (d of days; track d.value) { <option [ngValue]="d.value">{{ d.label }}</option> }
              </select>
            </div>
            <div>
              <label class="lbl">Default posting time</label>
              <select [(ngModel)]="m.defaultPostHour" name="defaultPostHour" class="input">
                @for (h of hours; track h) { <option [ngValue]="h">{{ hourLabel(h) }}</option> }
              </select>
            </div>
          </div>
          <label class="flex items-center gap-2 text-sm text-slate-700">
            <input type="checkbox" [(ngModel)]="m.autoRecur" name="autoRecur" />
            Auto-draft next week for me (I'll still confirm before anything is generated)
          </label>
        </section>

        <section class="bg-white border border-slate-200 rounded-xl p-5 mt-5">
          <h2 class="font-semibold text-slate-900">Channels &amp; content types</h2>
          <p class="text-xs text-slate-500 mt-0.5 mb-4">Choose what goes where. Untick a type to keep it off that channel.</p>

          @if (m.channels.length === 0) {
            <div class="text-sm text-slate-500 border border-dashed border-slate-300 rounded-lg p-4">
              No channels connected yet.
              <a routerLink="/admin/marketing/connections" class="text-teal-700 font-medium hover:underline">Connect a channel</a>
              to choose what the studio posts where.
            </div>
          } @else {
            <div class="overflow-x-auto">
              <table class="w-full text-sm">
                <thead>
                  <tr class="text-left text-slate-500 border-b border-slate-200">
                    <th class="py-2 font-medium">Channel</th>
                    <th class="py-2 font-medium text-center w-20">Include</th>
                    <th class="py-2 font-medium text-center w-16">Text</th>
                    <th class="py-2 font-medium text-center w-16">Poster</th>
                    <th class="py-2 font-medium text-center w-16">Video</th>
                  </tr>
                </thead>
                <tbody>
                  @for (c of m.channels; track c.platform) {
                    <tr class="border-b border-slate-100" [class.opacity-50]="!c.enabled">
                      <td class="py-2.5 font-medium text-slate-800">{{ c.displayName }}</td>
                      <td class="py-2.5 text-center"><input type="checkbox" [(ngModel)]="c.enabled" [name]="c.platform + '_en'" /></td>
                      <td class="py-2.5 text-center"><input type="checkbox" [(ngModel)]="c.allowText" [name]="c.platform + '_t'" [disabled]="!c.enabled" /></td>
                      <td class="py-2.5 text-center"><input type="checkbox" [(ngModel)]="c.allowPoster" [name]="c.platform + '_p'" [disabled]="!c.enabled" /></td>
                      <td class="py-2.5 text-center"><input type="checkbox" [(ngModel)]="c.allowVideo" [name]="c.platform + '_v'" [disabled]="!c.enabled" /></td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          }
        </section>

        <div class="flex items-center gap-3 mt-5">
          <button type="button" (click)="save(m)" [disabled]="saving()" class="btn-primary disabled:opacity-60">
            {{ saving() ? 'Saving…' : 'Save preferences' }}
          </button>
          @if (saved()) { <span class="text-sm text-green-700">Saved.</span> }
        </div>
      } @else {
        <p class="text-sm text-slate-500">Loading…</p>
      }
    </div>
  `,
})
export class AdminMarketingPlanComponent implements OnInit {
  private readonly api = inject(MarketingStudioService);

  readonly s = signal<MarketingPlanSettings | null>(null);
  readonly saving = signal(false);
  readonly saved = signal(false);

  readonly days = [
    { value: 0, label: 'Sunday' }, { value: 1, label: 'Monday' }, { value: 2, label: 'Tuesday' },
    { value: 3, label: 'Wednesday' }, { value: 4, label: 'Thursday' }, { value: 5, label: 'Friday' }, { value: 6, label: 'Saturday' },
  ];
  readonly hours = Array.from({ length: 24 }, (_, i) => i);

  ngOnInit(): void {
    this.api.getPlanSettings().subscribe((m) => this.s.set(m));
  }

  hourLabel(h: number): string {
    const ampm = h < 12 ? 'am' : 'pm';
    const hour12 = h % 12 === 0 ? 12 : h % 12;
    return `${hour12}:00 ${ampm}`;
  }

  save(m: MarketingPlanSettings): void {
    this.saving.set(true);
    this.saved.set(false);
    this.api.savePlanSettings(m).subscribe({
      next: (res) => { this.s.set(res); this.saving.set(false); this.saved.set(true); },
      error: () => this.saving.set(false),
    });
  }
}
