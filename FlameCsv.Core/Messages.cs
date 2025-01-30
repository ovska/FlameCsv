using System.Diagnostics.CodeAnalysis;

namespace FlameCsv;

internal static class Messages
{
    public const DynamicallyAccessedMemberTypes Ctors = DynamicallyAccessedMemberTypes.PublicConstructors;

    public const DynamicallyAccessedMemberTypes ReflectionBound =
        DynamicallyAccessedMemberTypes.PublicConstructors |
        DynamicallyAccessedMemberTypes.PublicProperties |
        DynamicallyAccessedMemberTypes.PublicFields;

    private const string Suffix
        = "Use the overloads accepting source generated CsvTypeMap for AOT/trimming compatible code.";

    public const string DynamicCode = "This code path uses compiled expressions. " + Suffix;
    public const string Reflection = "This code path uses reflection. " + Suffix;

    public const string StructFactorySuppressionMessage = "Constructors of the used struct are not actually used.";

    public const string HeaderProcessorSuppressionMessage
        = "All code paths that initialize this internal type are already annotated";
}
