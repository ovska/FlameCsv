using CommunityToolkit.HighPerformance;

// Console.WriteLine("Beginning fuzzing");
//
// var data = File.ReadAllBytes(
//     @"C:\Users\Sipi\source\repos\FlameCsv\FlameCsv.Fuzzing\crash-5df1a9a3d64c7d11a06e632577cf3b70fab65021");
//
// for (int i = 0; i < 32; i++)
// {
//     _ = Task.Run(
//         () =>
//         {
//             while (true)
//             {
//                 Execute(data);
//                 Thread.Yield();
//             }
//         });
// }
//
// await Task.Delay(-1);
// return;

static void Execute(ReadOnlySpan<byte> data)
{
    try
    {
        using var pool = new BoundedMemoryPool<byte>();

        using var memory = pool.Rent(Math.Max(256, data.Length));
        data.CopyTo(memory.Memory.Span);

        using var stream = memory.Memory.Slice(0, data.Length).AsStream();

        var options = new CsvOptions<byte> { MemoryPool = pool };
        var readOptions = new CsvReaderOptions { NoDirectBufferAccess = true };

        foreach (var r in CsvParser.Create(options, CsvPipeReader.Create(stream, pool, readOptions)).ParseRecords())
        {
            for (int i = 0; i < r.FieldCount; i++)
            {
                _ = r[i];
            }
        }
    }
    catch (CsvFormatException)
    {
        // invalid CSV produces this exception
    }
}

Fuzzer.LibFuzzer.Run(Execute);
