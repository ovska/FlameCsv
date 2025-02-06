using FlameCsv.SourceGen.Helpers;

namespace FlameCsv.SourceGen.Models;

internal interface IMemberModel : IEquatable<IMemberModel?>
{
    /// <inheritdoc cref="ParameterModel.IsRequired"/>
    bool IsRequired {get; }

    /// <inheritdoc cref="PropertyModel.IsIgnored"/>
    bool IsIgnored { get; }

    /// <inheritdoc cref="PropertyModel.CanRead"/>
    bool CanRead { get; }

    /// <inheritdoc cref="PropertyModel.CanWrite"/>
    bool CanWrite { get; }
    int? Order { get; }
    string Identifier { get; }
    string Name { get; }
    EquatableArray<string> Names { get; }
    ConverterModel? OverriddenConverter { get; }
    TypeRef Type { get; }

    void WriteId(IndentedTextWriter writer);
    void WriteConverterName(IndentedTextWriter writer);
}
