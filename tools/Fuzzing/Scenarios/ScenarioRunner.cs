using System.Text;
using CommunityToolkit.HighPerformance;
using CommunityToolkit.HighPerformance.Buffers;

namespace FlameCsv.Fuzzing.Scenarios;

public static class ScenarioRunner
{
    private static readonly PoisonPagePlacement[] _placements = [PoisonPagePlacement.After, PoisonPagePlacement.Before];

    public static void Run<TScenario>(Stream stream)
        where TScenario : IScenario
    {
        using var abw = new ArrayPoolBufferWriter<byte>(Environment.SystemPageSize);
        stream.CopyTo(abw.AsStream());

        ReadOnlySpan<byte> data = abw.WrittenSpan;

        foreach (var placement in _placements)
        {
            using (var byteMemory = PooledBoundedMemory<byte>.Rent(data.Length, placement))
            {
                data.CopyTo(byteMemory.Span);
                ReadOnlyMemory<byte> bytes = byteMemory.Memory.Slice(0, data.Length);
                TScenario.Run(bytes, placement);
            }

            if (TScenario.SupportsUtf16)
            {
                using var charMemory = PooledBoundedMemory<char>.Rent(data.Length, placement);
                int written;

                try
                {
                    written = Encoding.UTF8.GetChars(data, charMemory.Span);
                }
                catch (ArgumentException e) when (e.ParamName == "chars")
                {
                    continue;
                }

                ReadOnlyMemory<char> chars = charMemory.Memory.Slice(0, written);
                TScenario.Run(chars, placement);
            }
        }
    }
}
