using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal interface IMemberModel : IEquatable<IMemberModel>
{
    bool IsRequired {get; }
    bool CanRead { get; }
    bool CanWrite { get; }
    int Order { get; }
    string Name { get; }
    string ActualName { get; }
    EquatableArray<string> Names { get; }
    ConverterModel? OverriddenConverter { get; }
    TypeRef Type { get; }

    void WriteIndexName(StringBuilder sb);
    void WriteConverterName(StringBuilder sb);
}
