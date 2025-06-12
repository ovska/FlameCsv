using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using FlameCsv.SourceGen.Helpers;
using FlameCsv.SourceGen.Utilities;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace FlameCsv.SourceGen.Models;

internal readonly record struct EnumModel
{
    public static bool TryGet(
        ISymbol converterSymbol,
        AttributeData attributeData,
        CancellationToken cancellationToken,
        out EquatableArray<Diagnostic> diagnostics,
        out EnumModel model
    )
    {
        List<Diagnostic> diagList = PooledList<Diagnostic>.Acquire();

        if (
            converterSymbol is INamedTypeSymbol converterType
            && attributeData.AttributeClass
                is {
                    TypeArguments: [
                        { } tokenType,
                        INamedTypeSymbol { CanBeReferencedByName: true, EnumUnderlyingType: not null } enumType,
                    ]
                }
        )
        {
            if (tokenType.SpecialType is not (SpecialType.System_Byte or SpecialType.System_Char))
            {
                diagList.Add(Diagnostics.EnumUnsupportedToken(converterSymbol, attributeData, tokenType));
            }

            Diagnostics.EnsurePartial(converterType, cancellationToken, diagList);
            Diagnostics.CheckIfFileScoped(converterType, cancellationToken, diagList);
            Diagnostics.CheckIfFileScoped(enumType, cancellationToken, diagList);

            model = new EnumModel(
                enumType: enumType,
                tokenType: tokenType,
                converterType: converterType,
                cancellationToken,
                diagList
            );

            if (diagList.Count == 0)
            {
                diagnostics = diagList.ToEquatableArrayAndFree();
                return true;
            }
        }

        model = default;
        diagnostics = diagList.ToEquatableArrayAndFree();
        return false;
    }

    public TypeRef TokenType { get; }
    public TypeRef ConverterType { get; }
    public TypeRef EnumType { get; }
    public TypeRef UnderlyingType { get; }
    public EquatableArray<BigInteger> UniqueValues { get; }
    public EquatableArray<EnumValueModel> Values { get; }
    public bool HasFlagsAttribute { get; }
    public bool ContiguousFromZero { get; }
    public int ContiguousFromZeroCount { get; }
    public EquatableArray<NestedType> WrappingTypes { get; }
    public bool HasExplicitNames { get; }
    public bool HasNegativeValues { get; }

    /// <summary>
    /// Whether the typemap is in the global namespace.
    /// </summary>
    public bool InGlobalNamespace { get; }

    /// <summary>
    /// Namespace of the typemap.
    /// </summary>
    /// <seealso cref="InGlobalNamespace"/>
    public string Namespace { get; }

    private EnumModel(
        INamedTypeSymbol enumType,
        ITypeSymbol tokenType,
        ITypeSymbol converterType,
        CancellationToken cancellationToken,
        List<Diagnostic> diagnostics
    )
    {
        TokenType = new TypeRef(tokenType);
        ConverterType = new TypeRef(converterType);
        EnumType = new TypeRef(enumType);
        UnderlyingType = new TypeRef(enumType.EnumUnderlyingType!);

        HasFlagsAttribute = enumType.GetAttributes().Any(a => a.AttributeClass?.Name == "FlagsAttribute");

        List<EnumValueModel> values = PooledList<EnumValueModel>.Acquire();
        HashSet<BigInteger> uniqueValues = PooledSet<BigInteger>.Acquire();

        bool isUnsigned =
            enumType.EnumUnderlyingType?.SpecialType
                is SpecialType.System_Byte
                    or SpecialType.System_UInt16
                    or SpecialType.System_UInt32
                    or SpecialType.System_UInt64;

        bool hasExplicitNames = false;

        // loop over the enum's values
        foreach (var member in enumType.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true } enumValue)
            {
                var value = new EnumValueModel(enumValue, isUnsigned, diagnostics);
                values.Add(value);
                uniqueValues.Add(value.Value);

                hasExplicitNames |= value.HasValidExplicitName;
            }
        }

        values.Sort();
        Values = values.ToEquatableArrayAndFree();
        UniqueValues = uniqueValues.ToEquatableArrayAndFree();
        Array.Sort(UniqueValues.UnsafeArray!);

        int contiguousCount = 0;
        bool isContiguous = true;

        foreach (ref readonly var value in Values)
        {
            if (value.Value != contiguousCount)
            {
                isContiguous = false;
                break;
            }

            contiguousCount++;
        }

        ContiguousFromZero = isContiguous;
        ContiguousFromZeroCount = contiguousCount;

        WrappingTypes = NestedType.Parse(converterType, cancellationToken, diagnostics);
        InGlobalNamespace = converterType.ContainingNamespace.IsGlobalNamespace;
        Namespace = converterType.ContainingNamespace.ToDisplayString();
        HasExplicitNames = Values.AsImmutableArray().Any(v => !string.IsNullOrEmpty(v.ExplicitName));
        HasNegativeValues = UniqueValues.AsImmutableArray().Any(v => v < BigInteger.Zero);

        if (HasExplicitNames)
        {
            CheckExplicitNameDuplicates(enumType, diagnostics);
        }
    }

    private void CheckExplicitNameDuplicates(INamedTypeSymbol enumType, List<Diagnostic> diagnostics)
    {
        HashSet<string>? handled = null; // report only one diagnostic per duplicate name

        foreach (ref readonly var value in Values)
        {
            if (!value.HasValidExplicitName)
            {
                // If the explicit name is null or invalid, we don't need to check for duplicates
                continue;
            }

            foreach (ref readonly var innerValue in Values)
            {
                if (!innerValue.HasValidExplicitName || value == innerValue)
                {
                    continue;
                }

                if (value.ExplicitName == innerValue.Name || value.ExplicitName == innerValue.ExplicitName)
                {
                    if (!(handled ??= PooledSet<string>.Acquire()).Add(value.ExplicitName!))
                    {
                        // Already handled this explicit name
                        continue;
                    }

                    string invalidName = value.Name;

                    Location? location = enumType
                        .GetMembers()
                        .FirstOrDefault(m => m is IFieldSymbol f && f.Name == invalidName)
                        ?.DeclaringSyntaxReferences.FirstOrDefault()
                        ?.GetSyntax()
                        .GetLocation();

                    diagnostics.Add(
                        Diagnostics.EnumDuplicateName(
                            enumType,
                            value.Name,
                            location ?? enumType.Locations.FirstOrDefault(),
                            value.ExplicitName!
                        )
                    );
                }
            }
        }

        PooledSet<string>.Release(handled);
    }
}

