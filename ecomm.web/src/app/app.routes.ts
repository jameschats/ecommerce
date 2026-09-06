import { Routes } from '@angular/router';
import { adminGuard, authGuard } from './core/guards/auth.guard';
import { contentPageResolver } from './core/resolvers/content-page.resolver';
import { pageBannerResolver } from './core/resolvers/page-banner.resolver';
import { galleryResolver } from './features/home/gallery.resolver';
import { homeResolver } from './features/home/home.resolver';
import { testimonialsResolver } from './features/home/testimonials.resolver';

export const routes: Routes = [
  {
    path: '',
    resolve: {
      home: homeResolver,
      gallery: galleryResolver('new-designs', 'New designs'),
      secondGallery: galleryResolver('featured', 'Our Work'),
      homeAbout: contentPageResolver('about'),
      testimonials: testimonialsResolver,
    },
    loadComponent: () => import('./features/home/home.component').then((m) => m.HomeComponent),
  },
  {
    // "Order Now" — the quick-order price list without the home page's marketing.
    path: 'order',
    resolve: { pageBanners: pageBannerResolver('order') },
    loadComponent: () => import('./features/order/order.component').then((m) => m.OrderComponent),
  },
  {
    // Finished Calendar — the same table scoped to the one category that is kept out of
    // the main price list. Declared above 'order/:orderId/pay' is not required, but it
    // sits next to /order because it is the same screen with a different scope.
    path: 'finished-calendar',
    resolve: { pageBanners: pageBannerResolver('finished-calendar') },
    loadComponent: () =>
      import('./features/order/finished-calendar.component').then((m) => m.FinishedCalendarComponent),
  },
  {
    // Payment instructions for a placed order — UPI QR + bank details (design.md §8).
    path: 'order/:orderId/pay',
    canActivate: [authGuard],
    loadComponent: () => import('./features/order/payment.component').then((m) => m.PaymentComponent),
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
      { path: 'wishlist', loadComponent: () => import('./features/account/wishlist.component').then((m) => m.WishlistComponent) },
      { path: 'notifications', loadComponent: () => import('./features/notifications/notifications-page.component').then((m) => m.NotificationsPageComponent) },
    ],
  },
  {
    path: 'about',
    resolve: { pageBanners: pageBannerResolver('about'), page: contentPageResolver('about') },
    loadComponent: () => import('./features/pages/about/about.component').then((m) => m.AboutComponent),
  },
  {
    path: 'contact',
    resolve: { page: contentPageResolver('contact') },
    loadComponent: () => import('./features/pages/contact/contact.component').then((m) => m.ContactComponent),
  },
  {
    path: 'faq',
    resolve: { page: contentPageResolver('faq') },
    loadComponent: () => import('./features/pages/faq/faq.component').then((m) => m.FaqComponent),
  },
  {
    path: 'buying-guide',
    resolve: { page: contentPageResolver('buying-guide') },
    loadComponent: () => import('./features/pages/buying-guide/buying-guide.component').then((m) => m.BuyingGuideComponent),
  },
  {
    path: 'enquiry',
    loadComponent: () => import('./features/pages/enquiry/enquiry.component').then((m) => m.EnquiryComponent),
  },
  {
    path: 'login',
    loadComponent: () => import('./features/auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'forgot-password',
    loadComponent: () => import('./features/auth/forgot-password/forgot-password.component').then((m) => m.ForgotPasswordComponent),
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
      { path: 'payments', loadComponent: () => import('./features/admin/payments/admin-payments.component').then((m) => m.AdminPaymentsComponent) },
      { path: 'reviews', loadComponent: () => import('./features/admin/reviews/admin-reviews.component').then((m) => m.AdminReviewsComponent) },
      { path: 'coupons', loadComponent: () => import('./features/admin/coupons/admin-coupons.component').then((m) => m.AdminCouponsComponent) },
      { path: 'analytics', loadComponent: () => import('./features/admin/analytics/admin-analytics.component').then((m) => m.AdminAnalyticsComponent) },
      { path: 'suppliers', loadComponent: () => import('./features/admin/suppliers/admin-suppliers.component').then((m) => m.AdminSuppliersComponent) },
      { path: 'contacts', loadComponent: () => import('./features/admin/contacts/admin-contacts.component').then((m) => m.AdminContactsComponent) },
      { path: 'users', loadComponent: () => import('./features/admin/identity/admin-users.component').then((m) => m.AdminUsersComponent) },
      { path: 'abandoned', loadComponent: () => import('./features/admin/abandoned/admin-abandoned.component').then((m) => m.AdminAbandonedComponent) },
      { path: 'campaigns', loadComponent: () => import('./features/admin/campaigns/admin-campaigns.component').then((m) => m.AdminCampaignsComponent) },
      { path: 'templates', loadComponent: () => import('./features/admin/notifications/admin-templates.component').then((m) => m.AdminTemplatesComponent) },
      { path: 'notification-settings', loadComponent: () => import('./features/admin/notifications/admin-notification-settings.component').then((m) => m.AdminNotificationSettingsComponent) },
      { path: 'notifications', loadComponent: () => import('./features/notifications/notifications-page.component').then((m) => m.NotificationsPageComponent) },
      { path: 'store-settings', loadComponent: () => import('./features/admin/settings/admin-store-settings.component').then((m) => m.AdminStoreSettingsComponent) },
      { path: 'shop-settings', loadComponent: () => import('./features/admin/settings/admin-shop-settings.component').then((m) => m.AdminShopSettingsComponent) },
      { path: 'data-reset', loadComponent: () => import('./features/admin/settings/admin-data-reset.component').then((m) => m.AdminDataResetComponent) },
      { path: 'theme', loadComponent: () => import('./features/admin/theme/admin-theme.component').then((m) => m.AdminThemeComponent) },
      { path: 'pages', loadComponent: () => import('./features/admin/pages/admin-pages.component').then((m) => m.AdminPagesComponent) },
      { path: 'home-page', loadComponent: () => import('./features/admin/cms/admin-cms.component').then((m) => m.AdminCmsComponent) },
      { path: 'banners', loadComponent: () => import('./features/admin/cms/admin-banners.component').then((m) => m.AdminBannersComponent) },
      { path: 'gallery', loadComponent: () => import('./features/admin/cms/admin-gallery.component').then((m) => m.AdminGalleryComponent) },
      { path: 'testimonials', loadComponent: () => import('./features/admin/cms/admin-testimonials.component').then((m) => m.AdminTestimonialsComponent) },
      { path: 'import', loadComponent: () => import('./features/admin/catalog/admin-import.component').then((m) => m.AdminImportComponent) },
      { path: 'auth-providers', loadComponent: () => import('./features/admin/auth-providers/auth-providers.component').then((m) => m.AuthProvidersComponent) },
    ],
  },
  { path: '**', redirectTo: '' },
];
