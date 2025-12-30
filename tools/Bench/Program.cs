// #define DISASM
using System.Globalization;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using Perfolizer.Horology;
#if DISASM
using BenchmarkDotNet.Diagnosers;
#endif

// BenchmarkRunner.Run(
//     [
//         /**/
//         typeof(ReadObjects),
//         typeof(WriteObjects),
//         typeof(EnumerateBench),
//     ],
//     new Config(),
//     args
// );

BenchmarkRunner.Run<PeekFields>(new Config());

file class Config : ManualConfig
{
    const int Iters = 4;

    public Config()
    {
        AddExporter(HtmlExporter.Default);
        AddExporter(MarkdownExporter.GitHub);
        AddExporter(JsonExporter.BriefCompressed);
        AddExporter(
            new CsvExporter(
                CsvSeparator.Comma,
                new SummaryStyle(
                    cultureInfo: CultureInfo.InvariantCulture,
                    printUnitsInHeader: true,
                    printUnitsInContent: false,
                    printZeroValuesInContent: true,
                    timeUnit: null,
                    sizeUnit: null
                )
            )
        );

        AddLogger([.. DefaultConfig.Instance.GetLoggers()]);
        AddAnalyser([.. DefaultConfig.Instance.GetAnalysers()]);
        AddValidator([.. DefaultConfig.Instance.GetValidators()]);
        WithSummaryStyle(SummaryStyle.Default);

        AddColumnProvider(DefaultColumnProviders.Instance);

        AddJob(
            Job.Default.WithMinWarmupCount(Iters)
                .WithMaxWarmupCount(Iters * 2)
                .WithMinIterationCount(Iters)
                .WithMaxIterationCount(Iters * 3)
                .WithMinIterationTime(TimeInterval.FromSeconds(1))
                .WithGcServer(true)
#if DISASM
                .WithEnvironmentVariables(
                    OperatingSystem.IsMacOS()
                        ?
                        [
                            new EnvironmentVariable("COMPlus_JitDisasm", "TokenizeCore"),
                            new EnvironmentVariable("COMPlus_JitDisasmDiffable", "1"),
                            new EnvironmentVariable("COMPlus_TieredPGO", "1"),
                            new EnvironmentVariable("COMPlus_TC_QuickJitForLoops", "1"),
                            new EnvironmentVariable("COMPlus_ReadyToRun", "0"),
                        ]
                        : []
                )
#endif
        );

#if DISASM
        // requires mono on macOS
        if (!OperatingSystem.IsMacOS())
        {
            WithOptions(ConfigOptions.DisableLogFile);
            AddDiagnoser(new DisassemblyDiagnoser(new DisassemblyDiagnoserConfig(maxDepth: 2, printSource: true)));
        }
#else
        WithOptions(ConfigOptions.DisableLogFile);
#endif
    }
}
