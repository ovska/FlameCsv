using System.Text;

namespace FlameCsv.Fuzzing.Scenarios;

public static class ScenarioRunner
{
    private static readonly PoisonPagePlacement[] _placements = [PoisonPagePlacement.After, PoisonPagePlacement.Before];

    public static void Run<TScenario>(ReadOnlySpan<byte> data)
        where TScenario : IScenario
    {
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
                    return;
                }

                ReadOnlyMemory<char> chars = charMemory.Memory.Slice(0, written);
                TScenario.Run(chars, placement);
            }
        }
    }
}
