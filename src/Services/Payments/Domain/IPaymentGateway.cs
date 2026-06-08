using Microsoft.Extensions.Options;

namespace Services.Payments.Domain;

public interface IPaymentGateway
{
    bool Authorize(decimal amount);
}

public sealed class PaymentOptions
{
    /// <summary>Charges above this amount are declined. Deterministic, so the saga's
    /// happy path and its compensation path are both reproducible in tests.</summary>
    public decimal DeclineAboveAmount { get; set; } = 1000m;
}

/// <summary>Stand-in for a real PSP adapter (Stripe/Iugu/etc.). Declines by a fixed rule.</summary>
public sealed class FakePaymentGateway(IOptions<PaymentOptions> options) : IPaymentGateway
{
    public bool Authorize(decimal amount) => amount <= options.Value.DeclineAboveAmount;
}
