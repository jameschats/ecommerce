import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';

@Component({
  selector: 'app-account-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <section class="page-container py-8">
      <h1 class="text-xl font-bold text-slate-900 mb-5">My account</h1>
      <div class="grid md:grid-cols-[220px_1fr] gap-6 items-start">
        <nav class="bg-white rounded-xl border border-slate-200 p-2">
          <a routerLink="/account/profile" routerLinkActive="bg-primary/10 text-primary font-medium" class="block px-3 py-2 rounded-lg text-sm text-slate-700 hover:bg-slate-50">Profile</a>
          <a routerLink="/account/addresses" routerLinkActive="bg-primary/10 text-primary font-medium" class="block px-3 py-2 rounded-lg text-sm text-slate-700 hover:bg-slate-50">Addresses</a>
          <a routerLink="/account/orders" routerLinkActive="bg-primary/10 text-primary font-medium" class="block px-3 py-2 rounded-lg text-sm text-slate-700 hover:bg-slate-50">Orders</a>
          <a routerLink="/account/wishlist" routerLinkActive="bg-primary/10 text-primary font-medium" class="block px-3 py-2 rounded-lg text-sm text-slate-700 hover:bg-slate-50">Wishlist</a>
        </nav>
        <div class="min-w-0">
          <router-outlet />
        </div>
      </div>
    </section>
  `,
})
export class AccountLayoutComponent {}
