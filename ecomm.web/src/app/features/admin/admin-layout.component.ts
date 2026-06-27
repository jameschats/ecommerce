import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-admin-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <div class="min-h-screen flex bg-slate-50">
      <aside class="w-56 bg-white border-r border-slate-200 flex flex-col">
        <div class="h-14 flex items-center px-4 border-b border-slate-200 font-bold text-slate-800">Admin</div>
        <nav class="flex-1 p-3 space-y-1 text-sm">
          @for (l of links; track l.path) {
            <a [routerLink]="l.path" routerLinkActive="bg-blue-50 text-blue-700 font-medium"
               class="block px-3 py-2 rounded-lg text-slate-600 hover:bg-slate-50">{{ l.label }}</a>
          }
        </nav>
        <div class="p-3 border-t border-slate-200 text-sm">
          <a routerLink="/" class="block px-3 py-2 rounded-lg text-slate-500 hover:bg-slate-50">← View store</a>
          <button type="button" (click)="logout()" class="block w-full text-left px-3 py-2 rounded-lg text-slate-500 hover:bg-slate-50">Sign out</button>
        </div>
      </aside>
      <main class="flex-1 overflow-auto">
        <router-outlet />
      </main>
    </div>
  `,
})
export class AdminLayoutComponent {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly links = [
    { path: '/admin/products', label: 'Products' },
    { path: '/admin/categories', label: 'Categories' },
    { path: '/admin/brands', label: 'Brands' },
    { path: '/admin/attributes', label: 'Attributes' },
    { path: '/admin/inventory', label: 'Inventory' },
    { path: '/admin/orders', label: 'Orders' },
    { path: '/admin/theme', label: 'Theme' },
    { path: '/admin/home-page', label: 'Home page' },
    { path: '/admin/import', label: 'Import / Export' },
    { path: '/admin/auth-providers', label: 'Sign-in methods' },
  ];

  logout(): void {
    this.auth.logout();
    this.router.navigateByUrl('/');
  }
}
