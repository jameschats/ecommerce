import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input, output, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { QuickOrderLine, QuickOrderService } from '../../core/services/quick-order.service';
import { QuickOrderCheckoutService } from '../../core/services/quick-order-checkout.service';

/**
 * The estimate drawer — a right-side slide-in listing the ordered lines (design.md §6).
 *
 * Edits here and edits in the table are the same state, because both read and write
 * QuickOrderService. There is no second copy to keep in sync.
 */
@Component({
  selector: 'app-estimate-drawer',
  standalone: true,
  imports: [CurrencyPipe, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (open()) {
      <!-- Backdrop -->
      <div class="fixed inset-0 bg-slate-900/40 z-40" (click)="closed.emit()" aria-hidden="true"></div>

      <aside
        class="fixed top-0 right-0 bottom-0 w-full sm:w-[420px] bg-white z-50 shadow-2xl flex flex-col"
        role="dialog" aria-label="Your estimate">

        <header class="flex items-center gap-3 px-4 py-3 bg-primary text-white shrink-0">
          <h2 class="font-semibold flex-1">Your estimate</h2>
          <span class="text-sm text-white/80">{{ lineCount() }} item{{ lineCount() === 1 ? '' : 's' }}</span>
          <button type="button" (click)="closed.emit()" aria-label="Close estimate"
                  class="w-8 h-8 grid place-items-center rounded hover:bg-white/15 text-xl leading-none">×</button>
        </header>

        <div class="flex-1 overflow-y-auto">
          @if (!lineCount()) {
            <div class="py-16 px-6 text-center">
              <p class="font-medium text-slate-700">Nothing added yet.</p>
              <p class="text-sm text-slate-500 mt-1">Enter a quantity against any item in the price list.</p>
            </div>
          } @else {
            <ul class="divide-y divide-slate-100">
              @for (line of lines(); track line.item.productId + ':' + (line.variantId ?? '')) {
                <li class="flex gap-3 p-3">
                  @if (line.item.imageUrl) {
                    <img [src]="line.item.imageUrl" [alt]="line.item.name" width="48" height="48" loading="lazy"
                         class="w-12 h-12 rounded object-cover border border-slate-200 shrink-0" />
                  } @else {
                    <div class="w-12 h-12 rounded border border-dashed border-slate-200 bg-slate-50 shrink-0"></div>
                  }

                  <div class="flex-1 min-w-0">
                    <p class="font-medium text-slate-800 text-sm leading-snug">{{ line.item.name }}</p>
                    @if (line.variantLabel) {
                      <p class="text-xs text-primary mt-0.5">{{ line.variantLabel }}</p>
                    }
                    <p class="text-xs text-slate-500 font-mono mt-0.5">{{ line.item.sku }}</p>

                    <div class="mt-1.5 flex items-center gap-2">
                      <input type="number" min="0" step="1" inputmode="numeric"
                             [value]="line.qty"
                             (input)="setQty(line, $any($event.target).value)"
                             [attr.aria-label]="'Quantity for ' + line.item.name"
                             class="w-16 h-8 text-center rounded border border-slate-300 text-sm font-semibold
                                    [appearance:textfield] [&::-webkit-outer-spin-button]:appearance-none
                                    [&::-webkit-inner-spin-button]:appearance-none" />
                      <span class="text-xs text-slate-500">× ₹{{ line.item.price | number: '1.2-2' }}</span>
                      <span class="ml-auto font-semibold text-slate-900 text-sm">
                        ₹{{ line.lineTotal | number: '1.2-2' }}
                      </span>
                    </div>
                  </div>

                  <button type="button" (click)="remove(line)"
                          [attr.aria-label]="'Remove ' + line.item.name"
                          class="text-slate-400 hover:text-red-600 shrink-0 self-start text-lg leading-none">×</button>
                </li>
              }
            </ul>
          }

          <!-- Minimum order by state — lifted from the reference, and genuinely useful
               to a wholesale buyer deciding whether their basket qualifies. -->
          @if (stateMins().length) {
            <div class="p-4 border-t border-slate-100 mt-2">
              <h3 class="text-sm font-semibold text-slate-800 mb-2">Minimum order by state</h3>
              <dl class="text-sm divide-y divide-slate-100 rounded-lg border border-slate-200 overflow-hidden">
                @for (s of stateMins(); track s.stateName) {
                  <div class="flex justify-between px-3 py-1.5">
                    <dt class="text-slate-600">{{ s.stateName }}</dt>
                    <dd class="font-medium text-slate-800">₹{{ s.minOrderAmount | number: '1.2-2' }}</dd>
                  </div>
                }
              </dl>
            </div>
          }
        </div>

        @if (lineCount()) {
          <footer class="border-t border-slate-200 p-4 shrink-0 bg-white">
            <dl class="space-y-1 text-sm">
              <div class="flex justify-between">
                <dt class="text-slate-500">Items Total</dt>
                <dd class="text-slate-700 font-medium">{{ totalUnits() }}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-slate-500">Net Total</dt>
                <dd class="text-slate-600">{{ netTotal() | currency: 'INR' : 'symbol-narrow' : '1.2-2' }}</dd>
              </div>
              <div class="flex justify-between">
                <dt class="text-slate-500">Discount Total</dt>
                <dd class="text-emerald-600 font-medium">
                  − {{ discountTotal() | currency: 'INR' : 'symbol-narrow' : '1.2-2' }}
                </dd>
              </div>
              <div class="flex justify-between pt-1 border-t border-slate-100">
                <dt class="font-semibold text-slate-800">Sub Total</dt>
                <dd class="font-bold text-lg text-slate-900">
                  {{ subTotal() | currency: 'INR' : 'symbol-narrow' : '1.2-2' }}
                </dd>
              </div>
            </dl>

            <button type="button" (click)="confirmed.emit()"
                    class="mt-3 w-full bg-primary hover:bg-primary-dark text-white font-semibold py-3 rounded-lg transition">
              Confirm estimate
            </button>

            <!-- For a dealer quoting their own customer: the same basket on paper, without
                 having to place an order to get a printable copy of it. -->
            <button type="button" (click)="downloadQuote()" [disabled]="downloading()"
                    class="mt-2 w-full flex items-center justify-center gap-2 border border-slate-300
                           hover:bg-slate-50 disabled:opacity-60 text-slate-700 font-medium py-2.5 rounded-lg transition">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"
                   stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M12 3v12" /><path d="m7 10 5 5 5-5" /><path d="M5 21h14" />
              </svg>
              {{ downloading() ? 'Preparing…' : 'Download quotation (PDF)' }}
            </button>
            @if (quoteError()) { <p class="mt-1 text-xs text-red-600 text-center">{{ quoteError() }}</p> }

            <button type="button" (click)="clearAll()"
                    class="mt-2 w-full text-sm text-slate-500 hover:text-red-600 py-1">
              Clear all items
            </button>
          </footer>
        }
      </aside>
    }
  `,
})
export class EstimateDrawerComponent {
  private readonly quickOrder = inject(QuickOrderService);
  private readonly checkout = inject(QuickOrderCheckoutService);

  readonly open = input(false);
  readonly closed = output<void>();
  readonly confirmed = output<void>();

  readonly lines = this.quickOrder.lines;
  readonly lineCount = this.quickOrder.lineCount;
  readonly totalUnits = this.quickOrder.totalUnits;
  readonly netTotal = this.quickOrder.netTotal;
  readonly discountTotal = this.quickOrder.discountTotal;
  readonly subTotal = this.quickOrder.subTotal;

  private readonly config = toSignal(this.checkout.getConfig(), { initialValue: null });
  readonly stateMins = computed(() => this.config()?.stateMinOrders ?? []);

  setQty(line: QuickOrderLine, value: string): void {
    const parsed = value === '' ? 0 : Number.parseInt(value, 10);
    const qty = Number.isNaN(parsed) ? 0 : parsed;
    if (line.variantId != null) {
      this.quickOrder.setVariantLineQty(line.item.productId, line.variantId, qty);
    } else {
      this.quickOrder.setQty(line.item.productId, qty);
    }
  }

  remove(line: QuickOrderLine): void {
    this.setQty(line, '0');
  }

  clearAll(): void {
    this.quickOrder.clear();
    this.closed.emit();
  }

  readonly downloading = signal(false);
  readonly quoteError = signal<string | null>(null);

  /**
   * Renders the basket as a quotation PDF.
   *
   * Built on the server rather than printed from the browser: the totals include packing
   * and rounding that only the server computes, and a print stylesheet would produce a
   * screenshot of a drawer rather than a document anyone would send a customer.
   */
  downloadQuote(): void {
    if (!this.lineCount()) return;
    this.downloading.set(true);
    this.quoteError.set(null);

    const lines = this.lines().map((l) => ({ productId: l.item.productId, quantity: l.qty, variantId: l.variantId ?? undefined }));

    this.checkout.quotePdf(lines).subscribe({
      next: (blob) => {
        this.downloading.set(false);
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `quotation-${new Date().toISOString().slice(0, 10)}.pdf`;
        a.click();
        URL.revokeObjectURL(url);
      },
      error: () => {
        this.downloading.set(false);
        this.quoteError.set('Could not prepare the quotation. Please try again.');
      },
    });
  }
}
