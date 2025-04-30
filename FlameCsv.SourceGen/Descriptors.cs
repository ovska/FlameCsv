namespace FlameCsv.SourceGen;

internal static class Descriptors
{
    private const string CategoryDesign = "Design";

    private const string CategoryUsage = "Usage";
    // 1XX - Types and parameters
    // 2XX - Configuration
    // 3XX - Converters

    // 5XX - Enum Generator

    public static readonly DiagnosticDescriptor NotPartialType = new(
        id: "FLAMESG100",
        title: "Not a partial type",
        messageFormat: "Type {0} must be declared as partial to participate in source generation{1}",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor FileScopedType = new(
        id: "FLAMESG101",
        title: "File-scoped type",
        messageFormat: "File-scoped type {0} cannot participate in source generation",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoValidConstructor = new(
        id: "FLAMESG102",
        title: "No usable constructor found",
        messageFormat:
        "Cannot generate reading code: Could not find a valid public constructor for {0}: must have a constructor with [CsvConstructor], a single public constructor, or a parameterless constructor",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RefConstructorParameter = new(
        id: "FLAMESG103",
        title: "Invalid constructor parameter ref kind",
        messageFormat: "Cannot generate reading code: {0} had a constructor parameter with invalid kind: {1}",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor RefLikeConstructorParameter = new(
        id: "FLAMESG104",
        title: "Constructor had a ref-like parameter",
        messageFormat: "Cannot generate reading code: {0} had a ref-like constructor parameter: {1}",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoReadableMembers = new(
        id: "FLAMESG105",
        title: "No valid members/parameters for reading",
        messageFormat:
        "Cannot generate reading code: {0} had no properties, fields, or parameters that support writing their value",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoWritableMembers = new(
        id: "FLAMESG106",
        title: "No valid members for writing",
        messageFormat: "Cannot generate writing code: {0} had no properties or fields that support reading their value",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ConflictingConfiguration = new(
        id: "FLAMESG201",
        title: "Conflicting CSV configuration",
        messageFormat: "Conflicting configuration for {0} {1} in type {2}: {3}",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TargetMemberNotFound = new(
        id: "FLAMESG202",
        title: "Target member not found",
        messageFormat: "{0} '{1}' not found on type {2}",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoMatchingConstructor = new(
        id: "FLAMESG203",
        title: "No matching constructor",
        messageFormat: "No constructor found for {0} with the parameter types [{1}]",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor IgnoredParameterWithoutDefaultValue = new(
        id: "FLAMESG204",
        title: "Ignored parameter without default value",
        messageFormat: "Cannot generate reading code: Ignored parameter {0} on type {1} must have a default value",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MultipleTypeProxies = new(
        id: "FLAMESG205",
        title: "Multiple type proxies",
        messageFormat: "Cannot generate reading code: Multiple type proxies found for {0}",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoCsvFactoryConstructor = new(
        id: "FLAMESG206",
        title: "CsvConverterFactory with no valid constructor",
        messageFormat:
        "Overridden converter factory type {0} on {1} must have an empty public constructor, or a constructor accepting CsvOptions<{2}>",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor CsvConverterAbstract = new(
        id: "FLAMESG207",
        title: "CsvConverter must not be abstract",
        messageFormat: "CsvConverter {0} for {1} must not be abstract",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoTargetTypeOnAssembly = new(
        id: "FLAMESG208",
        title: "No TargetType on attribute applied to assembly",
        messageFormat: "Attribute {0} applied to assembly must have a TargetType",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor NoMemberNameOnAttribute = new(
        id: "FLAMESG209",
        title: "No MemberName on attribute",
        messageFormat: "Attribute {0} applied to {1} must have a MemberName",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EnumUnsupportedToken = new(
        id: "FLAMESG501",
        title: "Cannot generate enum converter: Token type not supported",
        messageFormat: "{0} is not a supported token type (must be char or byte)",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EnumInvalidExplicitName = new(
        id: "FLAMESG502",
        title: "Invalid explicit enum name",
        messageFormat:
        "Cannot generate enum converter: Explicit enum name \"{0}\" for {1}.{2} is not supported: value must not be empty, and must not start with a digit, plus, or minus",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    // DEPRECATED: FLAMESG503 EnumFlagsNotSupported
}
