namespace ImmortalLoot.Server.Services;

public interface IServerClock
{
    DateTime UtcNow { get; }
}

public sealed class ServerClock : IServerClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
