import { Component, computed, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ContentSection } from '../../core/models/content-page.model';

interface Stat { value: string; label: string; }
interface Card { icon: string; title: string; text: string; }
interface Cta { heading: string; text: string; buttonLabel: string; buttonLink: string; }

/**
 * Renders the typed sections of a content page.
 *
 * Prose HTML is bound with innerHTML, which is safe here because the API sanitises against a
 * tag allowlist on the way in — nothing reaches the browser that the editor's toolbar could
 * not have produced. The JSON-backed types are parsed and rendered as ordinary bindings, so
 * their text is escaped by Angular like any other interpolation.
 */
@Component({
  selector: 'app-content-sections',
  imports: [RouterLink],
  template: `
    @for (s of sections(); track s.sectionId) {
      @switch (s.sectionType) {

        @case ('Prose') {
          <div class="max-w-3xl">
            @if (s.title) { <h2 class="text-xl sm:text-2xl font-bold text-slate-900 mt-8 mb-2">{{ s.title }}</h2> }
            <div class="prose-content text-slate-600" [innerHTML]="s.content"></div>
          </div>
        }

        @case ('Stats') {
          <div class="grid grid-cols-2 md:grid-cols-4 gap-4 mt-10">
            @for (stat of stats(s); track stat.label) {
              <div class="bg-white border border-slate-200 rounded-xl p-5 text-center">
                <div class="text-2xl font-bold text-primary">{{ stat.value }}</div>
                <div class="text-sm text-slate-500 mt-1">{{ stat.label }}</div>
              </div>
            }
          </div>
        }

        @case ('Cards') {
          @if (s.title) { <h2 class="text-xl font-bold text-slate-900 mt-12 mb-4">{{ s.title }}</h2> }
          <div class="grid sm:grid-cols-2 lg:grid-cols-4 gap-4">
            @for (card of cards(s); track card.title) {
              <div class="bg-white border border-slate-200 rounded-xl p-5">
                <div class="text-2xl">{{ card.icon }}</div>
                <h3 class="font-semibold text-slate-800 mt-2">{{ card.title }}</h3>
                <p class="text-sm text-slate-500 mt-1">{{ card.text }}</p>
              </div>
            }
          </div>
        }

        @case ('Cta') {
          @if (cta(s); as c) {
            <div class="mt-12 bg-gradient-to-br from-primary to-primary-dark rounded-2xl px-8 py-10 text-center text-white">
              <h2 class="text-2xl font-bold">{{ c.heading }}</h2>
              @if (c.text) { <p class="mt-2 text-white/85">{{ c.text }}</p> }
              @if (c.buttonLabel) {
                <a [routerLink]="c.buttonLink || '/order'"
                   class="inline-block mt-5 bg-white text-primary-dark font-medium px-6 py-2.5 rounded-lg hover:bg-slate-100 transition">
                  {{ c.buttonLabel }}
                </a>
              }
            </div>
          }
        }
      }
    }
  `,
  styles: [`
    /* The editor produces bare tags; the site's own type scale dresses them. */
    .prose-content :is(p, ul, ol) { margin-block: 0.6rem; }
    .prose-content :is(ul, ol) { padding-inline-start: 1.4rem; }
    .prose-content ul { list-style: disc; }
    .prose-content ol { list-style: decimal; }
    .prose-content li { margin-block: 0.25rem; }
    .prose-content :is(h3, h4) { font-weight: 600; margin-block: 0.8rem 0.3rem; color: rgb(30 41 59); }
    .prose-content a { color: var(--color-primary, #2563eb); text-decoration: underline; }
    .prose-content strong { font-weight: 600; color: rgb(30 41 59); }
  `],
})
export class ContentSectionsComponent {
  readonly sections = input.required<ContentSection[]>();

  /** Parsed once per content string rather than on every change-detection pass. */
  private readonly parsed = computed(() => {
    const map = new Map<number, unknown>();
    for (const s of this.sections()) {
      if (s.sectionType === 'Prose' || s.sectionType === 'Faq' || !s.content) continue;
      try { map.set(s.sectionId, JSON.parse(s.content)); } catch { /* leave it out */ }
    }
    return map;
  });

  stats(s: ContentSection): Stat[] { return (this.parsed().get(s.sectionId) as Stat[]) ?? []; }
  cards(s: ContentSection): Card[] { return (this.parsed().get(s.sectionId) as Card[]) ?? []; }
  cta(s: ContentSection): Cta | null { return (this.parsed().get(s.sectionId) as Cta) ?? null; }
}