internal readonly record struct EnumValueModel : IComparable<EnumValueModel>
{
    public string Name { get; }
    public string? ExplicitName { get; }
    public BigInteger Value { get; }
    public string DisplayName => ExplicitName ?? Name;

    public EnumValueModel(IFieldSymbol enumValue, bool isUnsigned, List<Diagnostic> diagnostics)
    {
        Name = enumValue.Name;
        Value = isUnsigned
            ? new BigInteger(Convert.ToUInt64(enumValue.ConstantValue))
            : new BigInteger(Convert.ToInt64(enumValue.ConstantValue));

        foreach (var attribute in enumValue.GetAttributes())
        {
            if (attribute.AttributeClass?.ToDisplayString() != "System.Runtime.Serialization.EnumMemberAttribute")
            {
                continue;
            }

            // position of the named argument "Value"; should not be necessary but just in case
            int valueArgumentIndex = 0;

            // Check named arguments
            foreach (var namedArg in attribute.NamedArguments)
            {
                if (
                    string.Equals(namedArg.Key, "Value", StringComparison.Ordinal)
                    && namedArg.Value.Value is string value
                )
                {
                    ExplicitName = value;
                    break;
                }

                valueArgumentIndex++;
            }

            if (HasInvalidExplicitName)
            {
                Location? location = (attribute.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax)
                    ?.ArgumentList?.Arguments.ElementAtOrDefault(valueArgumentIndex)
                    ?.GetLocation();

                diagnostics.Add(
                    Diagnostics.EnumInvalidExplicitName(
                        enumValue.ContainingSymbol,
                        enumValue,
                        location ?? attribute.GetLocation(),
                        ExplicitName!
                    )
                );
            }

            break;
        }
    }

    public int CompareTo(EnumValueModel other)
    {
        int cmp = Value.CompareTo(other.Value);
        if (cmp == 0)
            cmp = StringComparer.Ordinal.Compare(Name, other.Name);
        if (cmp == 0)
            cmp = StringComparer.Ordinal.Compare(ExplicitName, other.ExplicitName);
        return cmp;
    }

    public bool HasInvalidExplicitName =>
        ExplicitName switch
        {
            null => false,
            "" => true,
            _ => ExplicitName[0].IsAsciiNumeric(),
        };

    public bool HasValidExplicitName => ExplicitName is not null && !HasInvalidExplicitName;
}
