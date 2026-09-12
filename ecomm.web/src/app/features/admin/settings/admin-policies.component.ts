import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { PolicyAdminService, StorePolicy } from '../../../core/services/policy-admin.service';
import { ThemeService } from '../../../core/services/theme.service';

/** Generic starter text per policy handle — a merchant starting point, not legal advice. Only ever
 *  shown locally in the editor textarea; nothing is saved until the merchant reviews and hits Save,
 *  so an unfilled policy still reads as "empty" everywhere else (footer, storefront) until then. */
const POLICY_STARTERS: Record<string, (store: string) => string> = {
  refund: (store) => `<h2>Return window</h2>
<p>You can request a return within [7] days of delivery. Items must be unused, in their original packaging, with all tags attached.</p>
<h2>How to start a return</h2>
<p>Go to <strong>My Orders</strong>, select the order and item, and choose "Return". We'll arrange a pickup or share drop-off instructions.</p>
<h2>Refunds</h2>
<p>Once we receive and inspect the returned item, we'll process your refund to the original payment method within [5–7] business days.</p>
<h2>Non-returnable items</h2>
<p>[List anything that can't be returned — e.g. innerwear, customized products, perishable goods.]</p>
<h2>Damaged or wrong item</h2>
<p>If you received a damaged, defective, or incorrect item, contact us within [48 hours] of delivery and we'll arrange a replacement or full refund at no extra cost.</p>`,

  privacy: (store) => `<h2>What we collect</h2>
<p>When you shop with ${store}, we collect information such as your name, email, phone number, shipping address, and order history to process your orders and provide support.</p>
<h2>How we use it</h2>
<ul>
<li>To process and deliver your orders</li>
<li>To send order updates and support responses</li>
<li>To improve our products and website</li>
<li>To send offers and updates, only if you've opted in</li>
</ul>
<h2>Sharing</h2>
<p>We share your information only with service providers who help us run our store (payment processors, shipping partners) — we never sell it to third parties.</p>
<h2>Cookies</h2>
<p>We use cookies to keep your cart working, remember your preferences, and understand how our site is used.</p>
<h2>Your rights</h2>
<p>You can request access to, correction of, or deletion of your personal data at any time by contacting us at [support email].</p>`,

  terms: (store) => `<h2>Using our store</h2>
<p>By placing an order with ${store}, you agree to these terms. Please read them carefully before shopping with us.</p>
<h2>Orders &amp; pricing</h2>
<p>All prices are listed in INR and include applicable taxes unless stated otherwise. We reserve the right to correct pricing errors and cancel affected orders with a full refund.</p>
<h2>Payments</h2>
<p>We accept the payment methods shown at checkout. Your order is confirmed once payment is successfully processed.</p>
<h2>Limitation of liability</h2>
<p>${store} is not liable for indirect or incidental damages arising from the use of our products or website, to the extent permitted by law.</p>
<h2>Governing law</h2>
<p>These terms are governed by the laws of [your state/country], and any disputes will be handled in the courts of [your city].</p>`,

  shipping: (store) => `<h2>Processing time</h2>
<p>Orders are processed within [1–2] business days of confirmation.</p>
<h2>Delivery time</h2>
<p>Once shipped, delivery typically takes [3–7] business days depending on your location.</p>
<h2>Shipping charges</h2>
<p>[Free shipping over ₹X / a flat rate of ₹Y] — shown at checkout before you pay.</p>
<h2>Tracking</h2>
<p>You'll receive a tracking link by [email / SMS / WhatsApp] as soon as your order ships.</p>
<h2>Delays &amp; undeliverable orders</h2>
<p>Delivery times may vary due to courier delays, weather, or address issues. If a delivery attempt fails, our courier partner will attempt redelivery or contact you to reschedule.</p>`,

  contact: (store) => `<h2>Get in touch</h2>
<p>Have a question about your order or our products? We're happy to help.</p>
<ul>
<li><strong>Email:</strong> [support email]</li>
<li><strong>Phone:</strong> [support phone]</li>
<li><strong>Hours:</strong> [e.g. Mon–Sat, 9:30 AM – 6:30 PM]</li>
<li><strong>Address:</strong> [registered business address]</li>
</ul>
<p>You can also reach us anytime using the form on our <a href="/contact">Contact page</a>.</p>`,

  legal: (store) => `<h2>Business details</h2>
<ul>
<li><strong>Legal name:</strong> [registered business name]</li>
<li><strong>GSTIN:</strong> [your GSTIN]</li>
<li><strong>Registered address:</strong> [address]</li>
</ul>
<h2>Disclaimer</h2>
<p>All content on ${store}'s website — including text, images, and branding — is owned by us or our licensors and may not be reused without permission.</p>
<p>We aim to keep product information accurate, but errors may occasionally occur. We reserve the right to correct such errors at any time.</p>`,
};

