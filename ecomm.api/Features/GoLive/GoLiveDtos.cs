namespace ecomm.api.Features.GoLive;

public sealed record GoLiveSummaryDto(
    bool IsLive, DateTime? GoneLiveAt,
    // Always removed
    int Orders, decimal OrdersWorth, int OrderLinesAndStatusHistory, int Invoices, int Payments,
    int Shipments, int ReviewsAndCreditNotes, int StockMovementHistory, int CartsAndWishlists,
    int Notifications, int SignInSessionsAndOtps,
    // "What this also puts right"
    int ReservedUnits, int ProductsWithReservedUnits, string NextInvoicePreview,
    // Also remove (optional)
    int CustomerAccounts, int CustomerAddresses, int TrafficAndSearchHistory, int ImportJobs, int ContactMessages);

public sealed record GoLiveResetRequest(
    bool IncludeCustomers, bool IncludeTraffic, bool IncludeImportHistory, bool IncludeContactMessages,
    string ConfirmationText);

public sealed record GoLiveResetResultDto(
    int OrdersRemoved, int InvoicesRemoved, int PaymentsRemoved, int ShipmentsRemoved,
    int ReviewsAndCreditNotesRemoved, int StockMovementRemoved, int CartsAndWishlistsRemoved,
    int NotificationsRemoved, int SignInSessionsAndOtpsRemoved, int ReservedUnitsReleased,
    int CustomersRemoved, int TrafficRemoved, int ImportJobsRemoved, int ContactMessagesRemoved);
