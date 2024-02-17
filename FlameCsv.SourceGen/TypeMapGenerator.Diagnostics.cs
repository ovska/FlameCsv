namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private static class Diagnostics
    {
        private static readonly DiagnosticDescriptor _noCtor = new(
            id: "FLAMESG001",
            title: "No usable constructor found",
            messageFormat: "{0} had no valid parameterless constructor, or constructor decorated with [CsvConstructor]",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: false);

        private static readonly DiagnosticDescriptor _duplicateCtor = new(
            id: "FLAMESG002",
            title: "Multiple constructors with CsvConstructorAttribute",
            messageFormat: "{0} had multiple constructors decorated with [CsvConstructor]",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: false);

        private static readonly DiagnosticDescriptor _privateCtor = new(
            id: "FLAMESG003",
            title: "Private constructor with CsvConstructorAttribute",
            messageFormat: "{0} had a private constructor decorated with [CsvConstructor]",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: false);

        private static readonly DiagnosticDescriptor _refCtorParam = new(
            id: "FLAMESG004",
            title: "Invalid constructor parameter ref kind",
            messageFormat: "{0} had a constructor parameter with ref kind {1}: {2}",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: false);

        private static readonly DiagnosticDescriptor _refStructParam = new(
            id: "FLAMESG005",
            title: "Constructor had a ref-like parameter",
            messageFormat: "{0} had a ref-like constructor parameter: {1}",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: false);

        private static readonly DiagnosticDescriptor _noWritableMembersOrParams = new(
            id: "FLAMESG006",
            title: "No writable members or parameters",
            messageFormat: "{0} had no writable members or parameters",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: false);

        private static readonly DiagnosticDescriptor _factoryNoCtorFound = new(
            id: "FLAMESG007",
            title: "CsvConverterFactory with no valid constructor",
            messageFormat: "{0} has no empty constructor, or a constructor accepting CsvOptions<T>",
            category: "Usage",
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: false);

        public static Diagnostic NoConstructorFound(ITypeSymbol type)
        {
            return Diagnostic.Create(_noCtor, GetLocation(type), type.ToDisplayString());
        }

        public static Diagnostic TwoConstructorsFound(ITypeSymbol type, IMethodSymbol constructor)
        {
            return Diagnostic.Create(_duplicateCtor, GetLocation(constructor), type.ToDisplayString());
        }

        public static Diagnostic PrivateConstructorFound(ITypeSymbol type, IMethodSymbol constructor)
        {
            return Diagnostic.Create(_privateCtor, GetLocation(constructor), type.ToDisplayString());
        }

        public static Diagnostic RefConstructorParameterFound(ITypeSymbol type, IParameterSymbol parameter)
        {
            return Diagnostic.Create(_refCtorParam, GetLocation(parameter), type.ToDisplayString(), parameter.RefKind, parameter.ToDisplayString());
        }

        public static Diagnostic RefLikeConstructorParameterFound(ITypeSymbol type, IParameterSymbol parameter)
        {
            return Diagnostic.Create(_refStructParam, GetLocation(parameter), type.ToDisplayString(), parameter.ToDisplayString());
        }

        public static Diagnostic NoWritableMembersOrParametersFound(ITypeSymbol type)
        {
            return Diagnostic.Create(_noWritableMembersOrParams, GetLocation(type), type.ToDisplayString());
        }

        public static Diagnostic NoCsvFactoryConstructorFound(ITypeSymbol factoryType)
        {
            return Diagnostic.Create(_factoryNoCtorFound, GetLocation(factoryType), factoryType.ToDisplayString());
        }

        private static Location? GetLocation(ISymbol symbol)
        {
            return symbol.Locations.Length == 0 ? null : symbol.Locations[0];
        }
    }
}
