import { isPlatformBrowser } from '@angular/common';
import { Component, ElementRef, HostBinding, HostListener, OnDestroy, OnInit, PLATFORM_ID, ViewChild, computed, inject, input, signal } from '@angular/core';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { CatalogService } from '../../core/services/catalog.service';
import { RecentlyViewedService } from '../../core/services/recently-viewed.service';
import { BuilderSection } from '../../core/services/cms.service';
import { Category, ProductListItem } from '../../core/models/catalog.model';
import { ThemeService } from '../../core/services/theme.service';

/**
 * Renders one storefront section from its type + settings/blocks JSON.
 * The single source of truth for how a section looks (matches the Section-Type
 * schema on the backend). RichText is bound via [innerHTML]; it was sanitized
 * server-side, and Angular sanitizes again here.
 */
@Component({
  selector: 'app-storefront-section',
  imports: [RouterLink],
  template: `
    @switch (section().sectionType) {
      @case ('Hero') {
        @switch (s().style) {
          @case ('banner') {
            <!-- Full-bleed slideshow: every Slide block stacked in the same box, cross-fading via
                 opacity so min-height never jumps between slides. Auto-advances on the section's own
                 autoplay/intervalSec settings (shared with the carousel style — see
                 startHeroAutoplayIfNeeded) unless a merchant has only added the one slide, in which
                 case this renders exactly as the old single-image banner always did. -->
            @if (blocks().length) {
              <section class="relative min-h-[360px] sm:min-h-[440px] bg-slate-900 overflow-hidden">
                @for (b of blocks(); track $index) {
                  <div class="absolute inset-0 flex items-center transition-opacity duration-700 ease-in-out"
                       [class.opacity-100]="heroActiveSlide() === $index" [class.opacity-0]="heroActiveSlide() !== $index"
                       [class.pointer-events-none]="heroActiveSlide() !== $index" [attr.aria-hidden]="heroActiveSlide() !== $index"
                       data-field="image" [style.background-image]="b.image ? 'url(' + b.image + ')' : null" style="background-size:cover;background-position:center">
                    <div class="absolute inset-0" style="background:linear-gradient(90deg, rgba(0,0,0,0.72), rgba(0,0,0,0.15))"></div>
                    <div class="relative page-container text-white" [attr.data-block-index]="$index">
                      <div class="max-w-xl">
                        @if (b.heading) { <h2 class="text-4xl sm:text-5xl font-extrabold leading-tight" data-field="heading">{{ b.heading }}</h2> }
                        @if (b.subheading) { <p class="mt-3 text-white/85 text-lg" data-field="subheading">{{ b.subheading }}</p> }
                        @if (b.buttonText) { <a [href]="b.buttonLink || '#'" class="inline-block mt-6 px-6 py-3 rounded-lg bg-primary text-white font-medium" data-field="buttonText">{{ b.buttonText }}</a> }
                      </div>
                    </div>
                  </div>
                }
                @if (blocks().length > 1) {
                  <div class="absolute bottom-5 inset-x-0 flex justify-center gap-1.5 z-10">
                    @for (b of blocks(); track $index) {
                      <button type="button" (click)="heroActiveSlide.set($index)" class="h-2 rounded-full transition-all"
                              [class]="heroActiveSlide() === $index ? 'bg-white w-5' : 'bg-white/50 w-2'" [attr.aria-label]="'Go to slide ' + ($index + 1)"></button>
                    }
                  </div>
                }
              </section>
            }
          }
          @case ('split') {
            @if (blocks()[0]; as b) {
              <section class="page-container py-8">
                <div class="grid md:grid-cols-2 items-stretch rounded-2xl overflow-hidden" style="background:var(--color-secondary,#0f172a)">
                  <div class="p-10 flex flex-col justify-center text-white" [attr.data-block-index]="0">
                    @if (b.heading) { <h2 class="text-3xl sm:text-4xl font-bold" data-field="heading">{{ b.heading }}</h2> }
                    @if (b.subheading) { <p class="mt-3 text-white/80" data-field="subheading">{{ b.subheading }}</p> }
                    @if (b.buttonText) { <a [href]="b.buttonLink || '#'" class="inline-block mt-5 px-5 py-2.5 rounded-lg bg-white text-slate-900 font-medium w-fit" data-field="buttonText">{{ b.buttonText }}</a> }
                  </div>
                  @if (b.image) { <img [src]="b.image" alt="" class="w-full h-full object-cover min-h-[280px]" data-field="image" /> }
                </div>
              </section>
            }
          }
          @case ('panels') {
            <!-- Three-panel hero: colour panel with copy flanked by two images (playful/kids look). -->
            @if (blocks()[0]; as b) {
              <section class="page-container py-6">
                <div class="grid md:grid-cols-[1fr_1.7fr_1fr] gap-4 items-stretch">
                  @if (blocks()[1]; as l) { <div class="hidden md:block overflow-hidden sf-card" [attr.data-block-index]="1"><img [src]="l.image" alt="" class="w-full h-full object-cover" data-field="image" /></div> }
                  <div class="p-8 sm:p-10 flex flex-col justify-center min-h-[320px] overflow-hidden" style="border-radius: var(--radius-card, 0.75rem)" [attr.data-block-index]="0"
                       [style.background-color]="theme.resolveBg(s()['colorScheme'], s().backgroundColor, 'var(--color-primary)')"
                       [style.color]="theme.resolveText(s()['colorScheme'], '#ffffff')">
                    @if (b.heading) { <h2 class="text-3xl sm:text-4xl font-extrabold leading-tight" data-field="heading">{{ b.heading }}</h2> }
                    @if (b.subheading) { <p class="mt-3 text-white/85" data-field="subheading">{{ b.subheading }}</p> }
                    @if (b.buttonText) { <a [href]="b.buttonLink || '#'" class="inline-block mt-5 px-5 py-2.5 rounded-lg bg-white text-slate-900 font-medium w-fit" data-field="buttonText">{{ b.buttonText }}</a> }
                  </div>
                  @if (blocks()[2]; as r) { <div class="hidden md:block overflow-hidden sf-card" [attr.data-block-index]="2"><img [src]="r.image" alt="" class="w-full h-full object-cover" data-field="image" /></div> }
                </div>
              </section>
            }
          }
          @case ('carousel') {
            <!-- Multi-card sliding hero: each slide is its own full-bleed image+heading+CTA card,
                 ~3 visible on desktop / 1 on mobile, with arrow + dot navigation. -->
            <section class="page-container py-6">
              <div class="relative">
                <div #heroCarousel (scroll)="onHeroCarouselScroll($event)" class="flex gap-3 overflow-x-auto no-scrollbar snap-x scroll-smooth">
                  @for (b of blocks(); track $index) {
                    <div data-hero-card data-field="image" class="relative shrink-0 snap-start w-[82%] sm:w-[48%] lg:w-[32%] min-h-[170px] sm:min-h-[220px] overflow-hidden sf-card" [attr.data-block-index]="$index"
                         [style.background-image]="b.image ? 'url(' + b.image + ')' : null" style="background-size:cover;background-position:center">
                      <div class="absolute inset-0 bg-gradient-to-t from-black/70 via-black/10 to-transparent"></div>
                      <div class="relative h-full flex flex-col justify-end p-4 sm:p-5 text-white">
                        @if (b.heading) { <h3 class="text-lg sm:text-xl font-bold leading-tight" data-field="heading">{{ b.heading }}</h3> }
                        @if (b.subheading) { <p class="mt-1 text-white/85 text-xs sm:text-sm" data-field="subheading">{{ b.subheading }}</p> }
                        @if (b.buttonText) { <a [href]="b.buttonLink || '#'" class="inline-block mt-2 sm:mt-3 px-3 py-1.5 rounded-lg bg-primary text-white font-medium text-xs sm:text-sm w-fit" data-field="buttonText">{{ b.buttonText }}</a> }
                      </div>
                    </div>
                  }
                  @if (!blocks().length) { <div class="w-full min-h-[170px] grid place-items-center text-slate-300 bg-slate-100 rounded-2xl">Add slides to this hero</div> }
                </div>
                @if (blocks().length > 1) {
                  <button type="button" (click)="scrollHeroCarousel(-1)" class="hidden sm:grid absolute left-2 top-1/2 -translate-y-1/2 w-9 h-9 rounded-full bg-white/90 shadow place-items-center hover:bg-white text-slate-700" aria-label="Previous">‹</button>
                  <button type="button" (click)="scrollHeroCarousel(1)" class="hidden sm:grid absolute right-2 top-1/2 -translate-y-1/2 w-9 h-9 rounded-full bg-white/90 shadow place-items-center hover:bg-white text-slate-700" aria-label="Next">›</button>
                }
              </div>
              @if (blocks().length > 1) {
                <div class="flex justify-center gap-1.5 mt-4">
                  @for (b of blocks(); track $index) {
                    <button type="button" (click)="scrollHeroCarouselTo($index)" class="h-2 rounded-full transition-all" [class]="heroActiveSlide() === $index ? 'bg-primary w-5' : 'bg-slate-300 w-2'" [attr.aria-label]="'Go to slide ' + ($index + 1)"></button>
                  }
                </div>
              }
            </section>
          }
          @default {
            <section class="relative">
              @for (b of blocks(); track $index) {
                <div class="relative min-h-[320px] flex items-center justify-center text-center bg-slate-900 text-white" [attr.data-block-index]="$index" data-field="image"
                     [style.background-image]="b.image ? 'url(' + b.image + ')' : null" style="background-size:cover;background-position:center">
                  <div class="bg-black/30 absolute inset-0"></div>
                  <div class="relative p-8 max-w-2xl">
                    @if (b.heading) { <h2 class="text-3xl sm:text-4xl font-bold" data-field="heading">{{ b.heading }}</h2> }
                    @if (b.subheading) { <p class="mt-2 text-slate-200" data-field="subheading">{{ b.subheading }}</p> }
                    @if (b.buttonText) { <a [href]="b.buttonLink || '#'" class="inline-block mt-4 px-5 py-2 rounded-lg bg-primary text-white font-medium" data-field="buttonText">{{ b.buttonText }}</a> }
                  </div>
                </div>
              }
              @if (!blocks().length) { <div class="min-h-[200px] grid place-items-center text-slate-300 bg-slate-100">Add slides to this hero</div> }
            </section>
          }
        }
      }
      @case ('Multicolumn') {
        <section class="page-container py-8">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 mb-6 text-center" data-field="heading">{{ s().heading }}</h2> }
          <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
            @for (b of blocks(); track $index) {
              <div class="text-center p-5 sf-card" [attr.data-block-index]="$index">
                @if (b.icon) { <div class="text-3xl" data-field="icon">{{ b.icon }}</div> }
                @if (b.heading) { <h3 class="font-semibold text-slate-900 mt-2" data-field="heading">{{ b.heading }}</h3> }
                @if (b.text) { <p class="text-sm text-slate-500 mt-1" data-field="text">{{ b.text }}</p> }
              </div>
            }
          </div>
        </section>
      }
      @case ('TileGrid') {
        <section class="page-container py-8">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 mb-5" data-field="heading">{{ s().heading }}</h2> }
          <div class="grid gap-4" [class]="tileCols()">
            @for (b of blocks(); track $index) {
              <a [href]="b.link || '/products'" class="group block overflow-hidden sf-card" [attr.data-block-index]="$index">
                <div class="aspect-[4/5] bg-slate-100 overflow-hidden" data-field="image">
                  @if (b.image) { <img [src]="b.image" [alt]="b.label || ''" class="w-full h-full object-cover group-hover:scale-105 transition" loading="lazy" /> }
                </div>
                @if (b.label || b.sublabel) {
                  <div class="p-3 text-center">
                    @if (b.label) { <div class="font-semibold text-slate-800" data-field="label">{{ b.label }}</div> }
                    @if (b.sublabel) { <div class="text-xs text-slate-500 mt-0.5" data-field="sublabel">{{ b.sublabel }}</div> }
                  </div>
                }
              </a>
            }
          </div>
        </section>
      }
      @case ('PromoTiles') {
        <section class="page-container py-8">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 mb-5" data-field="heading">{{ s().heading }}</h2> }
          <div class="grid grid-cols-2 sm:grid-cols-3 gap-4">
            @for (b of blocks(); track $index) {
              <a [href]="b.link || '/products'" class="relative block overflow-hidden min-h-[170px] sf-card" [attr.data-block-index]="$index"
                 [style.background-color]="theme.resolveBg(b.colorScheme, b.backgroundColor, 'var(--color-secondary, #0f172a)')" data-field="image">
                @if (b.image) { <img [src]="b.image" alt="" class="absolute inset-0 w-full h-full object-cover" loading="lazy" /> }
                <div class="absolute inset-0 bg-gradient-to-t from-black/60 via-black/10 to-transparent"></div>
                <div class="relative p-4 flex flex-col justify-end h-full min-h-[170px]" [style.color]="theme.resolveText(b.colorScheme, '#ffffff')">
                  @if (b.badge) { <span class="text-[11px] font-bold uppercase tracking-wide bg-white/90 text-slate-900 rounded px-1.5 py-0.5 w-fit mb-1.5" data-field="badge">{{ b.badge }}</span> }
                  @if (b.heading) { <div class="font-bold leading-snug" data-field="heading">{{ b.heading }}</div> }
                  @if (b.text) { <div class="text-xs text-white/80 mt-0.5" data-field="text">{{ b.text }}</div> }
                </div>
              </a>
            }
          </div>
        </section>
      }
      @case ('Marquee') {
        <div class="overflow-hidden py-2.5 text-sm font-medium"
             [style.background-color]="theme.resolveBg(s()['colorScheme'], s().backgroundColor, '#111827')"
             [style.color]="theme.resolveText(s()['colorScheme'], '#ffffff')">
          <div class="marquee-x flex whitespace-nowrap w-max" data-field="text">
            @for (i of ph; track i) {
              <span class="mx-6">{{ s().text || 'Free shipping over ₹499' }}</span><span class="opacity-50">✦</span>
              <span class="mx-6">{{ s().text || 'Free shipping over ₹499' }}</span><span class="opacity-50">✦</span>
            }
          </div>
        </div>
      }
      @case ('CountdownBar') {
        <div class="py-3 px-4 flex flex-wrap items-center justify-center gap-3 text-sm font-medium text-center"
             [style.background-color]="theme.resolveBg(s()['colorScheme'], s().backgroundColor, '#111827')"
             [style.color]="theme.resolveText(s()['colorScheme'], '#ffffff')">
          @if (remaining(); as r) {
            @if (s().heading) { <span data-field="heading">{{ s().heading }}</span> }
            <span class="font-mono font-bold tabular-nums">{{ r.days }}d {{ r.hours }}h {{ r.mins }}m {{ r.secs }}s</span>
          } @else {
            <span data-field="expiredText">{{ s().expiredText || 'This offer has ended' }}</span>
          }
          @if (s().buttonText) { <a [href]="s().buttonLink || '#'" class="ml-2 px-3 py-1 rounded-lg bg-white/15 hover:bg-white/25 font-medium" data-field="buttonText">{{ s().buttonText }}</a> }
        </div>
      }
      @case ('RichText') {
        <div class="max-w-3xl mx-auto px-4 py-8 prose" data-field="content" [style.text-align]="s().align || 'left'" [innerHTML]="s().content"></div>
      }
      @case ('ImageWithText') {
        <section class="max-w-5xl mx-auto px-4 py-10 grid sm:grid-cols-2 gap-8 items-center" [class.sm:flex-row-reverse]="s().imageSide === 'right'">
          @if (s().image) { <img [src]="s().image" alt="" class="rounded-xl w-full object-cover" [class.sm:order-2]="s().imageSide === 'right'" data-field="image" /> }
          <div>
            @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900" data-field="heading">{{ s().heading }}</h2> }
            @if (s().body) { <p class="mt-2 text-slate-600" data-field="body">{{ s().body }}</p> }
            @if (s().buttonText) { <a [href]="s().buttonLink || '#'" class="inline-block mt-4 px-5 py-2 rounded-lg bg-primary text-white font-medium" data-field="buttonText">{{ s().buttonText }}</a> }
          </div>
        </section>
      }
      @case ('Testimonials') {
        <section class="max-w-5xl mx-auto px-4 py-10">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 text-center mb-6" data-field="heading">{{ s().heading }}</h2> }
          <div class="grid sm:grid-cols-3 gap-4">
            @for (b of blocks(); track $index) {
              <div class="bg-white border border-slate-200 rounded-xl p-5" [attr.data-block-index]="$index">
                <div class="text-amber-400" data-field="rating">{{ stars(b.rating) }}</div>
                <p class="text-slate-600 mt-2" data-field="quote">"{{ b.quote }}"</p>
                <div class="text-sm font-medium text-slate-800 mt-3" data-field="author">— {{ b.author }}</div>
              </div>
            }
          </div>
        </section>
      }
      @case ('CtaNewsletter') {
        <section class="py-12 text-center"
                 [style.background-color]="theme.resolveBg(s()['colorScheme'], s().backgroundColor, '#111827')"
                 [style.color]="theme.resolveText(s()['colorScheme'], '#ffffff')">
          @if (s().heading) { <h2 class="text-2xl font-bold" data-field="heading">{{ s().heading }}</h2> }
          @if (s().subtext) { <p class="mt-1 text-white/80" data-field="subtext">{{ s().subtext }}</p> }
          @if (s().buttonText) { <a [href]="s().buttonLink || '#'" class="inline-block mt-4 px-6 py-2 rounded-lg bg-white text-slate-900 font-medium" data-field="buttonText">{{ s().buttonText }}</a> }
        </section>
      }
      @case ('Categories') {
        <section class="max-w-6xl mx-auto px-4 py-10">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 mb-5" data-field="heading">{{ s().heading }}</h2> }
          @if (categories().length) {
            @if (s().style === 'cards') {
              <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
                @for (c of categories(); track c.categoryId) {
                  <a [routerLink]="['/category', c.slug]" class="group block overflow-hidden sf-card">
                    <div class="aspect-[4/3] bg-slate-100 overflow-hidden">
                      @if (c.imageUrl) { <img [src]="c.imageUrl" alt="" class="w-full h-full object-cover group-hover:scale-105 transition" loading="lazy" /> }
                    </div>
                    <div class="p-3 text-center text-sm font-semibold text-slate-800">{{ c.name }}</div>
                  </a>
                }
              </div>
            } @else {
              <div class="grid grid-cols-2 sm:grid-cols-4 lg:grid-cols-6 gap-3">
                @for (c of categories(); track c.categoryId) {
                  <a [routerLink]="['/category', c.slug]" class="block p-4 text-center hover:border-primary transition sf-card">
                    @if (c.imageUrl) { <img [src]="c.imageUrl" alt="" class="w-12 h-12 mx-auto object-contain mb-2" /> }
                    <div class="text-sm font-medium text-slate-700">{{ c.name }}</div>
                  </a>
                }
              </div>
            }
          } @else {
            <!-- No categories yet — placeholder tiles so the layout reads (real ones replace these). -->
            <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
              @for (i of ph; track i) {
                <div class="overflow-hidden sf-card">
                  <div class="aspect-[4/3] bg-slate-100"></div>
                  <div class="p-3 flex justify-center"><div class="h-3 w-20 bg-slate-100 rounded"></div></div>
                </div>
              }
            </div>
          }
        </section>
      }
      @case ('Collage') {
        <section class="page-container py-8">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 mb-5" data-field="heading">{{ s().heading }}</h2> }
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-3 auto-rows-[140px] sm:auto-rows-[160px]">
            @for (b of blocks(); track $index) {
              <a [href]="b.link || '/products'" class="group block overflow-hidden sf-card" [attr.data-block-index]="$index" data-field="image"
                 [class]="b.span === 'large' ? 'col-span-2 row-span-2' : 'col-span-1 row-span-1'">
                @if (b.image) { <img [src]="b.image" alt="" class="w-full h-full object-cover group-hover:scale-105 transition" loading="lazy" /> }
              </a>
            }
          </div>
        </section>
      }
      @case ('EditorialSplit') {
        <section class="page-container py-10">
          <div class="grid md:grid-cols-2 gap-10 items-center" [class.md:flex-row-reverse]="s().imageSide === 'right'">
            @if (s().image) { <img [src]="s().image" alt="" class="rounded-xl w-full object-cover aspect-[4/3]" [class.md:order-2]="s().imageSide === 'right'" data-field="image" /> }
            <div>
              @if (s().eyebrow) { <div class="text-sm font-semibold uppercase tracking-wide text-primary mb-2" data-field="eyebrow">{{ s().eyebrow }}</div> }
              @if (s().heading) { <h2 class="text-3xl sm:text-4xl font-extrabold text-slate-900 leading-tight" data-field="heading">{{ s().heading }}</h2> }
              @if (s().body) { <p class="mt-4 text-lg text-slate-600" data-field="body">{{ s().body }}</p> }
              @if (s().buttonText) { <a [href]="s().buttonLink || '#'" class="inline-block mt-6 px-6 py-3 rounded-lg bg-primary text-white font-medium" data-field="buttonText">{{ s().buttonText }}</a> }
            </div>
          </div>
        </section>
      }
      @case ('FaqAccordion') {
        <section class="max-w-3xl mx-auto px-4 py-10">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 text-center mb-6" data-field="heading">{{ s().heading }}</h2> }
          <div class="divide-y divide-slate-200 border-y border-slate-200">
            @for (b of blocks(); track $index) {
              <div [attr.data-block-index]="$index">
                <button type="button" (click)="toggleFaq($index)" class="w-full flex items-center justify-between gap-3 py-4 text-left font-medium text-slate-800">
                  <span data-field="question">{{ b.question }}</span>
                  <span class="text-slate-400 shrink-0">{{ isFaqOpen($index) ? '−' : '+' }}</span>
                </button>
                @if (isFaqOpen($index)) { <p class="pb-4 text-slate-600" data-field="answer">{{ b.answer }}</p> }
              </div>
            }
          </div>
        </section>
      }
      @case ('VideoSection') {
        <section class="page-container py-10">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 text-center mb-5" data-field="heading">{{ s().heading }}</h2> }
          <div class="max-w-4xl mx-auto rounded-xl overflow-hidden bg-slate-900 aspect-video" data-field="videoUrl">
            @if (videoEmbedSrc(s().videoUrl); as embed) {
              <iframe [src]="embed" class="w-full h-full" frameborder="0" allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture" allowfullscreen></iframe>
            } @else if (s().videoUrl) {
              <video [src]="s().videoUrl" [poster]="s().posterImage || null" controls class="w-full h-full object-cover"></video>
            } @else if (s().posterImage) {
              <img [src]="s().posterImage" alt="" class="w-full h-full object-cover" />
            }
          </div>
          @if (s().caption) { <p class="text-center text-sm text-slate-500 mt-3" data-field="caption">{{ s().caption }}</p> }
        </section>
      }
      @case ('LogoStrip') {
        <section class="page-container py-8">
          @if (s().heading) { <h2 class="text-center text-sm font-semibold uppercase tracking-wide text-slate-400 mb-5" data-field="heading">{{ s().heading }}</h2> }
          <div class="flex flex-wrap items-center justify-center gap-8 sm:gap-12">
            @for (b of blocks(); track $index) {
              <a [href]="b.link || '#'" [attr.data-block-index]="$index" data-field="image" class="opacity-60 hover:opacity-100 transition grayscale hover:grayscale-0">
                @if (b.image) { <img [src]="b.image" [alt]="b.label || ''" class="h-8 sm:h-10 object-contain" loading="lazy" /> }
              </a>
            }
          </div>
        </section>
      }
      @case ('Stats') {
        <section class="page-container py-10">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 text-center mb-8" data-field="heading">{{ s().heading }}</h2> }
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-6 text-center">
            @for (b of blocks(); track $index) {
              <div [attr.data-block-index]="$index">
                <div class="text-3xl sm:text-4xl font-extrabold text-slate-900" data-field="value">{{ b.value }}</div>
                <div class="text-sm text-slate-500 mt-1" data-field="label">{{ b.label }}</div>
              </div>
            }
          </div>
        </section>
      }
      @case ('ImageGallery') {
        <section class="page-container py-8">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 mb-5" data-field="heading">{{ s().heading }}</h2> }
          <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-3">
            @for (b of blocks(); track $index) {
              <button type="button" (click)="openGalleryLightbox($index)" [attr.data-block-index]="$index" data-field="image"
                class="group block aspect-square overflow-hidden sf-card">
                @if (b.image) { <img [src]="b.image" [alt]="b.caption || ''" class="w-full h-full object-cover group-hover:scale-105 transition" loading="lazy" /> }
              </button>
            }
          </div>
        </section>
        @if (galleryLightboxIndex() !== null) {
          <div class="fixed inset-0 z-50 bg-black/90 flex items-center justify-center" (click)="closeGalleryLightbox()">
            <button type="button" (click)="closeGalleryLightbox()" aria-label="Close"
              class="absolute top-4 right-4 text-white/80 hover:text-white text-3xl leading-none w-10 h-10 grid place-items-center">×</button>
            @if (blocks().length > 1) {
              <button type="button" (click)="$event.stopPropagation(); stepGalleryLightbox(-1)" aria-label="Previous image"
                class="absolute left-4 top-1/2 -translate-y-1/2 text-white/80 hover:text-white text-4xl w-12 h-12 grid place-items-center">‹</button>
              <button type="button" (click)="$event.stopPropagation(); stepGalleryLightbox(1)" aria-label="Next image"
                class="absolute right-4 top-1/2 -translate-y-1/2 text-white/80 hover:text-white text-4xl w-12 h-12 grid place-items-center">›</button>
            }
            @if (blocks()[galleryLightboxIndex()!]; as b) {
              <figure class="flex flex-col items-center max-w-[90vw]" (click)="$event.stopPropagation()">
                <img [src]="b.image" [alt]="b.caption || ''" class="max-w-[90vw] max-h-[85vh] object-contain" />
                @if (b.caption) { <figcaption class="text-white/80 text-sm mt-3">{{ b.caption }}</figcaption> }
              </figure>
            }
          </div>
        }
      }
      @case ('TabbedProductGrid') {
        <!-- Phase F: tabs switch the product grid in place, no navigation (boAt's "Big Deals" pattern). -->
        <section class="max-w-6xl mx-auto px-4 py-10">
          @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900 mb-5" data-field="heading">{{ s().heading }}</h2> }
          @if (blocks().length) {
            <div class="flex gap-2 overflow-x-auto no-scrollbar border-b border-slate-200 mb-6">
              @for (b of blocks(); track $index) {
                <button type="button" (click)="selectProductTab($index)" [attr.data-block-index]="$index"
                  class="px-4 py-2.5 text-sm font-medium whitespace-nowrap border-b-2 transition"
                  [class]="activeProductTab() === $index ? 'border-primary text-primary' : 'border-transparent text-slate-500 hover:text-slate-800'">
                  <span data-field="label">{{ b.label || 'Tab ' + ($index + 1) }}</span>
                </button>
              }
            </div>
            @if (tabProductsLoading()) {
              <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
                @for (i of ph; track i) {
                  <div class="overflow-hidden sf-card">
                    <div class="aspect-square bg-slate-100"></div>
                    <div class="p-3 space-y-2"><div class="h-3 w-3/4 bg-slate-100 rounded"></div><div class="h-3 w-1/3 bg-slate-100 rounded"></div></div>
                  </div>
                }
              </div>
            } @else if (!tabProducts().length) {
              <p class="text-slate-400 text-center py-10">No products in this tab yet.</p>
            } @else {
              <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
                @for (p of tabProducts(); track p.productId) {
                  <a [routerLink]="['/product', p.slug]" class="block overflow-hidden sf-card">
                    <div class="aspect-square bg-slate-50 grid place-items-center overflow-hidden">
                      @if (p.primaryImageUrl) { <img [src]="p.primaryImageUrl" [alt]="p.name" class="w-full h-full object-cover" /> } @else { <span class="text-slate-300 text-xs">No image</span> }
                    </div>
                    <div class="p-3"><div class="text-sm font-medium text-slate-800 line-clamp-2">{{ p.name }}</div><div class="text-slate-900 font-bold mt-1">₹{{ p.price }}</div></div>
                  </a>
                }
              </div>
            }
          }
        </section>
      }
      @case ('InstagramFeed') {
        <section class="page-container py-8">
          <div class="flex items-center justify-between mb-5">
            @if (s().heading) { <h2 class="text-2xl font-bold text-slate-900" data-field="heading">{{ s().heading }}</h2> }
            @if (s().profileUrl || s().handle) {
              <a [href]="s().profileUrl || '#'" target="_blank" rel="noopener" class="text-sm font-medium text-primary hover:underline" data-field="handle">
                {{ s().handle ? '@' + s().handle : 'Follow us' }}
              </a>
            }
          </div>
          <div class="grid grid-cols-2 sm:grid-cols-4 gap-2 sm:gap-3">
            @for (b of blocks(); track $index) {
              <a [href]="b.link || s().profileUrl || '#'" target="_blank" rel="noopener" data-field="image"
                class="group block aspect-square overflow-hidden bg-slate-100" [attr.data-block-index]="$index">
                @if (b.image) { <img [src]="b.image" alt="" class="w-full h-full object-cover group-hover:scale-105 transition" loading="lazy" /> }
              </a>
            }
          </div>
        </section>
      }
      @case ('CustomSection') {
        <!-- T17: a free-form canvas — each block declares its own type (Heading/Text/Image/Button/
             Spacer/Divider), unlike every other section type here where every block in the array is
             implicitly the same kind. -->
        <section class="page-container py-8">
          <div class="max-w-3xl mx-auto space-y-4">
            @for (b of blocks(); track $index) {
              <div [attr.data-block-index]="$index">
                @switch (b.type) {
                  @case ('Heading') {
                    @switch (b.size) {
                      @case ('Large') { <h2 class="text-3xl sm:text-4xl font-bold text-slate-900" data-field="text">{{ b.text }}</h2> }
                      @case ('Small') { <h4 class="text-lg font-semibold text-slate-900" data-field="text">{{ b.text }}</h4> }
                      @default { <h3 class="text-2xl font-bold text-slate-900" data-field="text">{{ b.text }}</h3> }
                    }
                  }
                  @case ('Text') {
                    <div class="prose text-slate-600" data-field="content" [innerHTML]="b.content"></div>
                  }
                  @case ('Image') {
                    @if (b.image) {
                      @if (b.link) {
                        <a [href]="b.link" data-field="image"><img [src]="b.image" alt="" class="w-full rounded-lg object-cover" /></a>
                      } @else {
                        <img [src]="b.image" alt="" class="w-full rounded-lg object-cover" data-field="image" />
                      }
                    }
                  }
                  @case ('Button') {
                    <a [href]="b.link || '#'" data-field="text"
                       class="inline-block px-6 py-3 rounded-lg font-medium transition"
                       [class]="b.style === 'Secondary' ? 'border border-slate-300 text-slate-700 hover:bg-slate-50' : 'bg-primary text-white hover:bg-primary-dark'">
                      {{ b.text }}
                    </a>
                  }
                  @case ('Spacer') {
                    <div [class]="b.height === 'Large' ? 'h-16' : b.height === 'Small' ? 'h-4' : 'h-8'"></div>
                  }
                  @case ('Divider') {
                    <hr class="border-slate-200" />
                  }
                }
              </div>
            }
            @if (!blocks().length) { <p class="text-slate-300 text-center py-10">Add blocks to build this section.</p> }
          </div>
        </section>
      }
      @case ('FeaturedProducts') {
        <!-- Was falling through to @default below, which uses a narrower max-w-6xl (1152px) container
             than every other section's shared page-container (1480px) — visibly narrower than its
             neighbours on the same page. Same markup, correct width. -->
        <section class="page-container py-10">
          @if (s().heading || section().title) { <h2 class="text-2xl font-bold text-slate-900 mb-5" data-field="heading">{{ s().heading || section().title }}</h2> }
          @if (!products().length) {
            <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
              @for (i of ph; track i) {
                <div class="overflow-hidden sf-card">
                  <div class="aspect-square bg-slate-100"></div>
                  <div class="p-3 space-y-2"><div class="h-3 w-3/4 bg-slate-100 rounded"></div><div class="h-3 w-1/3 bg-slate-100 rounded"></div></div>
                </div>
              }
            </div>
          } @else if (s().layout === 'carousel') {
            <!-- Was scroll-only — no arrow buttons at all, unlike the Hero carousel (which has both
                 arrows and dots). Fine on touch/trackpad, but gave a mouse-only desktop visitor zero
                 visible way to see more than what's in view. Same scroll-by-card-width mechanism as
                 scrollHeroCarousel, just targeting this carousel's own #prodCarousel instead. -->
            <div class="relative">
              <div #prodCarousel class="flex gap-4 overflow-x-auto no-scrollbar snap-x pb-2 scroll-smooth">
                @for (p of products(); track p.productId) {
                  <a data-prod-card [routerLink]="['/product', p.slug]" class="snap-start shrink-0 w-44 sm:w-52 block overflow-hidden sf-card">
                    <div class="aspect-square bg-slate-50 grid place-items-center overflow-hidden">
                      @if (p.primaryImageUrl) { <img [src]="p.primaryImageUrl" [alt]="p.name" class="w-full h-full object-cover" /> } @else { <span class="text-slate-300 text-xs">No image</span> }
                    </div>
                    <div class="p-3"><div class="text-sm font-medium text-slate-800 line-clamp-2">{{ p.name }}</div><div class="text-slate-900 font-bold mt-1">₹{{ p.price }}</div></div>
                  </a>
                }
              </div>
              @if (products().length > 4) {
                <button type="button" (click)="scrollProductCarousel(-1)" class="hidden sm:grid absolute left-0 top-[38%] -translate-y-1/2 -translate-x-1/2 w-9 h-9 rounded-full bg-white shadow place-items-center hover:bg-slate-50 text-slate-700" aria-label="Previous">‹</button>
                <button type="button" (click)="scrollProductCarousel(1)" class="hidden sm:grid absolute right-0 top-[38%] -translate-y-1/2 translate-x-1/2 w-9 h-9 rounded-full bg-white shadow place-items-center hover:bg-slate-50 text-slate-700" aria-label="Next">›</button>
              }
            </div>
          } @else {
            <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
              @for (p of products(); track p.productId) {
                <a [routerLink]="['/product', p.slug]" class="block overflow-hidden sf-card">
                  <div class="aspect-square bg-slate-50 grid place-items-center overflow-hidden">
                    @if (p.primaryImageUrl) { <img [src]="p.primaryImageUrl" [alt]="p.name" class="w-full h-full object-cover" /> } @else { <span class="text-slate-300 text-xs">No image</span> }
                  </div>
                  <div class="p-3"><div class="text-sm font-medium text-slate-800 line-clamp-2">{{ p.name }}</div><div class="text-slate-900 font-bold mt-1">₹{{ p.price }}</div></div>
                </a>
              }
            </div>
          }
        </section>
      }
      @default {
        <!-- ProductGrid / any other unhandled product-rail-shaped type -->
        <section class="max-w-6xl mx-auto px-4 py-10">
          @if (s().heading || section().title) { <h2 class="text-2xl font-bold text-slate-900 mb-5" data-field="heading">{{ s().heading || section().title }}</h2> }
          @if (!products().length) {
            <!-- No products yet — skeleton cards so the layout reads (real ones replace these). -->
            <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
              @for (i of ph; track i) {
                <div class="overflow-hidden sf-card">
                  <div class="aspect-square bg-slate-100"></div>
                  <div class="p-3 space-y-2"><div class="h-3 w-3/4 bg-slate-100 rounded"></div><div class="h-3 w-1/3 bg-slate-100 rounded"></div></div>
                </div>
              }
            </div>
          } @else if (s().layout === 'carousel') {
            <!-- Was scroll-only — no arrow buttons at all, unlike the Hero carousel (which has both
                 arrows and dots). Fine on touch/trackpad, but gave a mouse-only desktop visitor zero
                 visible way to see more than what's in view. Same scroll-by-card-width mechanism as
                 scrollHeroCarousel, just targeting this carousel's own #prodCarousel instead. -->
            <div class="relative">
              <div #prodCarousel class="flex gap-4 overflow-x-auto no-scrollbar snap-x pb-2 scroll-smooth">
                @for (p of products(); track p.productId) {
                  <a data-prod-card [routerLink]="['/product', p.slug]" class="snap-start shrink-0 w-44 sm:w-52 block overflow-hidden sf-card">
                    <div class="aspect-square bg-slate-50 grid place-items-center overflow-hidden">
                      @if (p.primaryImageUrl) { <img [src]="p.primaryImageUrl" [alt]="p.name" class="w-full h-full object-cover" /> } @else { <span class="text-slate-300 text-xs">No image</span> }
                    </div>
                    <div class="p-3"><div class="text-sm font-medium text-slate-800 line-clamp-2">{{ p.name }}</div><div class="text-slate-900 font-bold mt-1">₹{{ p.price }}</div></div>
                  </a>
                }
              </div>
              @if (products().length > 4) {
                <button type="button" (click)="scrollProductCarousel(-1)" class="hidden sm:grid absolute left-0 top-[38%] -translate-y-1/2 -translate-x-1/2 w-9 h-9 rounded-full bg-white shadow place-items-center hover:bg-slate-50 text-slate-700" aria-label="Previous">‹</button>
                <button type="button" (click)="scrollProductCarousel(1)" class="hidden sm:grid absolute right-0 top-[38%] -translate-y-1/2 translate-x-1/2 w-9 h-9 rounded-full bg-white shadow place-items-center hover:bg-slate-50 text-slate-700" aria-label="Next">›</button>
              }
            </div>
          } @else {
            <div class="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 gap-4">
              @for (p of products(); track p.productId) {
                <a [routerLink]="['/product', p.slug]" class="block overflow-hidden sf-card">
                  <div class="aspect-square bg-slate-50 grid place-items-center overflow-hidden">
                    @if (p.primaryImageUrl) { <img [src]="p.primaryImageUrl" [alt]="p.name" class="w-full h-full object-cover" /> } @else { <span class="text-slate-300 text-xs">No image</span> }
                  </div>
                  <div class="p-3"><div class="text-sm font-medium text-slate-800 line-clamp-2">{{ p.name }}</div><div class="text-slate-900 font-bold mt-1">₹{{ p.price }}</div></div>
                </a>
              }
            </div>
          }
        </section>
      }
    }

    <!-- E1 editor-canvas overlays (never rendered for real shoppers). data-editor-toolbar marks
         elements the capture-phase click/mousedown interceptors must leave alone so these
         buttons' own (click) handlers fire. -->
    @if (theme.editorMode()) {
      <button type="button" data-editor-toolbar class="te-insert" (click)="postInsert()">＋ Add section</button>
      @if (highlighted()) {
        <div class="te-toolbar" data-editor-toolbar>
          <button type="button" (click)="postAction('toggleHide')">Hide</button>
          <button type="button" (click)="postAction('duplicate')">Duplicate</button>
          <button type="button" class="te-danger" (click)="postAction('delete')">Delete</button>
        </div>
      }
    }
  `,
  styles: [`
    /* T15/E1 editor-canvas affordances — every rule is scoped under .theme-editor-canvas, which is
       only set in editor mode, so none of this exists for real shoppers. */
    :host(.theme-editor-canvas) { display: block; position: relative; }
    :host(.theme-editor-selected) { outline: 2px solid #2563eb; outline-offset: -2px; }
    :host(.theme-editor-canvas:hover) { outline: 1px dashed #93c5fd; outline-offset: -1px; }
    :host(.theme-editor-canvas:hover)::before {
      content: attr(data-section-label);
      position: absolute; top: 0; left: 0; z-index: 30;
      background: #2563eb; color: #fff; font-size: 11px; font-weight: 600; line-height: 1;
      padding: 4px 8px; border-radius: 0 0 6px 0; pointer-events: none; white-space: nowrap;
    }
    :host(.theme-editor-canvas) [data-block-index]:hover { outline: 1px dashed #60a5fa; outline-offset: -1px; }
    :host(.theme-editor-canvas) .theme-editor-selected-block { outline: 2px solid #2563eb; outline-offset: -2px; }
    /* E3: field-level hover takes precedence over the block-level outline above it (more specific
       selector, and it's the nearer/innermost element anyway) — a finer dashed line signals "this
       exact element", distinct from "this whole block". */
    :host(.theme-editor-canvas) [data-field]:hover { outline: 1px dashed #a78bfa; outline-offset: -1px; }
    :host(.theme-editor-canvas) .theme-editor-selected-field { outline: 2px solid #7c3aed; outline-offset: -2px; }
    .te-toolbar {
      position: absolute; top: 6px; right: 6px; z-index: 31; display: flex; gap: 2px;
      background: #fff; border: 1px solid #e2e8f0; border-radius: 8px; padding: 2px;
      box-shadow: 0 4px 12px rgb(0 0 0 / 0.12);
    }
    .te-toolbar button {
      font-size: 11px; font-weight: 500; color: #334155; padding: 4px 8px; border-radius: 6px;
      background: transparent; border: 0; cursor: pointer; font-family: inherit;
    }
    .te-toolbar button:hover { background: #f1f5f9; }
    .te-toolbar button.te-danger { color: #dc2626; }
    .te-insert {
      display: none; position: absolute; top: 0; left: 50%; transform: translate(-50%, -50%); z-index: 31;
      background: #2563eb; color: #fff; font-size: 11px; font-weight: 600; font-family: inherit;
      padding: 4px 10px; border-radius: 9999px; border: 0; cursor: pointer;
      box-shadow: 0 2px 8px rgb(0 0 0 / 0.2); white-space: nowrap;
    }
    :host(.theme-editor-canvas:hover) .te-insert { display: block; }
  `],
})
export class StorefrontSectionComponent implements OnInit, OnDestroy {
  private readonly catalog = inject(CatalogService);
  private readonly recentlyViewed = inject(RecentlyViewedService);
  private readonly elementRef = inject(ElementRef<HTMLElement>);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));
  private readonly sanitizer = inject(DomSanitizer);
  readonly theme = inject(ThemeService);
  readonly section = input.required<BuilderSection>();

  readonly products = signal<ProductListItem[]>([]);
  readonly categories = signal<Category[]>([]);
  /** Placeholder tiles shown when a section has no catalog data yet (fresh store / preview). */
  readonly ph = [0, 1, 2, 3];

  /** Phase F: TabbedProductGrid — which tab is active, and that tab's fetched products. */
  readonly activeProductTab = signal(0);
  readonly tabProducts = signal<ProductListItem[]>([]);
  readonly tabProductsLoading = signal(false);
  selectProductTab(index: number): void {
    this.activeProductTab.set(index);
    this.loadTabProducts(index);
  }
  private loadTabProducts(index: number): void {
    const b = this.blocks()[index];
    if (!b) { this.tabProducts.set([]); return; }
    this.tabProductsLoading.set(true);
    const count = Number(b['count']) || 8;
    this.catalog.getProducts({
      pageSize: count,
      isFeatured: b['source'] === 'featured' ? true : undefined,
      sort: b['source'] === 'newest' ? 'newest' : b['source'] === 'bestsellers' ? 'bestsellers' : undefined,
      categoryId: b['source'] === 'category' && b['categoryId'] ? Number(b['categoryId']) : undefined,
    }).subscribe({
      next: (r) => { this.tabProducts.set(r.items); this.tabProductsLoading.set(false); },
      error: () => this.tabProductsLoading.set(false),
    });
  }

  /** E2: live draft/saved data pushed from the theme editor — takes precedence over the
   *  section input so edits render instantly without an iframe reload or host changes. */
  private readonly override = signal<{ settings: string | null; blocks: string | null } | null>(null);
  readonly s = computed<any>(() => this.parse<any>(this.override()?.settings ?? this.section().settings, {}));
  readonly blocks = computed<any[]>(() => this.parse<any[]>(this.override()?.blocks ?? this.section().blocks, []));

  /** CountdownBar: Dd/Hh/Mm/Ss remaining, or null once expired. Computed synchronously in ngOnInit
   *  (works identically server + browser) so SSR output already shows correct numbers; a browser-only
   *  interval then keeps it ticking. */
  readonly remaining = signal<{ days: number; hours: number; mins: number; secs: number } | null>(null);
  private countdownTimer: ReturnType<typeof setInterval> | null = null;

  /** Hero 'carousel'/'banner' styles: which slide is currently in view — drives the active dot for
   *  carousel (via scroll position) and the visible cross-faded slide for banner (set directly). */
  @ViewChild('heroCarousel') heroCarousel?: ElementRef<HTMLElement>;
  readonly heroActiveSlide = signal(0);
  scrollHeroCarousel(dir: 1 | -1): void {
    const el = this.heroCarousel?.nativeElement;
    const card = el?.querySelector<HTMLElement>('[data-hero-card]');
    if (!el || !card) return;
    el.scrollBy({ left: dir * (card.offsetWidth + 12), behavior: 'smooth' });
  }
  scrollHeroCarouselTo(index: number): void {
    this.heroCarousel?.nativeElement.querySelectorAll<HTMLElement>('[data-hero-card]')[index]
      ?.scrollIntoView({ behavior: 'smooth', inline: 'start', block: 'nearest' });
  }
  onHeroCarouselScroll(event: Event): void {
    const el = event.target as HTMLElement;
    const card = el.querySelector<HTMLElement>('[data-hero-card]');
    if (!card) return;
    this.heroActiveSlide.set(Math.round(el.scrollLeft / (card.offsetWidth + 12)));
  }

  /** FeaturedProducts 'carousel' layout's own prev/next — same shape as scrollHeroCarousel above,
   *  targeting #prodCarousel instead (shared by the FeaturedProducts @case and the legacy @default
   *  branch — mutually exclusive per instance, same trick as heroCarousel). */
  @ViewChild('prodCarousel') prodCarousel?: ElementRef<HTMLElement>;
  scrollProductCarousel(dir: 1 | -1): void {
    const el = this.prodCarousel?.nativeElement;
    const card = el?.querySelector<HTMLElement>('[data-prod-card]');
    if (!el || !card) return;
    el.scrollBy({ left: dir * (card.offsetWidth + 16), behavior: 'smooth' });
  }

  /** Hero's autoplay/intervalSec settings existed in the schema since Hero shipped but were never
   *  actually read anywhere — the carousel style needed manual arrow/dot clicks or a swipe to see any
   *  slide but the first, and "banner" only ever supported one slide at all (fixed separately, see the
   *  'banner' @case). Applies to both styles now; a single-slide hero is a no-op either way. */
  private heroAutoplayTimer: ReturnType<typeof setInterval> | null = null;
  private startHeroAutoplayIfNeeded(): void {
    if (!this.isBrowser) return;
    const settings = this.s();
    const style = settings['style'];
    if (style !== 'banner' && style !== 'carousel') return;
    if (settings['autoplay'] === false) return;
    const count = this.blocks().length;
    if (count <= 1) return;
    const seconds = Math.max(2, Number(settings['intervalSec']) || 5);
    this.heroAutoplayTimer = setInterval(() => {
      const next = (this.heroActiveSlide() + 1) % count;
      if (style === 'carousel') this.scrollHeroCarouselTo(next);
      else this.heroActiveSlide.set(next);
    }, seconds * 1000);
  }

  /** T15/E1: click-to-select-in-canvas + hover affordances. Only active inside the theme editor's
   *  preview iframe — gated by ThemeService.editorMode() so real shoppers never see any of this. */
  @HostBinding('attr.data-section-id') get sectionIdAttr(): number { return this.section().pageSectionId; }
  /** Editor-mode marker class: all E1 canvas CSS (hover outline, name badge, block outlines,
   *  toolbar/insert visibility) is scoped under it. */
  @HostBinding('class.theme-editor-canvas') get canvasMode(): boolean { return this.theme.editorMode(); }
  /** Name badge content, shown on hover via CSS content: attr(data-section-label). */
  @HostBinding('attr.data-section-label') get sectionLabel(): string | null {
    return this.theme.editorMode() ? (this.section().title || this.section().sectionType) : null;
  }
  /** Signal, not a plain field: this is mutated from a raw window 'message' listener, and the app
   *  runs zoneless — a plain field write there never schedules change detection (the original T15
   *  version had exactly that bug: the selection outline never appeared). */
  readonly highlighted = signal(false);
  @HostBinding('class.theme-editor-selected') get isSelected(): boolean { return this.highlighted(); }
  /** Belt-and-suspenders alongside the mousedown prevention below — some browsers can still
   *  start a drag-selection before a JS handler gets a chance to run. */
  @HostBinding('style.user-select') get editorUserSelect(): string | null { return this.theme.editorMode() ? 'none' : null; }

  /** Our own overlay buttons (toolbar, insert pill) must be exempt from the canvas interceptors,
   *  or their Angular (click) handlers would never fire. */
  private static isToolbarTarget(event: Event): boolean {
    return !!(event.target as HTMLElement).closest('[data-editor-toolbar]');
  }
  /** E6: inspector mode — a merchant-toggled escape hatch that suspends the interceptors below so
   *  real links/Add-to-Cart/etc. work normally inside the canvas, to sanity-check actual storefront
   *  behaviour without leaving the editor. Every section on the page shares the same broadcast
   *  postMessage, so all instances suspend/resume together. */
  private readonly inspectorSuspended = signal(false);

  /** Native double-click/drag word-selection is resolved on mousedown, before any 'click' event
   *  fires — preventDefault() on click alone (below) is too late to stop it. */
  private readonly onEditorMouseDown = (event: MouseEvent) => {
    if (this.inspectorSuspended() || StorefrontSectionComponent.isToolbarTarget(event)) return;
    event.preventDefault();
  };
  private readonly onEditorClick = (event: MouseEvent) => {
    if (this.inspectorSuspended() || StorefrontSectionComponent.isToolbarTarget(event)) return;
    event.preventDefault();
    event.stopPropagation();
    const target = event.target as HTMLElement;
    const blockEl = target.closest('[data-block-index]') as HTMLElement | null;
    const blockIndex = blockEl ? Number(blockEl.getAttribute('data-block-index')) : undefined;
    // E3: the nearest data-field (self-or-ancestor) identifies exactly which setting/block field was
    // clicked, so the editor can jump straight to and focus that one input — "element by element".
    const fieldEl = target.closest('[data-field]') as HTMLElement | null;
    const field = fieldEl ? fieldEl.getAttribute('data-field') : undefined;
    window.parent.postMessage({ type: 'theme-editor:select', sectionId: this.section().pageSectionId, blockIndex, field }, window.location.origin);
  };
  private readonly onWindowMessage = (event: MessageEvent) => {
    if (event.origin !== window.location.origin) return;
    const data = event.data;
    if (data?.type === 'theme-editor:highlight') {
      const mine = data.sectionId === this.section().pageSectionId;
      this.highlighted.set(mine);
      this.setSelectionHighlight(
        mine && typeof data.blockIndex === 'number' ? data.blockIndex : null,
        mine && typeof data.field === 'string' ? data.field : null,
      );
    } else if (data?.type === 'theme-editor:update-section' && data.sectionId === this.section().pageSectionId) {
      this.override.set({ settings: data.settings ?? null, blocks: data.blocks ?? null });
    } else if (data?.type === 'theme-editor:inspector') {
      this.inspectorSuspended.set(!!data.enabled);
    }
  };

  /** E1/E3: outline the selected block and/or field in-canvas. Direct DOM class toggling (not a
   *  binding) because the target elements are arbitrary per-section markup identified only by
   *  data-block-index/data-field. A field lookup is scoped to the selected block's subtree (not the
   *  whole section) since repeated blocks reuse the same field keys — e.g. every Multicolumn block
   *  has its own data-field="heading", so an unscoped query would always hit the first one. */
  private setSelectionHighlight(blockIndex: number | null, field: string | null): void {
    const host = this.elementRef.nativeElement as HTMLElement;
    host.querySelectorAll('.theme-editor-selected-block').forEach((el) => el.classList.remove('theme-editor-selected-block'));
    host.querySelectorAll('.theme-editor-selected-field').forEach((el) => el.classList.remove('theme-editor-selected-field'));
    const blockEl = blockIndex !== null ? host.querySelector(`[data-block-index="${blockIndex}"]`) : null;
    blockEl?.classList.add('theme-editor-selected-block');
    if (field) (blockEl ?? host).querySelector(`[data-field="${field}"]`)?.classList.add('theme-editor-selected-field');
  }

  postAction(action: 'toggleHide' | 'duplicate' | 'delete'): void {
    window.parent.postMessage({ type: 'theme-editor:action', sectionId: this.section().pageSectionId, action }, window.location.origin);
  }
  postInsert(): void {
    window.parent.postMessage({ type: 'theme-editor:insert', beforeSectionId: this.section().pageSectionId }, window.location.origin);
  }

  ngOnInit(): void {
    const type = this.section().sectionType;
    if (type === 'Categories') {
      this.catalog.getCategories().subscribe((c) => this.categories.set(c.slice(0, 12)));
    } else if (type === 'FeaturedProducts' || type === 'ProductGrid') {
      const cfg: Record<string, any> = this.s();
      const count = Number(cfg['count']) || 8;
      if (cfg['source'] === 'collection' && cfg['collectionId']) {
        this.catalog.getCollectionMembers(Number(cfg['collectionId']), count).subscribe((items) => this.products.set(items));
      } else {
        this.catalog.getProducts({
          pageSize: count,
          isFeatured: cfg['source'] === 'featured' ? true : undefined,
          sort: cfg['source'] === 'newest' ? 'newest' : cfg['source'] === 'bestsellers' ? 'bestsellers' : undefined,
          categoryId: cfg['source'] === 'category' && cfg['categoryId'] ? Number(cfg['categoryId']) : undefined,
        }).subscribe((r) => this.products.set(r.items));
      }
    } else if (type === 'RecentlyViewed') {
      const ids = this.recentlyViewed.getIds();
      const count = Number(this.s()['count']) || 8;
      if (ids.length) {
        const wanted = ids.slice(0, count);
        this.catalog.getProducts({ ids: wanted }).subscribe((r) => {
          // Preserve most-recent-first order — the API doesn't guarantee result order for an id-list filter.
          const rank = new Map(wanted.map((id, i) => [id, i]));
          this.products.set([...r.items].sort((a, b) => (rank.get(a.productId) ?? 0) - (rank.get(b.productId) ?? 0)));
        });
      }
    } else if (type === 'TabbedProductGrid') {
      this.loadTabProducts(0);
    } else if (type === 'CountdownBar') {
      this.tickCountdown();
      if (this.isBrowser) this.countdownTimer = setInterval(() => this.tickCountdown(), 1000);
    } else if (type === 'Hero') {
      this.startHeroAutoplayIfNeeded();
    }

    // Capture phase, not bubble: routerLink/href navigation must be intercepted before it fires,
    // not after — a bubble-phase listener on this host would run too late (RouterLink's own click
    // handler lives on the anchor itself and triggers navigation imperatively, not via the
    // browser's deferred default action, so preventDefault() from an ancestor bubble listener
    // wouldn't stop it).
    if (typeof window !== 'undefined' && this.theme.editorMode()) {
      this.elementRef.nativeElement.addEventListener('mousedown', this.onEditorMouseDown, { capture: true });
      this.elementRef.nativeElement.addEventListener('click', this.onEditorClick, { capture: true });
      window.addEventListener('message', this.onWindowMessage);
      // Tell the editor this section's listener is live. Every reload (Hide/Duplicate/Delete/
      // Reorder/Add section/theme-settings save) tears down and replaces this whole iframe document,
      // so a selection made before the reload is otherwise lost forever — the editor has no way to
      // know WHEN the new document is ready to receive a highlight message again. A blind setTimeout
      // guess is what T15 originally shipped with and it silently drops the message half the time.
      // This handshake removes the guesswork: whichever section mounts first announces readiness,
      // and the editor re-sends the current selection in response — see onWindowMessage's
      // 'theme-editor:ready' case in admin-theme-editor.component.ts.
      window.parent.postMessage({ type: 'theme-editor:ready' }, window.location.origin);
    }
  }

  ngOnDestroy(): void {
    if (this.countdownTimer) clearInterval(this.countdownTimer);
    if (this.heroAutoplayTimer) clearInterval(this.heroAutoplayTimer);
    if (typeof window === 'undefined') return;
    this.elementRef.nativeElement.removeEventListener('mousedown', this.onEditorMouseDown, { capture: true });
    this.elementRef.nativeElement.removeEventListener('click', this.onEditorClick, { capture: true });
    window.removeEventListener('message', this.onWindowMessage);
  }

  private tickCountdown(): void {
    const target = new Date(this.s()['endDateTime'] ?? '').getTime();
    const diff = target - Date.now();
    if (!target || diff <= 0) {
      this.remaining.set(null);
      if (this.countdownTimer) { clearInterval(this.countdownTimer); this.countdownTimer = null; }
      return;
    }
    const secs = Math.floor(diff / 1000);
    this.remaining.set({ days: Math.floor(secs / 86400), hours: Math.floor(secs / 3600) % 24, mins: Math.floor(secs / 60) % 60, secs: secs % 60 });
  }

  stars(n: number): string { const r = Math.max(0, Math.min(5, Math.round(n || 0))); return '★★★★★'.slice(0, r) + '☆☆☆☆☆'.slice(0, 5 - r); }

  /** FaqAccordion: which items are expanded (first one open by default). */
  private readonly openFaqIndices = signal<Set<number>>(new Set([0]));

  /** ImageGallery: which block index (if any) is open in the full-size lightbox. */
  readonly galleryLightboxIndex = signal<number | null>(null);
  openGalleryLightbox(index: number): void { this.galleryLightboxIndex.set(index); }
  closeGalleryLightbox(): void { this.galleryLightboxIndex.set(null); }
  stepGalleryLightbox(dir: 1 | -1): void {
    const n = this.blocks().length;
    if (!n) return;
    this.galleryLightboxIndex.update((i) => (((i ?? 0) + dir) % n + n) % n);
  }

  @HostListener('document:keydown.escape')
  onGalleryEscape(): void {
    if (this.galleryLightboxIndex() !== null) this.closeGalleryLightbox();
  }
  isFaqOpen(index: number): boolean { return this.openFaqIndices().has(index); }
  toggleFaq(index: number): void {
    const next = new Set(this.openFaqIndices());
    next.has(index) ? next.delete(index) : next.add(index);
    this.openFaqIndices.set(next);
  }

  /** VideoSection: YouTube/Vimeo URLs become a sanitized embed src; anything else (e.g. a direct
   *  .mp4) falls through to a plain <video> tag instead. Only our own fixed embed-URL prefix +
   *  a regex-extracted id is ever trusted — never the raw settings string. */
  videoEmbedSrc(url: string | null | undefined): SafeResourceUrl | null {
    if (!url) return null;
    const yt = url.match(/(?:youtube\.com\/watch\?v=|youtu\.be\/|youtube\.com\/embed\/)([\w-]+)/);
    if (yt) return this.sanitizer.bypassSecurityTrustResourceUrl(`https://www.youtube.com/embed/${yt[1]}`);
    const vimeo = url.match(/vimeo\.com\/(\d+)/);
    if (vimeo) return this.sanitizer.bypassSecurityTrustResourceUrl(`https://player.vimeo.com/video/${vimeo[1]}`);
    return null;
  }

  /** Tailwind column classes for TileGrid (full class names so the JIT keeps them). */
  tileCols(): string {
    const c = String(this.s()['columns'] ?? '3');
    return c === '2' ? 'grid-cols-1 sm:grid-cols-2' : c === '4' ? 'grid-cols-2 sm:grid-cols-4' : 'grid-cols-2 sm:grid-cols-3';
  }

  private parse<T>(json: string | null, fallback: T): T {
    try { return json ? JSON.parse(json) : fallback; } catch { return fallback; }
  }
}
