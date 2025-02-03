namespace FlameCsv.SourceGen;

internal static class Descriptors
{
    public static readonly DiagnosticDescriptor FileScopedType = new(
        id: "FLAMESG001",
        title: "File-scoped type",
        messageFormat: "File-scoped type {0} cannot participate in source generation",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoValidConstructor = new(
        id: "FLAMESG100",
        title: "No usable constructor found",
        messageFormat:
        "Cannot generate reading code: Could not find a valid public constructor for {0}: must have a constructor with [CsvConstructor], a single public constructor, or a parameterless constructor",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RefConstructorParameter = new(
        id: "FLAMESG101",
        title: "Invalid constructor parameter ref kind",
        messageFormat: "Cannot generate reading code: {0} had a constructor parameter with invalid ref kind {1}: {2}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RefLikeConstructorParameter = new(
        id: "FLAMESG102",
        title: "Constructor had a ref-like parameter",
        messageFormat: "Cannot generate reading code: {0} had a ref-like constructor parameter: {1}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MultipleTypeProxies = new(
        id: "FLAMESG103",
        title: "Multiple type proxies",
        messageFormat: "Cannot generate reading code: Multiple type proxies found for {0}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ExplicitInterfaceRequired = new(
        id: "FLAMESG104",
        title: "Required explicit interface implementation",
        messageFormat:
        "Cannot generate reading code: Explicitly implemented property {0} cannot be marked as required",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoReadableMembers = new(
        id: "FLAMESG200",
        title: "No valid members/parameters for reading",
        messageFormat:
        "Cannot generate reading code: {0} had no properties, fields, or parameters that support writing their value",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoWritableMembers = new(
        id: "FLAMESG201",
        title: "No valid members for writing",
        messageFormat: "Cannot generate writing code: {0} had no properties or fields that support reading their value",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoCsvFactoryConstructor = new(
        id: "FLAMESG202",
        title: "CsvConverterFactory with no valid constructor",
        messageFormat:
        "Overridden converter factory type {0} on {1} must have an empty public constructor, or a constructor accepting CsvOptions<{2}>",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CsvConverterAbstract = new(
        id: "FLAMESG203",
        title: "CsvConverter must not be abstract",
        messageFormat: "CsvConverter {0} for {1} must not be abstract",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TargetMemberNotFound = new(
        id: "FLAMESG204",
        title: "Target member not found",
        messageFormat: "{0} '{1}' not found on type {2}",
        category: "Usage",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
