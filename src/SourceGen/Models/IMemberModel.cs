using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Utilities;

namespace FlameCsv.SourceGen.Models;

internal enum ModelKind : byte
{
    Property,
    Field,
    Parameter,
}

internal interface IMemberModel : IEquatable<IMemberModel?>
{
    /// <inheritdoc cref="ParameterModel.IsRequired"/>
    bool IsRequired { get; }

    /// <inheritdoc cref="PropertyModel.IsIgnored"/>
    bool IsIgnored { get; }

    /// <inheritdoc cref="PropertyModel.IsParsable"/>
    bool IsParsable { get; }

    /// <inheritdoc cref="PropertyModel.IsFormattable"/>
    bool IsFormattable { get; }

    /// <inheritdoc cref="PropertyModel.Order"/>
    int? Order { get; }

    /// <inheritdoc cref="PropertyModel.Index"/>
    int? Index { get; }

    /// <inheritdoc cref="PropertyModel.Identifier"/>
    string Identifier { get; }

    /// <inheritdoc cref="PropertyModel.HeaderName"/>
    string HeaderName { get; }

    /// <inheritdoc cref="PropertyModel.Aliases"/>
    EquatableArray<string> Aliases { get; }

    /// <inheritdoc cref="PropertyModel.OverriddenConverter"/>
    IConverterModel? OverriddenConverter { get; }

    /// <inheritdoc cref="PropertyModel.Type"/>
    TypeRef Type { get; }

    /// <inheritdoc cref="PropertyModel.Convertability"/>
    BuiltinConvertable Convertability { get; }

    /// <summary>Returns whether the member is a parameter, property, or a field.</summary>
    ModelKind Kind { get; }

    /// <summary>
    /// Writes the parsing ID of the member to the writer.
    /// </summary>
    void WriteId(IndentedTextWriter writer);

    /// <summary>
    /// Writes the converter name of the member to the writer.
    /// </summary>
    void WriteConverterName(IndentedTextWriter writer);
}
