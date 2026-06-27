/** Tailwind classes for an order/payment status badge. */
export function orderStatusClass(status: string): string {
  switch (status) {
    case 'Paid':
    case 'Delivered':
    case 'Success':
      return 'bg-green-100 text-green-700';
    case 'Packed':
    case 'Shipped':
      return 'bg-blue-100 text-blue-700';
    case 'Pending':
      return 'bg-amber-100 text-amber-700';
    case 'Cancelled':
    case 'Failed':
    case 'Returned':
      return 'bg-red-100 text-red-700';
    case 'Refunded':
      return 'bg-purple-100 text-purple-700';
    default:
      return 'bg-slate-100 text-slate-600';
  }
}
