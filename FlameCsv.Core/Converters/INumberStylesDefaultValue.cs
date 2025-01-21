using System.Globalization;

namespace FlameCsv.Converters;

internal interface INumberStylesDefaultValue
{
    /// <summary>
    /// Returns the default number styles for the type.
    /// </summary>
    static abstract NumberStyles Default { get; }
}

internal readonly struct FloatStyles : INumberStylesDefaultValue
{
    public static NumberStyles Default => NumberStyles.Float;
}

internal readonly struct IntegerStyles : INumberStylesDefaultValue
{
    public static NumberStyles Default => NumberStyles.Integer;
}
