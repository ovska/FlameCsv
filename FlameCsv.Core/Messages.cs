using System.Diagnostics.CodeAnalysis;

namespace FlameCsv;

internal static class Messages
{
    public const DynamicallyAccessedMemberTypes Ctors = DynamicallyAccessedMemberTypes.PublicConstructors;

    public const DynamicallyAccessedMemberTypes ReflectionBound =
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicFields;

    public const string CompiledExpressions =
        "This code path uses reflection and compiled expressions. Use SourceGen and overloads accepting " +
        "CsvTypeMap to avoid them.";

    public const string StructFactorySuppressionMessage =
        "Constructors of the used struct are not actually used.";

    public const string HeaderProcessorSuppressionMessage =
        "All code paths that initialize this internal type are already annotated";
}
