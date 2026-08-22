import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';

/**
 * Settings landing page. Shopify-style: the many settings sub-pages live here as a card grid
 * instead of crowding the sidebar. Keep this list in sync with the settings routes in app.routes.ts.
 */
@Component({
  selector: 'app-admin-settings',
  imports: [RouterLink],
  template: `
    <div class="max-w-4xl mx-auto p-6">
      <h1 class="text-2xl font-bold text-slate-800 mb-1">Settings</h1>
      <p class="text-slate-500 mb-6">Manage your store, payments, shipping and account.</p>

      <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
        @for (s of settings; track s.path) {
          <a [routerLink]="s.path"
             class="flex items-start gap-3 p-4 bg-white border border-slate-200 rounded-xl hover:border-blue-300 hover:shadow-sm transition">
            <span class="text-xl leading-none mt-0.5" aria-hidden="true">{{ s.icon }}</span>
            <span>
              <span class="block font-medium text-slate-800">{{ s.label }}</span>
              <span class="block text-sm text-slate-500">{{ s.desc }}</span>
            </span>
          </a>
        }
      </div>
    </div>
  `,
})
export class AdminSettingsComponent {
  readonly settings = [
    { path: '/admin/store-settings', icon: '🏬', label: 'Store details', desc: 'Business name, contact email, address and GST.' },
    { path: '/admin/payments', icon: '💳', label: 'Payments', desc: 'Payment gateways and Cash on Delivery.' },
    { path: '/admin/shipping', icon: '🚚', label: 'Shipping', desc: 'Rates, zones and pincode serviceability.' },
    { path: '/admin/checkout-settings', icon: '🛒', label: 'Checkout', desc: 'Checkout behaviour and options.' },
    { path: '/admin/policies', icon: '📜', label: 'Policies', desc: 'Refund, privacy, terms and shipping policies.' },
    { path: '/admin/notification-templates', icon: '✉️', label: 'Email & SMS', desc: 'Customer notification templates.' },
    { path: '/admin/staff', icon: '👥', label: 'Staff', desc: 'Team members and their permissions.' },
    { path: '/admin/billing', icon: '🧾', label: 'Plan & billing', desc: 'Subscription plan and invoices.' },
    { path: '/admin/ai', icon: '✨', label: 'AI credits', desc: 'AI usage and credit balance.' },
    { path: '/admin/domain', icon: '🌐', label: 'Custom domain', desc: 'Connect a domain customers recognise.' },
    { path: '/admin/auth-providers', icon: '🔑', label: 'Sign-in methods', desc: 'Email, mobile OTP and Google login.' },
    { path: '/admin/developer', icon: '🔌', label: 'API & webhooks', desc: 'API keys and webhook subscriptions for integrations.' },
    { path: '/admin/support', icon: '💬', label: 'Support', desc: 'Get help from the platform team.' },
  ];
}
