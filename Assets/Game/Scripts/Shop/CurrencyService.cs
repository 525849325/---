using System;
using System.Collections.Generic;
using ImmortalLoot.Config;

namespace ImmortalLoot.Shop
{
    [Serializable]
    public sealed class CurrencyState
    {
        public long SoftCurrency;
        public long PremiumCurrency;
        public long Version;
        public List<CurrencyTransaction> Transactions = new List<CurrencyTransaction>();
    }

    [Serializable]
    public sealed class CurrencyTransaction
    {
        public string IdempotencyKey;
        public CurrencyType Currency;
        public long Delta;
        public long BalanceAfter;
        public string Reason;
        public DateTime CreatedAtUtc;
    }

    public sealed class CurrencyService
    {
        private readonly CurrencyState _state;
        private readonly Dictionary<string, CurrencyTransaction> _transactions;
        public CurrencyState State => _state;

        public CurrencyService(CurrencyState state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _state.Transactions = _state.Transactions ?? new List<CurrencyTransaction>();
            _transactions = new Dictionary<string, CurrencyTransaction>(StringComparer.Ordinal);
            foreach (var transaction in _state.Transactions)
                if (!string.IsNullOrWhiteSpace(transaction.IdempotencyKey)) _transactions[transaction.IdempotencyKey] = transaction;
        }

        public long Balance(CurrencyType type) => type == CurrencyType.SoftCurrency ? _state.SoftCurrency : _state.PremiumCurrency;

        public CurrencyTransaction Change(CurrencyType type, long delta, string reason, string idempotencyKey, DateTime createdAtUtc)
        {
            if (delta == 0) throw new ArgumentException("Currency delta cannot be zero.");
            if (string.IsNullOrWhiteSpace(idempotencyKey)) throw new ArgumentException("Idempotency key is required.");
            if (_transactions.TryGetValue(idempotencyKey, out var replay)) return replay;
            var current = Balance(type);
            if (delta < 0 && current < -delta) throw new InvalidOperationException("Insufficient currency.");
            var next = checked(current + delta);
            if (type == CurrencyType.SoftCurrency) _state.SoftCurrency = next;
            else _state.PremiumCurrency = next;
            _state.Version++;
            var transaction = new CurrencyTransaction
            {
                IdempotencyKey = idempotencyKey, Currency = type, Delta = delta, BalanceAfter = next,
                Reason = reason ?? string.Empty, CreatedAtUtc = createdAtUtc
            };
            _state.Transactions.Add(transaction);
            _transactions.Add(idempotencyKey, transaction);
            return transaction;
        }
    }
}
