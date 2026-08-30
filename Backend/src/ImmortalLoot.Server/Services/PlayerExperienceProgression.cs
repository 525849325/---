using ImmortalLoot.Server.Persistence;

namespace ImmortalLoot.Server.Services;

public static class PlayerExperienceProgression
{
    public static void Grant(Player player, long amount)
    {
        ArgumentNullException.ThrowIfNull(player);
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (player.Level <= 0) throw new InvalidOperationException("Player level must be positive.");

        var cultivationExperience = checked(player.CultivationExperience + amount);
        var levelExperience = checked(player.Exp + amount);
        var level = player.Level;
        while (levelExperience >= level * 100L)
        {
            levelExperience -= level * 100L;
            level = checked(level + 1);
        }

        player.CultivationExperience = cultivationExperience;
        player.Exp = levelExperience;
        player.Level = level;
    }
}
