using FlameCsv.SourceGen.Helpers;
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

        // loop over the enum's values
        foreach (var member in enumType.GetMembers())
        {
            if (member is IFieldSymbol { HasConstantValue: true } enumValue)
            {
                var value = new EnumValueModel(enumValue, isUnsigned, diagnostics);
                values.Add(value);
                uniqueValues.Add(value.Value);
            }
        }

        values.Sort();
        Values = values.ToEquatableArrayAndFree();
        UniqueValues = uniqueValues.ToEquatableArrayAndFree();
        Array.Sort(UniqueValues.UnsafeGetArray!);

        int expected = 0;
        bool isContiguous = true;

        foreach (ref readonly var value in Values)
        {
            if (value.Value != expected)
            {
                isContiguous = false;
                break;
            }

            expected++;
        }

        ContiguousFromZero = isContiguous;
        ContiguousFromZeroCount = expected;

        WrappingTypes = NestedType.Parse(converterType, cancellationToken, diagnostics);
        InGlobalNamespace = converterType.ContainingNamespace.IsGlobalNamespace;
        Namespace = converterType.ContainingNamespace.ToDisplayString();
        HasExplicitNames = Values.AsImmutableArray().Any(v => !string.IsNullOrEmpty(v.ExplicitName));
        HasNegativeValues = UniqueValues.AsImmutableArray().Any(v => v < BigInteger.Zero);
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

            int index = 0;

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

                index++;
            }

            if (ExplicitName is not null && (ExplicitName is "" || ExplicitName[0].IsAsciiNumeric()))
            {
                Location? location = (attribute.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax)
                    ?.ArgumentList?.Arguments.ElementAtOrDefault(index)
                    ?.GetLocation();

                diagnostics.Add(
                    Diagnostics.EnumInvalidExplicitName(
                        enumValue.ContainingSymbol,
                        enumValue,
                        location ?? attribute.GetLocation(),
                        ExplicitName
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
}
