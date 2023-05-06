namespace FlameCsv.SourceGen;

public partial class TypeMapGenerator
{
    private static class Diagnostics
    {
        private const string Category = "FlameCsv";

        private static readonly DiagnosticDescriptor _emptyHeaderValuesAttribute = new(
            id: "FCSV001",
            title: "Empty CsvHeaderValuesAttribute",
            messageFormat: "No header values on {0}.{1}",
            category: Category,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: false);

        public static Diagnostic EmptyHeaderValuesAttribute(ITypeSymbol type, ISymbol member)
        {
            return Diagnostic.Create(_emptyHeaderValuesAttribute, null, type.ToDisplayString(), member.ToDisplayString());
        }
    }
}
