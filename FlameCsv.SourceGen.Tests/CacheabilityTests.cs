using System.Collections.Immutable;
using System.Reflection;
using Basic.Reference.Assemblies;
using FlameCsv.Attributes;
using FlameCsv.SourceGen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.SourceGen.Tests;

public class TypeMapTests
{
    private static readonly MetadataReference[] _metadataReferences =
    [
        Net90.References.SystemRuntime,
        MetadataReference.CreateFromFile(typeof(CsvTypeMapAttribute<,>).Assembly.Location),
    ];

    [Fact]
    public void Test_Cacheability()
    {
        CSharpCompilation compilation = CSharpCompilation.Create(
            "TestProject",
            [
                CSharpSyntaxTree.ParseText(
                    """
                    [FlameCsv.Attributes.CsvTypeMap<char, TestClass>]
                    partial class TestTypeMap;

                    public class TestClass
                    {
                        public int Id { get; set; }
                        public string? Name { get; set; }
                    }
                    """,
                    cancellationToken: TestContext.Current.CancellationToken),
            ],
            _metadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        TypeMapGenerator generator = new();
        ISourceGenerator sourceGenerator = generator.AsSourceGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            [sourceGenerator],
            driverOptions: new GeneratorDriverOptions(default, true));

        // Run the generator once
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);
        // Assert.Equal(
        //     IncrementalStepRunReason.New,
        //     driver.GetRunResult().Results.Single().TrackedOutputSteps.Single(x => x.Key != "FlameCsv_Diagnostics").Value.Single().Outputs.Single().Reason);

        Assert.All(
            driver
                .GetRunResult()
                .Results.Single()
                .TrackedOutputSteps.Single(x => x.Key != "FlameCsv_Diagnostics")
                .Value,
            step => step.Outputs.All(x => x.Reason == IncrementalStepRunReason.New));

        // Update the compilation and rerun the generator
        compilation = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText("// dummy", cancellationToken: TestContext.Current.CancellationToken));
        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        // Assert the driver doesn't recompute the output
        GeneratorRunResult result = driver.GetRunResult().Results.Single();
        IEnumerable<(object Value, IncrementalStepRunReason Reason)> allOutputs = result
            .TrackedOutputSteps.SelectMany(outputStep => outputStep.Value)
            .SelectMany(output => output.Outputs);

        Assert.All(allOutputs, output => Assert.Equal(IncrementalStepRunReason.Cached, output.Reason));

        // Assert the driver use the cached result from typemap
        ImmutableArray<(object Value, IncrementalStepRunReason Reason)> assemblyNameOutputs
            = result.TrackedSteps["FlameCsv_TypeMap"].Single().Outputs;
        (object Value, IncrementalStepRunReason Reason) output2 = Assert.Single(assemblyNameOutputs);
        Assert.Equal(IncrementalStepRunReason.Cached, output2.Reason);
    }

    [Fact]
    public void Should_Be_Equatable()
    {
        HashSet<Type> handled = [];
        AssertEquatable(typeof(TypeMapModel), handled);

        static void AssertEquatable(Type type, HashSet<Type> handled)
        {
            if (!handled.Add(type))
            {
                return;
            }

            if (type.IsPrimitive || type == typeof(string))
            {
                return;
            }

            if (type.IsAssignableTo(typeof(IEquatable<>).MakeGenericType(type)))
            {
                return;
            }

            if (type.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                Type elementType = type.GetElementType() ?? type.GetGenericArguments()[0];

                // ReSharper disable once TailRecursiveCall
                AssertEquatable(elementType, handled);
                return;
            }

            foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            {
                if (member is PropertyInfo property)
                {
                    AssertEquatable(property.PropertyType, handled);
                }
                else if (member is FieldInfo field)
                {
                    AssertEquatable(field.FieldType, handled);
                }
            }
        }
    }

#if false // debugging
    [Fact]
    public void Diagnostics_Should_Equate()
    {
        Diagnostic d1 = GetDiagnostic();
        Diagnostic d2 = GetDiagnostic();
        Assert.Equal(d1, d2);

        static Diagnostic GetDiagnostic()
        {
            var compilation = CSharpCompilation.Create(
                nameof(Diagnostics_Should_Equate),
                [
                    CSharpSyntaxTree.ParseText(
                        """
                        class TestClass 
                        {
                            public int Id { get; set; }
                            public string? Name { get; set; }
                        }
                        """)
                ],
                [Basic.Reference.Assemblies.Net90.References.SystemRuntime],
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

            var semanticModel = compilation.GetSemanticModel(compilation.SyntaxTrees.Single());
            var namedTypeSymbol = semanticModel.GetDeclaredSymbol(
                semanticModel
                    .SyntaxTree.GetRoot()
                    .DescendantNodes()
                    .OfType<ClassDeclarationSyntax>()
                    .Single(c => c.Identifier.Text == "TestClass"))!;

            return Diagnostics.NoReadableMembers(namedTypeSymbol);
        }
    }

    [Fact]
    public void A_Test()
    {
        var compilation = CSharpCompilation.Create(
            "TestProject",
            [
                CSharpSyntaxTree.ParseText(
                    """
                    using FlameCsv.Binding;
                    using FlameCsv.Binding.Attributes;
                    using FlameCsv.Converters;
                    using System;

                    namespace TestiCompile;

                    public static partial class TypeMapBindingTests
                    {
                        [CsvTypeField(memberName: "Id", "__id__")]
                        [CsvTypeField(memberName: "dof", "doeeeef", IsParameter = true)]
                        [CsvTypeField(memberName: "Xyzz", "aaaaasd", IsRequired = false)]
                        private class _Obj : ISomething
                        {
                            [CsvConstructor]
                            public _Obj(string? name = "\\test", DayOfWeek dof = DayOfWeek.Sunday, DayOfWeek? dof2 = DayOfWeek.Wednesday)
                            {
                                Name = name;
                            }
                        
                            public int Id { get; set; }
                            public string? Name { get; set; }
                        
                            [CsvConverter<char, EnumTextConverter<DayOfWeek>>]
                            public DayOfWeek DOF { get; set; }
                        
                            public int? NullableInt { get; set; }
                            public DayOfWeek? NullableDOF { get; set; }
                        
                            bool ISomething.Xyzz { get; set; }
                        }
                        
                        [CsvTypeMap<char, _Obj>]
                        private partial class Test;
                        
                        private interface ISomething
                        {
                            bool Xyzz { get; set; }
                        }
                    }
                    """)
            ],
            _metadataReferences,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new TypeMapGenerator();
        var sourceGenerator = generator.AsSourceGenerator();

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [sourceGenerator],
            driverOptions: new GeneratorDriverOptions(default, trackIncrementalGeneratorSteps: true));

        // Run the generator once
        driver = driver.RunGenerators(compilation);
        Assert.Equal(
            IncrementalStepRunReason.New,
            driver.GetRunResult().Results.Single().TrackedOutputSteps.Single().Value.Single().Outputs.Single().Reason);

        // var fromFirstRun = (TypeMapModel)driver
        //     .GetRunResult()
        //     .Results.Single()
        //     .TrackedSteps["FlameCsv_TypeMap"]
        //     .Single()
        //     .Outputs.Single()
        //     .Item1;

        // Update the compilation and rerun the generator
        compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("// dummy"));
        driver = driver.RunGenerators(compilation);

        // Assert the driver doesn't recompute the output
        var result = driver.GetRunResult().Results.Single();
        var allOutputs = result
            .TrackedOutputSteps.SelectMany(outputStep => outputStep.Value)
            .SelectMany(output => output.Outputs);
    }
#endif
}
