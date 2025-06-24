namespace FlameCsv.SourceGen;

internal static class Descriptors
{
    private const string CategoryDesign = "Design";

    private const string CategoryUsage = "Usage";

    // 1XX - Types and parameters
    // 2XX - Configuration
    // 3XX - Converters
    // 4XX - Reserved
    // 5XX - Enum Generator

    /// <summary>FLAMESG100</summary>
    public static readonly DiagnosticDescriptor NotPartialType = new(
        id: "FLAMESG100",
        title: "Not a partial type",
        messageFormat: "Type {0} must be partial to participate in source generation{1}",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG101</summary>
    public static readonly DiagnosticDescriptor FileScopedType = new(
        id: "FLAMESG101",
        title: "File-scoped type",
        messageFormat: "File-scoped type {0} cannot participate in source generation",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG102</summary>
    public static readonly DiagnosticDescriptor NoValidConstructor = new(
        id: "FLAMESG102",
        title: "No usable constructor found",
        messageFormat: "Cannot generate reading code: Could not find a valid public constructor for {0}: must have a constructor with [CsvConstructor], a single public constructor, or a parameterless constructor",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG103</summary>
    public static readonly DiagnosticDescriptor RefConstructorParameter = new(
        id: "FLAMESG103",
        title: "Invalid constructor parameter ref kind",
        messageFormat: "Cannot generate reading code: {0} had a constructor parameter with invalid kind: {1}",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG104</summary>
    public static readonly DiagnosticDescriptor RefLikeConstructorParameter = new(
        id: "FLAMESG104",
        title: "Constructor had a ref-like parameter",
        messageFormat: "Cannot generate reading code: {0} had a ref-like constructor parameter: {1}",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG105</summary>
    public static readonly DiagnosticDescriptor NoReadableMembers = new(
        id: "FLAMESG105",
        title: "No valid members/parameters for reading",
        messageFormat: "Cannot generate reading code: {0} had no properties, fields, or parameters that support writing their value",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG106</summary>
    public static readonly DiagnosticDescriptor NoWritableMembers = new(
        id: "FLAMESG106",
        title: "No valid members for writing",
        messageFormat: "Cannot generate writing code: {0} had no properties or fields that support reading their value",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG201</summary>
    public static readonly DiagnosticDescriptor ConflictingConfiguration = new(
        id: "FLAMESG201",
        title: "Conflicting CSV configuration",
        messageFormat: "Conflicting configuration for {0} {1} in type {2}: {3}",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG202</summary>
    public static readonly DiagnosticDescriptor TargetMemberNotFound = new(
        id: "FLAMESG202",
        title: "Target member not found",
        messageFormat: "{0} '{1}' not found on type {2}",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG203</summary>
    public static readonly DiagnosticDescriptor NoMatchingConstructor = new(
        id: "FLAMESG203",
        title: "No matching constructor",
        messageFormat: "No constructor found for {0} with the parameter types [{1}]",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG204</summary>
    public static readonly DiagnosticDescriptor IgnoredParameterWithoutDefaultValue = new(
        id: "FLAMESG204",
        title: "Ignored parameter without default value",
        messageFormat: "Cannot generate reading code: Ignored parameter {0} on type {1} must have a default value",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG205</summary>
    public static readonly DiagnosticDescriptor MultipleTypeProxies = new(
        id: "FLAMESG205",
        title: "Multiple type proxies",
        messageFormat: "Cannot generate reading code: Multiple type proxies found for {0}",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG206</summary>
    public static readonly DiagnosticDescriptor NoCsvConverterConstructor = new(
        id: "FLAMESG206",
        title: "CsvConverter with no valid constructor",
        messageFormat: "Overridden converter {0} on {1} must have an empty public constructor, or a constructor accepting CsvOptions<{2}>",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG207</summary>
    public static readonly DiagnosticDescriptor CsvConverterAbstract = new(
        id: "FLAMESG207",
        title: "CsvConverter must not be abstract",
        messageFormat: "CsvConverter {0} for {1} must not be abstract",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG208</summary>
    public static readonly DiagnosticDescriptor NoTargetTypeOnAssembly = new(
        id: "FLAMESG208",
        title: "No TargetType on attribute applied to an assembly",
        messageFormat: "Attribute [{0}] applied to an assembly must have a TargetType",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG209</summary>
    public static readonly DiagnosticDescriptor NoMemberNameOnAttribute = new(
        id: "FLAMESG209",
        title: "No MemberName on attribute",
        messageFormat: "Attribute [{0}] applied to {1} must have a MemberName",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG210</summary>
    public static readonly DiagnosticDescriptor ConflictingIndex = new(
        id: "FLAMESG210",
        title: "Conflicting index attributes",
        messageFormat: "Cannot generate headerless CSV support: Conflicting index attributes found on {0}: {1}",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG211</summary>
    public static readonly DiagnosticDescriptor GapInIndex = new(
        id: "FLAMESG211",
        title: "Gap in index attributes",
        messageFormat: "Cannot generate headerless CSV support: Index {0} was not configured on {1}",
        category: CategoryDesign,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG212</summary>
    public static readonly DiagnosticDescriptor CsvConverterTypeMismatch = new(
        id: "FLAMESG212",
        title: "CsvConverter type mismatch",
        messageFormat: "Converter {0} for {1} does not match the target type: expected {2}, got {3}",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG501</summary>
    public static readonly DiagnosticDescriptor EnumUnsupportedToken = new(
        id: "FLAMESG501",
        title: "Cannot generate enum converter: Token type not supported",
        messageFormat: "{0} is not a supported token type (must be char or byte)",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG502</summary>
    public static readonly DiagnosticDescriptor EnumInvalidExplicitName = new(
        id: "FLAMESG502",
        title: "Invalid explicit enum name",
        messageFormat: "Cannot generate enum converter: Explicit enum name \"{0}\" for {1}.{2} is not supported: {3}",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    // DEPRECATED: FLAMESG503 EnumFlagsNotSupported

    /// <summary>FLAMESG504</summary>
    public static readonly DiagnosticDescriptor EnumDuplicateName = new(
        id: "FLAMESG504",
        title: "Duplicate explicit enum name",
        messageFormat: "Cannot generate enum converter: Explicit enum name \"{0}\" for {1}.{2} must be unique among other enum members and explicit names",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );

    /// <summary>FLAMESG505</summary>
    public static readonly DiagnosticDescriptor EnumUnsupportedFlag = new(
        id: "FLAMESG505",
        title: "Unsupported flags enum",
        messageFormat: "Cannot generate enum converter: Flags enum {0} value {1} must be a single bit, or a combination of other defined values",
        category: CategoryUsage,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true
    );
}
