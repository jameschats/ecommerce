import { isPlatformBrowser } from '@angular/common';
import { Component, OnDestroy, OnInit, PLATFORM_ID, computed, inject, signal } from '@angular/core';
import { ThemeService } from '../../core/services/theme.service';

interface AnnouncementMessage { text: string; link?: string; }

/**
 * The storefront announcement bar — a thin promo/notice strip above the header,
 * rendered from the published theme's `announcement` group section. Renders
 * nothing when the theme defines no announcement (so the current chrome is
 * unaffected). Rotates through multiple messages when the section opts into it.
 */
@Component({
  selector: 'app-announcement-bar',
  template: `
    @if (message(); as m) {
      <div class="text-center text-sm py-2 px-4" [style.background-color]="bg()" [style.color]="textColor()">
        @if (m.link) {
          <a [href]="m.link" class="hover:underline">{{ m.text }}</a>
        } @else {
          <span>{{ m.text }}</span>
        }
      </div>
    }
  `,
})
export class AnnouncementBarComponent implements OnInit, OnDestroy {
  private readonly theme = inject(ThemeService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  /** First visible AnnouncementBar section in the announcement zone. */
  private readonly section = computed(() => this.theme.announcement().find((s) => s.sectionType === 'AnnouncementBar') ?? null);
  private readonly settings = computed<Record<string, any>>(() => this.parse(this.section()?.settings, {}));
  private readonly messages = computed<AnnouncementMessage[]>(() =>
    this.parse<AnnouncementMessage[]>(this.section()?.blocks, []).filter((m) => m?.text));

  readonly bg = computed(() => this.theme.resolveBg(this.settings()['colorScheme'], this.settings()['backgroundColor'], '#111827'));
  readonly textColor = computed(() => this.theme.resolveText(this.settings()['colorScheme'], '#ffffff'));
  readonly index = signal(0);
  readonly message = computed<AnnouncementMessage | null>(() => {
    const list = this.messages();
    return list.length ? list[this.index() % list.length] : null;
  });

  private timer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    // Rotate only in the browser, only when opted in and there's more than one message.
    if (this.isBrowser && this.settings()['autoplay'] !== false && this.messages().length > 1) {
      this.timer = setInterval(() => this.index.update((i) => i + 1), 5000);
    }
  }
  ngOnDestroy(): void { if (this.timer) clearInterval(this.timer); }

  private parse<T>(json: string | null | undefined, fallback: T): T {
    try { return json ? JSON.parse(json) as T : fallback; } catch { return fallback; }
  }
}
