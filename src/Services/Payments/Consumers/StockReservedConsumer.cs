using BuildingBlocks.Messaging;
using Contracts;
using Services.Payments.Domain;
using Services.Payments.Infrastructure;

namespace Services.Payments.Consumers;

/// <summary>
/// Reacts to <see cref="StockReserved"/>: charges via the gateway and emits the saga's next
/// event. The charge record and the emitted event ride the inbox transaction, so charge +
/// emit + dedup commit atomically (ADR-001/002).
/// </summary>
public sealed class StockReservedConsumer(PaymentsDbContext db, IOutbox outbox, IPaymentGateway gateway)
    : IIntegrationEventConsumer<StockReserved>
{
    public Task ConsumeAsync(StockReserved message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (gateway.Authorize(message.Amount))
        {
            db.Payments.Add(Payment.Charged(message.OrderId, message.Amount));
            outbox.Add(new PaymentCharged(message.OrderId, message.Amount));
        }
        else
        {
            db.Payments.Add(Payment.Failed(message.OrderId, message.Amount));
            outbox.Add(new PaymentFailed(message.OrderId, "amount-exceeds-limit"));
        }

        return Task.CompletedTask;
    }
}
