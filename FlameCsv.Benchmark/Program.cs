using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;

var config = DefaultConfig.Instance
    //.AddJob(Job.Default
    //    .WithStrategy(RunStrategy.Throughput)
    //    .WithId("Scalar")
    //    .WithEnvironmentVariable("DOTNET_EnableAVX2", "0")
    //    .WithEnvironmentVariable("DOTNET_EnableAVX512F", "0")
    //    .AsBaseline())
    .AddJob(Job.Default
        .WithStrategy(RunStrategy.Throughput));

BenchmarkRunner.Run<CountEscapableBench>(config);

//var bb = new BindingBench();

//// Repeat 10 times
//for (var i = 0; i < 10; i++)
//{
//    bb.FlameTypeMap();
//}

//Console.ReadLine();

//bb.FlameTypeMap();

//Console.ReadLine();

//new ModeBench2().Old();

// Console.WriteLine("Process started: {0}", Environment.ProcessId);
//
// var cts = new CancellationTokenSource();
//
// // ReSharper disable once AccessToDisposedClosure
// Console.CancelKeyPress += (_, _) => cts.Cancel();
//
// var bench = new CsvStringEnumerateBench();
//
// try
// {
//     while (true)
//     {
//         bench.FlameCsv_Utf8();
//
//         if (cts.IsCancellationRequested)
//             break;
//     }
// }
// catch (OperationCanceledException) when (cts.IsCancellationRequested)
// {
// }
// finally
// {
//     cts.Dispose();
// }
