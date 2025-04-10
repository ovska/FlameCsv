namespace FlameCsv.Reflection;

internal abstract class ReflectionData
{
    public abstract ReadOnlySpan<object> Attributes { get; }

    public static ReflectionData Empty { get; } = new EmptyImpl();

    private sealed class EmptyImpl : ReflectionData
    {
        public override ReadOnlySpan<object> Attributes => [];
    }
}
