/** Tailwind classes for a merchant-standing badge. */
export function standingClass(s: string): string {
  return s === 'Blacklisted' ? 'bg-red-50 text-red-700 border border-red-200'
    : s === 'Flagged' || s === 'Watch' ? 'bg-amber-50 text-amber-700 border border-amber-200'
    : s === 'Trusted' ? 'bg-blue-50 text-blue-700 border border-blue-200'
    : 'bg-green-50 text-green-700 border border-green-200';
}
