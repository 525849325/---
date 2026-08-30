namespace ImmortalLoot.Core
{
    public interface IRandomSource
    {
        int Range(int minInclusive, int maxExclusive);
        float Value();
    }
}