@Component({
  selector: 'app-admin-policies',
  imports: [FormsModule],
  template: `
    <div class="max-w-3xl mx-auto p-6">
      <h1 class="text-xl font-bold text-slate-900 mb-1">Policies</h1>
      <p class="text-sm text-slate-500 mb-4">Your store's legal pages. Ones you fill in are shown on the storefront and linked in the footer.</p>
      @if (message()) { <div class="mb-4 rounded-lg bg-green-50 border border-green-200 text-green-700 text-sm px-3 py-2">{{ message() }}</div> }

      <div class="grid sm:grid-cols-2 gap-6">
        <!-- list -->
        <div class="bg-white border border-slate-200 rounded-xl divide-y divide-slate-100 self-start">
          @for (p of policies(); track p.handle) {
            <button type="button" (click)="pick(p)" class="w-full text-left px-4 py-3 hover:bg-slate-50 flex items-center justify-between"
              [class.bg-slate-50]="active()?.handle === p.handle">
              <span class="text-sm font-medium text-slate-800">{{ p.title }}</span>
              @if (p.hasContent) { <span class="text-xs text-green-600">✓</span> } @else { <span class="text-xs text-slate-300">empty</span> }
            </button>
          }
        </div>

        <!-- editor -->
        @if (active(); as p) {
          <div class="bg-white border border-slate-200 rounded-xl p-5">
            <label class="lbl">Title</label>
            <input class="input mb-3" [(ngModel)]="title" />
            <label class="lbl">Content (HTML — scripts are stripped)</label>
            @if (!p.hasContent) { <p class="text-xs text-violet-600 mb-1">Starter template below — edit it to match your store, then save.</p> }
            <textarea class="input font-mono text-xs" rows="12" [(ngModel)]="body" placeholder="<p>Your policy…</p>"></textarea>
            <button type="button" (click)="save()" [disabled]="saving()" class="btn-primary mt-3">{{ saving() ? 'Saving…' : 'Save policy' }}</button>
          </div>
        } @else {
          <div class="text-slate-400 text-sm p-6 text-center border border-dashed border-slate-200 rounded-xl">Pick a policy to edit.</div>
        }
      </div>
    </div>
  `,
})
export class AdminPoliciesComponent implements OnInit {
  private readonly api = inject(PolicyAdminService);
  private readonly theme = inject(ThemeService);
  readonly policies = signal<StorePolicy[]>([]);
  readonly active = signal<StorePolicy | null>(null);
  readonly saving = signal(false);
  readonly message = signal<string | null>(null);
  title = '';
  body = '';

  ngOnInit(): void { this.load(); }
  private load(): void { this.api.list().subscribe((p) => this.policies.set(p)); }

  pick(p: StorePolicy): void {
    this.active.set(p);
    this.title = p.title;
    this.body = p.bodyHtml ?? POLICY_STARTERS[p.handle]?.(this.theme.storeName() || 'our store') ?? '';
  }

  save(): void {
    const p = this.active(); if (!p) return;
    this.saving.set(true);
    this.api.save(p.handle, this.title, this.body).subscribe({
      next: () => { this.saving.set(false); this.message.set('Policy saved.'); setTimeout(() => this.message.set(null), 2500); this.load(); },
      error: () => this.saving.set(false),
    });
  }
}
