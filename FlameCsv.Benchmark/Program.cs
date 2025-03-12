using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using Perfolizer.Metrology;

#if RELEASE
IConfig config = DefaultConfig.Instance;
#else
IConfig config = new DebugInProcessConfig();
#endif

//config.AddJob(Job.Default
//    .WithStrategy(RunStrategy.Throughput)
//    .WithId("Scalar")
//    .WithEnvironmentVariable("DOTNET_EnableAVX2", "0")
//    .WithEnvironmentVariable("DOTNET_EnableAVX", "0")
//    .WithEnvironmentVariable("DOTNET_EnableAVX2", "0")
//    .WithEnvironmentVariable("DOTNET_EnableSSE", "0")
//    .WithEnvironmentVariable("DOTNET_EnableSSE2", "0")
//    .WithEnvironmentVariable("DOTNET_EnableSSE3", "0")
//    .WithEnvironmentVariable("DOTNET_EnableSSSE3", "0")
//    .WithEnvironmentVariable("DOTNET_EnableSSE41", "0")
//    .WithEnvironmentVariable("DOTNET_EnableSSE42", "0")
//    .WithEnvironmentVariable("DOTNET_EnableAVX512F", "0");
config.AddJob(Job.Default.WithStrategy(RunStrategy.Throughput).AsBaseline());

config.AddExporter(JsonExporter.BriefCompressed);
config.AddExporter(
    new CsvExporter(
        CsvSeparator.CurrentCulture,
        new SummaryStyle(
            cultureInfo: System.Globalization.CultureInfo.InvariantCulture,
            printUnitsInHeader: true,
            printUnitsInContent: false,
            timeUnit: Perfolizer.Horology.TimeUnit.Millisecond,
            sizeUnit: SizeUnit.KB)));

BenchmarkRunner.Run<WriteBench>(config, args);
