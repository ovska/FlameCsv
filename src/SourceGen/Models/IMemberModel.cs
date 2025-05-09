using FlameCsv.SourceGen.Helpers;

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

    /// <inheritdoc cref="PropertyModel.CanRead"/>
    bool CanRead { get; }

    /// <inheritdoc cref="PropertyModel.CanWrite"/>
    bool CanWrite { get; }

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
    ConverterModel? OverriddenConverter { get; }

    /// <inheritdoc cref="PropertyModel.Type"/>
    TypeRef Type { get; }

    /// <inheritdoc cref="PropertyModel.Convertability"/>
    BuiltinConvertable Convertability { get; }

    /// <summary>Returns whether the member is a parameter, property, or a field.</summary>
    ModelKind Kind { get; }

    void WriteId(IndentedTextWriter writer);
    void WriteConverterName(IndentedTextWriter writer);
}
