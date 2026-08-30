using System;
using System.Threading.Tasks;
using UnityEngine;

namespace ImmortalLoot.Payment
{
    public sealed class PaymentRequest
    {
        public string OrderNo { get; }
        public string ProductId { get; }
        public PaymentRequest(string orderNo, string productId) { OrderNo = orderNo; ProductId = productId; }
    }

    public sealed class PlatformPaymentResult
    {
        public bool Succeeded { get; }
        public string Provider { get; }
        public string Receipt { get; }
        public string Error { get; }
        public PlatformPaymentResult(bool succeeded, string provider, string receipt, string error)
        { Succeeded = succeeded; Provider = provider; Receipt = receipt; Error = error; }
    }

    public interface IPaymentProvider
    {
        Task<PlatformPaymentResult> PurchaseAsync(PaymentRequest request);
    }

    public sealed class MockPaymentProvider : IPaymentProvider
    {
        private readonly bool _succeed;
        private readonly bool _allowMock;
        public MockPaymentProvider(bool succeed = true, bool? allowMock = null)
        {
            _succeed = succeed;
            _allowMock = allowMock ?? Debug.isDebugBuild;
        }

        public Task<PlatformPaymentResult> PurchaseAsync(PaymentRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.OrderNo) || string.IsNullOrWhiteSpace(request.ProductId))
                throw new ArgumentException("A server-created order and product are required.");
            if (!_allowMock) return Task.FromResult(new PlatformPaymentResult(false, "mock", string.Empty, "Mock payment is available only in Development builds."));
            if (!_succeed) return Task.FromResult(new PlatformPaymentResult(false, "mock", string.Empty, "Mock cancellation"));
            var receipt = "mock-receipt:" + request.OrderNo + ":" + request.ProductId;
            return Task.FromResult(new PlatformPaymentResult(true, "mock", receipt, string.Empty));
        }
    }

    // Platform implementations only acquire signed receipts. Currency and items are
    // never granted here; the receipt must be sent to the authoritative server.
    public sealed class GooglePlayPaymentProvider : IPaymentProvider
    {
        public Task<PlatformPaymentResult> PurchaseAsync(PaymentRequest request) => throw new NotSupportedException("Google Play Billing is not configured in the MVP.");
    }

    public sealed class DouyinPaymentProvider : IPaymentProvider
    {
        public Task<PlatformPaymentResult> PurchaseAsync(PaymentRequest request) => throw new NotSupportedException("Douyin payment is not configured in the MVP.");
    }
}
