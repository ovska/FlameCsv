#if FEATURE_PARALLEL
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using FlameCsv.Reading;
using FlameCsv.Reading.Parallel;

// ReSharper disable UnusedType.Local
// ReSharper disable UnusedMember.Local
// ReSharper disable PrivateFieldCanBeConvertedToLocalVariable

namespace FlameCsv.Benchmark;

[MemoryDiagnoser(displayGenColumns: true)]
[HideColumns("Error", "StdDev")]
public class ParallelBench
{
    private static readonly string _data = File.ReadAllText(
        @"C:\Users\Sipi\source\repos\FlameCsv\FlameCsv.Benchmark\Data\65K_Records_Data.csv"
    );

    [Benchmark]
    public void Sync()
    {
        Dictionary<string, decimal> totalRevenueByCountry = new();
        Dictionary<string, int> unitsSoldByCountry = new();

        var trbcl = totalRevenueByCountry.GetAlternateLookup<ReadOnlySpan<char>>();
        var tsbcl = unitsSoldByCountry.GetAlternateLookup<ReadOnlySpan<char>>();

        long totalUnitsSold = 0;

        CsvConverter<char, decimal> dc = CsvOptions<char>.Default.GetConverter<decimal>();
        CsvConverter<char, int> ic = CsvOptions<char>.Default.GetConverter<int>();

        foreach (ref readonly var record in CsvReader.Enumerate(_data))
        {
            ReadOnlySpan<char> country = record.GetField(1);

            if (trbcl.TryGetValue(country, out decimal totalRevenue))
            {
                trbcl[country] = totalRevenue + record.ParseField(dc, 11);
            }
            else
            {
                trbcl[country] = record.ParseField(dc, 11);
            }

            if (tsbcl.TryGetValue(country, out int unitsSold))
            {
                tsbcl[country] = unitsSold + record.ParseField<int>(8);
            }
            else
            {
                tsbcl[country] = record.ParseField<int>(8);
            }

            totalUnitsSold += record.ParseField(ic, 8);
        }

        _ = totalUnitsSold;
    }

    [Benchmark]
    public void Parallel()
    {
        CsvParallel.Enumerate<object?, Accumulator>(new(_data.AsMemory()), new Accumulator()).ForAll(_ => { });
    }

    readonly struct Accumulator : ICsvParallelTryInvoke<char, object?>
    {
        private readonly CsvConverter<char, decimal> _cdecimal = CsvOptions<char>.Default.Aot.GetConverter<decimal>();
        private readonly CsvConverter<char, int> _cint = CsvOptions<char>.Default.Aot.GetConverter<int>();

        private readonly StrongBox<long> _totalUnitsSold = new();
        private readonly ConcurrentDictionary<string, StrongBox<decimal>> _totalRevenueByCountry;
        private readonly ConcurrentDictionary<string, StrongBox<int>> _unitsSoldByCountry;

        private readonly ConcurrentDictionary<string, StrongBox<decimal>>.AlternateLookup<ReadOnlySpan<char>> _trbcl;
        private readonly ConcurrentDictionary<string, StrongBox<int>>.AlternateLookup<ReadOnlySpan<char>> _tsbcl;

        private readonly Lock _lock = new();

        public Accumulator()
        {
            _totalRevenueByCountry = [];
            _unitsSoldByCountry = [];
            _trbcl = _totalRevenueByCountry.GetAlternateLookup<ReadOnlySpan<char>>();
            _tsbcl = _unitsSoldByCountry.GetAlternateLookup<ReadOnlySpan<char>>();
        }

        public bool TryInvoke<TRecord>(
            scoped ref TRecord record,
            in CsvParallelState state,
            [NotNullWhen(true)] out object? result
        )
            where TRecord : ICsvFields<char>, allows ref struct
        {
            result = null!;

            ReadOnlySpan<char> country = record[1];
            _ = _cint.TryParse(record[8], out int unitsSold);
            _ = _cdecimal.TryParse(record[11], out decimal totalRevenue);

            Interlocked.Add(ref _totalUnitsSold.Value, unitsSold);

            if (!_trbcl.TryGetValue(country, out var totalRevenueValue))
            {
                if (!_trbcl.TryAdd(country, totalRevenueValue = new()))
                {
                    totalRevenueValue = _trbcl[country];
                }
            }

            lock (_lock)
                totalRevenueValue.Value += totalRevenue;

            if (!_tsbcl.TryGetValue(country, out var unitsSoldValue))
            {
                if (!_tsbcl.TryAdd(country, unitsSoldValue = new()))
                {
                    unitsSoldValue = _tsbcl[country];
                }

                Interlocked.Add(ref unitsSoldValue.Value, unitsSold);
            }

            return true;
        }
    }

    /*
     C# class for the following CSV data:
Region,Country,Item Type,Sales Channel,Order Priority,Order Date,Order ID,Ship Date,Units Sold,Unit Price,Unit Cost,Total Revenue,Total Cost,Total Profit
Asia,South Africa,Fruits,Offline,M,2012-07-27,443368995,2012-07-28,1593,9.33,6.92,14862.69,11023.56,3839.13
Asia,Morocco,Clothes,Online,M,2013-09-14,667593514,2013-10-19,4611,109.28,35.84,503890.08,165258.24,338631.84
Asia,Papua New Guinea,Meat,Offline,M,2015-05-15,940995585,2015-06-04,360,421.89,364.69,151880.4,131288.4,20592
Asia,Djibouti,Clothes,Offline,H,2017-05-17,880811536,2017-07-02,562,109.28,35.84,61415.36,20142.08,41273.28
Asia,Slovakia,Beverages,Offline,L,2016-10-26,174590194,2016-12-04,3973,47.45,31.79,188518.85,126301.67,62217.18
     */

    class Row
    {
        public string? Region { get; set; }
        public string? Country { get; set; }
        public string? ItemType { get; set; }
        public string? SalesChannel { get; set; }
        public string? OrderPriority { get; set; }
        public DateTime OrderDate { get; set; }
        public long OrderId { get; set; }
        public DateTime ShipDate { get; set; }
        public int UnitsSold { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalCost { get; set; }
        public decimal TotalProfit { get; set; }
    }
}
#endif
