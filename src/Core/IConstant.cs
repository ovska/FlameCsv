namespace FlameCsv;

internal interface IConstant
{
    static abstract bool Value { get; }
}

internal readonly struct TrueConstant : IConstant
{
    public static bool Value => true;
}

internal readonly struct FalseConstant : IConstant
{
    public static bool Value => false;
}
