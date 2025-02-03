using System.Reflection;
using FlameCsv.SourceGen.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace FlameCsv.SourceGen.Tests;

public class TypeMapTests
{
    private static readonly MetadataReference[] _metadataReferences =
    [
        Basic.Reference.Assemblies.Net90.References.SystemRuntime,
        MetadataReference.CreateFromFile(typeof(Binding.CsvTypeMapAttribute<,>).Assembly.Location)
    ];

    [Fact]
    public void Test_Cacheability()
    {
        var compilation = CSharpCompilation.Create(
            "TestProject",
            [
                CSharpSyntaxTree.ParseText(
                    """
                    [FlameCsv.Binding.CsvTypeMap<char, TestClass>]
                    partial class TestTypeMap;

                    public class TestClass
                    {
                        public int Id { get; set; }
                        public string? Name { get; set; }
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

        // var fromSecondRun = (TypeMapModel)result.TrackedSteps["FlameCsv_TypeMap"][0].Outputs[0].Item1;
        // Assert.Equal(fromFirstRun, fromSecondRun);

        var output = Assert.Single(allOutputs);
        Assert.Equal(IncrementalStepRunReason.Cached, output.Reason);

        // Assert the driver use the cached result from typemap
        var assemblyNameOutputs = result.TrackedSteps["FlameCsv_TypeMap"].Single().Outputs;
        var output2 = Assert.Single(assemblyNameOutputs);
        Assert.Equal(IncrementalStepRunReason.Unchanged, output2.Reason);
    }

    [Fact]
    public void Should_Be_Equatable()
    {
        HashSet<Type> handled = [];
        AssertEquatable(typeof(TypeMapModel), handled);

        static void AssertEquatable(Type type, HashSet<Type> handled)
        {
            if (!handled.Add(type)) return;
            if (type.IsPrimitive || type == typeof(string)) return;
            if (type.IsAssignableTo(typeof(IEquatable<>).MakeGenericType(type))) return;

            if (type.GetInterfaces().Any(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>)))
            {
                var elementType = type.GetElementType() ?? type.GetGenericArguments()[0];

                // ReSharper disable once TailRecursiveCall
                AssertEquatable(elementType, handled);
                return;
            }

            foreach (var member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance))
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

                    public static partial class TypeMapBindingTests {
                    [CsvTypeField(memberName: "Id", "__id__")]
                    private class _Obj : ISomething
                    {
                        [CsvConstructor]
                        public _Obj(string? name = "\\test", DayOfWeek dof = DayOfWeek.Tuesday, DayOfWeek? dof2 = DayOfWeek.Wednesday)
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
