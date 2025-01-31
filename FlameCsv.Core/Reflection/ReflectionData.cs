namespace FlameCsv.Reflection;

internal abstract class ReflectionData
{
    public abstract ReadOnlySpan<object> Attributes { get; }
}
