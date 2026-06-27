import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/guards/auth.guard';
import { homeResolver } from './features/home/home.resolver';

export const routes: Routes = [
  {
    path: '',
    resolve: { home: homeResolver },
    loadComponent: () => import('./features/home/home.component').then((m) => m.HomeComponent),
  },
  {
    path: 'products',
    loadComponent: () => import('./features/catalog/product-list/product-list.component').then((m) => m.ProductListComponent),
  },
  {
    path: 'category/:slug',
    loadComponent: () => import('./features/catalog/product-list/product-list.component').then((m) => m.ProductListComponent),
  },
  {
    path: 'product/:slug',
    loadComponent: () => import('./features/catalog/product-detail/product-detail.component').then((m) => m.ProductDetailComponent),
  },
  {
    path: 'cart',
    loadComponent: () => import('./features/cart/cart.component').then((m) => m.CartComponent),
  },
  {
    path: 'checkout',
    canActivate: [authGuard],
    loadComponent: () => import('./features/checkout/checkout.component').then((m) => m.CheckoutComponent),
  },
  {
    path: 'account',
    canActivate: [authGuard],
    loadComponent: () => import('./features/account/account-layout.component').then((m) => m.AccountLayoutComponent),
    children: [
      { path: '', redirectTo: 'profile', pathMatch: 'full' },
      { path: 'profile', loadComponent: () => import('./features/account/profile.component').then((m) => m.ProfileComponent) },
      { path: 'addresses', loadComponent: () => import('./features/account/addresses.component').then((m) => m.AddressesComponent) },
      { path: 'orders', loadComponent: () => import('./features/orders/order-history.component').then((m) => m.OrderHistoryComponent) },
      { path: 'orders/:id', loadComponent: () => import('./features/orders/order-detail.component').then((m) => m.OrderDetailComponent) },
    ],
  },
  { path: 'about', loadComponent: () => import('./features/pages/about/about.component').then((m) => m.AboutComponent) },
  { path: 'contact', loadComponent: () => import('./features/pages/contact/contact.component').then((m) => m.ContactComponent) },
  { path: 'faq', loadComponent: () => import('./features/pages/faq/faq.component').then((m) => m.FaqComponent) },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'register',
    loadComponent: () => import('./features/auth/register/register.component').then((m) => m.RegisterComponent),
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () => import('./features/admin/admin-layout.component').then((m) => m.AdminLayoutComponent),
    children: [
      { path: '', redirectTo: 'products', pathMatch: 'full' },
      { path: 'products', loadComponent: () => import('./features/admin/catalog/admin-products.component').then((m) => m.AdminProductsComponent) },
      { path: 'products/new', loadComponent: () => import('./features/admin/catalog/admin-product-form.component').then((m) => m.AdminProductFormComponent) },
      { path: 'products/:id', loadComponent: () => import('./features/admin/catalog/admin-product-form.component').then((m) => m.AdminProductFormComponent) },
      { path: 'categories', loadComponent: () => import('./features/admin/catalog/admin-categories.component').then((m) => m.AdminCategoriesComponent) },
      { path: 'brands', loadComponent: () => import('./features/admin/catalog/admin-brands.component').then((m) => m.AdminBrandsComponent) },
      { path: 'attributes', loadComponent: () => import('./features/admin/catalog/admin-attributes.component').then((m) => m.AdminAttributesComponent) },
      { path: 'inventory', loadComponent: () => import('./features/admin/inventory/admin-inventory.component').then((m) => m.AdminInventoryComponent) },
      { path: 'orders', loadComponent: () => import('./features/admin/orders/admin-orders.component').then((m) => m.AdminOrdersComponent) },
      { path: 'theme', loadComponent: () => import('./features/admin/theme/admin-theme.component').then((m) => m.AdminThemeComponent) },
      { path: 'home-page', loadComponent: () => import('./features/admin/cms/admin-cms.component').then((m) => m.AdminCmsComponent) },
      { path: 'import', loadComponent: () => import('./features/admin/catalog/admin-import.component').then((m) => m.AdminImportComponent) },
      { path: 'auth-providers', loadComponent: () => import('./features/admin/auth-providers/auth-providers.component').then((m) => m.AuthProvidersComponent) },
    ],
  },
  { path: '**', redirectTo: '' },
];
