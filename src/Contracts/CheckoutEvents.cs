namespace Contracts;

// Integration events for the checkout saga. Versioned by namespace/name; payloads
// are intentionally small. These are the contract shared across all three services.

public sealed record OrderPlaced(Guid OrderId, string Sku, int Quantity, decimal Amount) : IntegrationEvent;

// Carries Amount downstream so Payments can charge without re-fetching the order
// (events carry what consumers need).
public sealed record StockReserved(Guid OrderId, string Sku, int Quantity, decimal Amount) : IntegrationEvent;

public sealed record StockReservationFailed(Guid OrderId, string Sku, string Reason) : IntegrationEvent;

public sealed record StockReleased(Guid OrderId, string Sku, int Quantity) : IntegrationEvent;

public sealed record PaymentCharged(Guid OrderId, decimal Amount) : IntegrationEvent;

public sealed record PaymentFailed(Guid OrderId, string Reason) : IntegrationEvent;

public sealed record OrderConfirmed(Guid OrderId) : IntegrationEvent;

public sealed record OrderCancelled(Guid OrderId, string Reason) : IntegrationEvent;
