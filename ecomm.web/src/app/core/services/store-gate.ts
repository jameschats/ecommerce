/** LocalStorage flag set once a visitor passes the pre-launch password gate. */
export const STORE_UNLOCK_KEY = 'store_unlocked';

/**
 * Routes the pre-launch gate must never block: the merchant console, auth, and the gate itself.
 * (Shopify keeps admin on a separate domain; here it shares the app, so exempt it explicitly.)
 */
const EXEMPT_PREFIXES = ['/password', '/admin', '/superadmin', '/login', '/register', '/forgot-password', '/signup'];

export function isGateExempt(url: string): boolean {
  const path = url.split('?')[0].split('#')[0];
  return EXEMPT_PREFIXES.some((p) => path === p || path.startsWith(p + '/'));
}
