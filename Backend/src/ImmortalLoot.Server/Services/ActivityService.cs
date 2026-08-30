namespace ImmortalLoot.Server.Services;
using ImmortalLoot.Server.Config;

public sealed record ActivityView(string Id, string Name, string Type, DateTime StartTimeUtc, DateTime EndTimeUtc, double RewardModifier);

public sealed class ActivityService(IServerClock clock, ServerGameConfigCatalog catalog)
{
    private readonly IReadOnlyList<ActivityView> _activities = catalog.Activities.Select(value =>
        new ActivityView(value.Id, value.Name, value.Type, value.StartTimeUtc, value.EndTimeUtc, value.RewardModifier)).ToArray();

    public IReadOnlyList<ActivityView> ListActive() => _activities.Where(value => value.StartTimeUtc <= clock.UtcNow && clock.UtcNow < value.EndTimeUtc).ToArray();
    public double RewardMultiplier(string type)
    {
        var result = 1.0;
        foreach (var activity in ListActive()) if (activity.Type == type) result *= activity.RewardModifier;
        return result;
    }
}
