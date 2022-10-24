BenchmarkRunner.Run<CsvReadBench>();

// Console.WriteLine("Process started: {0}", Environment.ProcessId);
//
// var cts = new CancellationTokenSource();
//
// // ReSharper disable once AccessToDisposedClosure
// Console.CancelKeyPress += (_, _) => cts.Cancel();
//
// var bench = new CsvReadBench();
//
// try
// {
//     while (true)
//     {
//         await bench.Custom();
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
