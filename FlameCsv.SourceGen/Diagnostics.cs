namespace FlameCsv.SourceGen;

    public static class Diagnostics
    {
        private static readonly DiagnosticDescriptor _noCtor = new(
            id: "FLAMESG100",
            title: "No usable constructor found",
            messageFormat: "Could not find a valid public constructor for {0}: must have a constructor with [CsvConstructor], a single public constructor, or a parameterless constructor",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor _refCtorParam = new(
            id: "FLAMESG101",
            title: "Invalid constructor parameter ref kind",
            messageFormat: "{0} had a constructor parameter with invalid ref kind {1}: {2}",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor _refStructParam = new(
            id: "FLAMESG102",
            title: "Constructor had a ref-like parameter",
            messageFormat: "{0} had a ref-like constructor parameter: {1}",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor _noReadableMembers = new(
            id: "FLAMESG200",
            title: "No readable members or parameters",
            messageFormat: "Cannot generate reading code: {0} had no readable properties, fields, or parameters",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor _noWritableMembers = new(
            id: "FLAMESG201",
            title: "No writable members or parameters",
            messageFormat: "Cannot generate writing code: {0} had no writable properties or fields",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor _factoryNoCtorFound = new(
            id: "FLAMESG202",
            title: "CsvConverterFactory with no valid constructor",
            messageFormat: "Factory type {0} on {1} must have an empty public constructor, or a constructor accepting CsvOptions<{2}>",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        public static Diagnostic NoConstructorFound(ITypeSymbol type)
        {
            return Diagnostic.Create(_noCtor, GetLocation(type), type.ToDisplayString());
        }

        public static Diagnostic RefConstructorParameterFound(ITypeSymbol type, IParameterSymbol parameter)
        {
            return Diagnostic.Create(_refCtorParam, GetLocation(parameter), type.ToDisplayString(), parameter.RefKind, parameter.ToDisplayString());
        }

        public static Diagnostic RefLikeConstructorParameterFound(ITypeSymbol type, IParameterSymbol parameter)
        {
            return Diagnostic.Create(_refStructParam, GetLocation(parameter), type.ToDisplayString(), parameter.ToDisplayString());
        }

        public static Diagnostic NoReadableMembers(ITypeSymbol type)
        {
            return Diagnostic.Create(_noReadableMembers, GetLocation(type), type.ToDisplayString());
        }

        public static Diagnostic NoWritableMembers(ITypeSymbol type)
        {
            return Diagnostic.Create(_noWritableMembers, GetLocation(type), type.ToDisplayString());
        }

        public static Diagnostic NoCsvFactoryConstructorFound(ISymbol target, string factoryType, ITypeSymbol tokenType)
        {
            return Diagnostic.Create(_factoryNoCtorFound, GetLocation(target), target.ToDisplayString(), factoryType, tokenType.ToDisplayString());
        }

        private static Location? GetLocation(ISymbol symbol)
        {
            return symbol.Locations.IsDefaultOrEmpty ? null : symbol.Locations[0];
        }
    }
