namespace FlameCsv.SourceGen.Bindings;

internal readonly struct IndexBinding
{
    public int Index { get; }
    public ISymbol? Symbol { get; }

    public IndexBinding(int index, ISymbol? symbol = null)
    {
        Index = index;
        Symbol = symbol;
    }
}

internal static class IndexAttributeBinder
{
    public static (List<IndexBinding>?, string?) TryGetIndexBindings(
        ITypeSymbol type,
        in KnownSymbols symbols,
        bool write)
    {
#if true
        return (null, "Index binding not yet supported");
#else
        List<IndexBinding> list = [];

        foreach (var attr in type.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, symbols.CsvIndexTargetAttribute))
            {
                if (!IsValidScope(attr, write))
                    continue;

                int index = (int)attr.ConstructorArguments[0].Value!;
                string? memberName = attr.ConstructorArguments[1].Value as string;

                if (TryFindMember(type, memberName, out var result))
                {
                    list.Add(new IndexBinding(index, result));
                }
            }
            else if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, symbols.CsvIndexIgnoreAttribute))
            {
                if (!IsValidScope(attr, write))
                    continue;

                foreach (var value in attr.ConstructorArguments[0].Values)
                {
                    int index = (int)value.Value!;

                    if (!HasIgnoredIndex(index, list))
                        list.Add(new IndexBinding(index));
                }
            }
        }

        foreach (var member in type.GetMembers())
        {
            if (member is not IPropertySymbol or IFieldSymbol)
                continue;

            foreach (var attr in member.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, symbols.CsvIndexAttribute))
                {
                    if (!IsValidScope(attr, write))
                        continue;

                    list.Add(new IndexBinding((int)attr.ConstructorArguments[0].Value!, member));
                }
            }
        }

        // TODO
        //if (!write)
        //{
        //    foreach (var parameter in constructor!.Parameters)
        //    {
        //        bool found = false;

        //        foreach (var attr in parameter.GetAttributes())
        //        {
        //            if (SymbolEqualityComparer.Default.Equals(attr.AttributeClass, symbols.CsvIndexAttribute))
        //            {
        //                list.Add(new IndexBinding((int)attr.ConstructorArguments[0].Value!, parameter));
        //                found = true;
        //                break;
        //            }
        //        }

        //        if (!found && !parameter.HasExplicitDefaultValue)
        //        {
        //            throw new Exception();
        //        }
        //    }
        //}

        return (list, GetError(list));

        static bool HasIgnoredIndex(int index, List<IndexBinding> list)
        {
            foreach (var binding in list)
            {
                if (binding.Index == index && binding.Symbol is null)
                    return true;
            }

            return false;
        }

        static bool IsValidScope(AttributeData attribute, bool write)
        {
            foreach (var kvp in attribute.NamedArguments)
            {
                if (kvp.Key == "Scope")
                {
                    BindingScope scope = (BindingScope)kvp.Value.Value!;
                    return write switch
                    {
                        true => scope != BindingScope.Read,
                        false => scope != BindingScope.Write,
                    };
                }
            }

            return true;
        }

        static bool TryFindMember(ITypeSymbol type, string? name, out ISymbol result)
        {
            if (name is not null)
            {
                foreach (var member in type.GetMembers(name))
                {
                    if (member is IPropertySymbol or IFieldSymbol)
                    {
                        result = member;
                        return true;
                    }
                }
            }

            result = null!;
            return false;
        }

        static string? GetError(List<IndexBinding> bindings)
        {
            if (bindings.Count == 0)
                return "Index bindings not configured";

            if (bindings.TrueForAll(static b => b.Symbol is null))
                return "All index bindings were ignored";

            bindings.Sort(static (a, b) => a.Index.CompareTo(b.Index));

            int nextIndex = 0;

            foreach (var binding in bindings)
            {
                if (binding.Index != nextIndex)
                    return $"Index {nextIndex} not valid, got {binding.Index} instead";

                nextIndex++;

                // ignored?
                if (binding.Symbol is null)
                    continue;

                foreach (var inner in bindings)
                {
                    if (inner.Index == binding.Index &&
                        !SymbolEqualityComparer.Default.Equals(inner.Symbol, binding.Symbol))
                    {
                        return $"Index {inner.Index} configured multiple times";
                    }
                }
            }

            return null;
        }
#endif
    }
}
