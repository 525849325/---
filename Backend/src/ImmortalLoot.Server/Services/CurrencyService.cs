using ImmortalLoot.Server.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ImmortalLoot.Server.Services;

public enum GameCurrency { SoftCurrency, PremiumCurrency }

public sealed class CurrencyService(GameDbContext db)
{
    public async Task<long> ChangeAsync(Guid playerId, GameCurrency type, long delta, string reason, string referenceId, CancellationToken cancellationToken)
    {
        if (delta == 0) throw new ArgumentException("Currency delta cannot be zero.");
        var wallet = db.PlayerCurrencies.Local.SingleOrDefault(value => value.PlayerId == playerId)
            ?? await db.PlayerCurrencies.SingleOrDefaultAsync(value => value.PlayerId == playerId, cancellationToken);
        if (wallet is null)
        {
            wallet = new PlayerCurrency { PlayerId = playerId };
            db.PlayerCurrencies.Add(wallet);
        }
        var current = type == GameCurrency.SoftCurrency ? wallet.SoftCurrency : wallet.PremiumCurrency;
        if (delta < 0 && current < -delta) throw new InvalidOperationException("Insufficient currency.");
        var next = checked(current + delta);
        if (type == GameCurrency.SoftCurrency) wallet.SoftCurrency = next;
        else wallet.PremiumCurrency = next;
        wallet.Version++;
        db.CurrencyLogs.Add(new CurrencyLog
        {
            PlayerId = playerId, Currency = type.ToString(), Delta = delta, BalanceAfter = next,
            Reason = reason, ReferenceId = referenceId
        });
        return next;
    }
}
