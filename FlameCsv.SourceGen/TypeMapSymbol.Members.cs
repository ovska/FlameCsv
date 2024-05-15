using FlameCsv.SourceGen.Bindings;
using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using System.Diagnostics;

namespace FlameCsv.SourceGen;

partial struct TypeMapSymbol
{
    public TypeBindings Bindings => _typeBindings.Value;

    private TypeBindings ResolveMembers()
    {
        var members = ImmutableArray.CreateBuilder<MemberBinding>();
        var parameters = ImmutableArray.CreateBuilder<ParameterBinding>();

        IMethodSymbol? constructor = null;
        IMethodSymbol? parameterlessCtor = null;

        foreach (var member in Type.GetPublicMembersRecursive())
        {
            ThrowIfCancellationRequested();

            if (member.DeclaredAccessibility == Accessibility.Private)
                continue;

            // TODO: write only
            if (member is IPropertySymbol property && property.ValidFor(in this))
            {
                var meta = new SymbolMetadata(property, in Symbols);
                members.Add(new MemberBinding(member, property.Type, in meta));

            }
            else if (member is IFieldSymbol field && field.ValidFor(in this))
            {
                var meta = new SymbolMetadata(field, in Symbols);
                members.Add(new MemberBinding(member, field.Type, in meta));
            }
            else if (
                !SkipConstructor &&
                member is IMethodSymbol { MethodKind: MethodKind.Constructor } ctor)
            {
                foreach (var attr in ctor.GetAttributes())
                {
                    if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, Symbols.CsvConstructorAttribute))
                    {
                        if (constructor is not null)
                        {
                            Fail(Diagnostics.TwoConstructorsFound(Type, constructor));
                        }

                        if (ctor.DeclaredAccessibility == Accessibility.Private)
                        {
                            Fail(Diagnostics.PrivateConstructorFound(Type, ctor));
                        }

                        constructor = ctor;
                    }
                }

                if (ctor.Parameters.Length == 0)
                {
                    parameterlessCtor ??= ctor;
                }
            }
        }

        if (!SkipConstructor)
        {
            constructor ??= parameterlessCtor;
            if (constructor is null)
            {
                Fail(Diagnostics.NoConstructorFound(Type));
            }

            foreach (var parameter in constructor.Parameters)
            {
                ThrowIfCancellationRequested();

                if (parameter.Type.IsRefLikeType)
                {
                    Fail(Diagnostics.RefLikeConstructorParameterFound(Type, parameter));
                }

                if (parameter.RefKind is not RefKind.None and not RefKind.In)
                {
                    Fail(Diagnostics.RefConstructorParameterFound(Type, parameter));
                }

                var meta = new SymbolMetadata(parameter, in Symbols);
                Debug.Assert(meta.Scope != BindingScope.Write);
                parameters.Add(new ParameterBinding(parameter, in meta));
            }
        }

        if (members.Count == 0 && parameters.Count == 0)
        {
            Fail(Diagnostics.NoWritableMembersOrParametersFound(Type));
        }

        parameters.Sort((a, b) => a.ParameterPosition.CompareTo(b.ParameterPosition));
        return new TypeBindings(members.ToImmutable(), parameters.ToImmutable());
    }
}
