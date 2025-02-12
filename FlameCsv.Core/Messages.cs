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

    private const string Prefix
        = "This code path may require types that cannot be statically analyzed and might need runtime code generation. ";

    public const string ConverterFactories = Prefix + "Use the source generator APIs for native AOT applications.";
    public const string ConverterOverload = Prefix + "Use an alternative overload for native AOT applications.";

    public const string FactoryMethod =
        "This method may require reflection, runtime code generation, or types that cannot be statically analyzed. " +
        "It may not work properly in native AOT applications, and is not supported by the source generator.";
}
