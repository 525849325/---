using System;

namespace ImmortalLoot.Core
{
    public interface IServerClock
    {
        DateTime UtcNow { get; }
    }
}
