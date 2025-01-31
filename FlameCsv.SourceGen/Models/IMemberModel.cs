using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal interface IMemberModel
{
    bool IsRequired {get; }
    bool CanRead { get; }
    bool CanWrite { get; }
    int Order { get; }
    string Name { get; }
    string IndexPrefix { get; }
    string ConverterPrefix { get; }
    ImmutableEquatableArray<string> Names { get; }
    ConverterModel? OverriddenConverter { get; }
    TypeRef Type { get; }
}
