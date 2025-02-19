using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;

// new CsvEnumerateBench().Flame_byte();
// return;

var config = DefaultConfig.Instance
    //.AddJob(Job.Default
    //    .WithStrategy(RunStrategy.Throughput)
    //    .WithId("Scalar")
    //    .WithEnvironmentVariable("DOTNET_EnableAVX2", "0")
    //    .WithEnvironmentVariable("DOTNET_EnableAVX512F", "0")
    //    .AsBaseline())
    .AddJob(Job.Default.WithStrategy(RunStrategy.Throughput));

BenchmarkRunner.Run<InvokeBench>(config);

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

public class InvokeBench
{
    [Benchmark]
    public void WithRef()
    {
        var value = new BigStruct();

        for (int i = 0; i < 1000; i++)
        {
            _withRef(ref value);
        }
    }

    [Benchmark]
    public void WithoutRef()
    {
        var value = new BigStruct();

        for (int i = 0; i < 1000; i++)
        {
            _withoutRef(value);
        }
    }

    private readonly Action<BigStruct> _withoutRef = value => { };
    private readonly DoSomething _withRef = (ref BigStruct value) => { };

    delegate void DoSomething(ref BigStruct value);

    public ref struct BigStruct
    {
        public int A;
        public int B;
        public int C;
        public int D;
        public int E;
        public int F;
        public int G;
        public int H;
        public int I;
        public int J;
        public int K;
        public int L;
        public int M;
        public int N;
        public int O;
        public int P;
    }
}
