using ImmortalLoot.Server.Config;
using ImmortalLoot.Server.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ImmortalLoot.Server.Services;

public sealed record TaskDefinition(string Id, string EventType, int Target, int ActivityPoints, RewardPayload Reward);
public sealed record TaskView(string Id, int Progress, int Target, int ActivityPoints, bool CanClaim, bool Claimed, RewardPayload Reward);
public sealed record ActivityChestView(int RequiredPoints, bool CanClaim, bool Claimed, RewardPayload Reward);
public sealed record DailyTaskBoard(string UtcDate, int ActivityPoints, IReadOnlyList<TaskView> Tasks, IReadOnlyList<ActivityChestView> Chests);

public sealed class TaskService(GameDbContext db, RewardService rewards, IServerClock clock, ServerGameConfigCatalog catalog)
{
    private readonly TaskDefinition[] Definitions = catalog.Tasks.Select(value => new TaskDefinition(value.Id, value.EventType, value.Target, value.ActivityPoints, new RewardPayload(value.SoftCurrency, value.PremiumCurrency, value.Items))).ToArray();
    private readonly (int Points, RewardPayload Reward)[] Chests = catalog.ActivityChests.Select(value => (value.RequiredPoints, new RewardPayload(value.SoftCurrency, value.PremiumCurrency, value.Items))).ToArray();

    public async Task RecordAsync(Guid playerId, string eventType, int amount, CancellationToken cancellationToken)
    {
        if (amount <= 0) return;
        var date = clock.UtcNow.ToString("yyyy-MM-dd");
        foreach (var definition in Definitions.Where(value => value.EventType == eventType))
        {
            var state = await db.PlayerTasks.SingleOrDefaultAsync(value => value.PlayerId == playerId && value.TaskId == definition.Id && value.UtcDate == date, cancellationToken);
            if (state is null)
            {
                state = new PlayerTask { PlayerId = playerId, TaskId = definition.Id, UtcDate = date };
                db.PlayerTasks.Add(state);
            }
            state.Progress = Math.Min(definition.Target, checked(state.Progress + amount));
        }
    }

    public async Task<DailyTaskBoard> ListAsync(Guid playerId, CancellationToken cancellationToken)
    {
        var date = clock.UtcNow.ToString("yyyy-MM-dd");
        var states = await db.PlayerTasks.AsNoTracking().Where(value => value.PlayerId == playerId && value.UtcDate == date).ToDictionaryAsync(value => value.TaskId, cancellationToken);
        var taskViews = Definitions.Select(definition =>
        {
            states.TryGetValue(definition.Id, out var state);
            var progress = state?.Progress ?? 0;
            return new TaskView(definition.Id, progress, definition.Target, definition.ActivityPoints, progress >= definition.Target && state?.IsClaimed != true, state?.IsClaimed == true, definition.Reward);
        }).ToArray();
        var points = taskViews.Where(value => value.Progress >= value.Target).Sum(value => value.ActivityPoints);
        var chestViews = Chests.Select(chest =>
        {
            states.TryGetValue(ChestId(chest.Points), out var state);
            return new ActivityChestView(chest.Points, points >= chest.Points && state?.IsClaimed != true, state?.IsClaimed == true, chest.Reward);
        }).ToArray();
        return new DailyTaskBoard(date, points, taskViews, chestViews);
    }

    public async Task<RewardResult> ClaimAsync(Guid playerId, string taskId, CancellationToken cancellationToken)
    {
        var definition = Definitions.SingleOrDefault(value => value.Id == taskId) ?? throw new KeyNotFoundException("Task was not found.");
        var date = clock.UtcNow.ToString("yyyy-MM-dd");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var state = await db.PlayerTasks.SingleOrDefaultAsync(value => value.PlayerId == playerId && value.TaskId == taskId && value.UtcDate == date, cancellationToken)
            ?? throw new InvalidOperationException("Task is not complete.");
        if (state.Progress < definition.Target) throw new InvalidOperationException("Task is not complete.");
        var result = await rewards.GrantTrackedAsync(playerId, "task:" + date + ":" + taskId, "Task", definition.Reward, cancellationToken);
        state.IsClaimed = true;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<RewardResult> ClaimActivityChestAsync(Guid playerId, int requiredPoints, CancellationToken cancellationToken)
    {
        var chest = Chests.SingleOrDefault(value => value.Points == requiredPoints);
        if (chest.Points == 0) throw new KeyNotFoundException("Activity chest was not found.");
        var board = await ListAsync(playerId, cancellationToken);
        if (board.ActivityPoints < requiredPoints) throw new InvalidOperationException("Activity points are insufficient.");
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var taskId = ChestId(requiredPoints);
        var state = await db.PlayerTasks.SingleOrDefaultAsync(value => value.PlayerId == playerId && value.TaskId == taskId && value.UtcDate == board.UtcDate, cancellationToken);
        if (state is null)
        {
            state = new PlayerTask { PlayerId = playerId, TaskId = taskId, UtcDate = board.UtcDate, Progress = requiredPoints };
            db.PlayerTasks.Add(state);
        }
        var result = await rewards.GrantTrackedAsync(playerId, "activity:" + board.UtcDate + ":" + requiredPoints, "ActivityChest", chest.Reward, cancellationToken);
        state.IsClaimed = true;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    private static string ChestId(int points) => "activity_chest_" + points;
}
