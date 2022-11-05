using System.Buffers;
using System.Linq.Expressions;
using System.Reflection;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Parsers;
using FlameCsv.Parsers.Text;
using FlameCsv.Readers;
using FlameCsv.Readers.Internal;
using FlameCsv.Runtime;

namespace FlameCsv.Tests;

public class Temp
{
    class Nakki : ParserBase<char, DateTime>
    {
        public override bool TryParse(ReadOnlySpan<char> span, out DateTime value)
        {
            value = DateTime.UnixEpoch;
            return true;
        }
    }

    class Obj
    {
        public int A { get; init; }
        public int B { get; set; }
        [CsvParserOverride(typeof(Nakki))] public DateTime C { get; set; }
    }

    [Fact]
    public void Testi()
    {
        var data = new[] { 0, 1, 2, 3, 4 };
        Range r = default;
        var test = data[r.End..];

        var bindings = new CsvBindingCollection<Obj>(
            new CsvBinding[]
            {
                new(0, typeof(Obj).GetProperty("A")!),
                new(1, typeof(Obj).GetProperty("B")!),
                new(2, typeof(Obj).GetProperty("C")!),
            });

        var configuration = new CsvReaderOptions<char>()
            .AddParser(new IntegerTextParser())
            .AddParser(new DateTimeTextParser("o"));

        var state = configuration.CreateState(bindings);

        var opts = CsvTokens<char>.Unix;
        using var bo = new BufferOwner<char>();
        var enumerator = new CsvColumnEnumerator<char>(
            "1,2,2015-01-01T00:00:00.0000000Z",
            in opts,
            3,
            0,
            ref bo._array);
        var asd = state.Parse(ref enumerator);

        var init = ReflectionUtil.CreateInitializer<int, int, DateTime, Obj>(
            typeof(Obj).GetProperty("A"),
            typeof(Obj).GetProperty("B"),
            typeof(Obj).GetProperty("C"));

        var result = init(1, 2, DateTime.UnixEpoch);
        Assert.Equal(1, result.A);
        Assert.Equal(2, result.B);

        var setter = ReflectionUtil.CreateSetter<Obj, int>(o => o.A);
        setter(result, 123);
        Assert.Equal(123, result.A);
    }

    [Fact]
    public void Should_Parse()
    {
        var seq = new ReadOnlySequence<char>("1,2,3,4,5,6".ToCharArray());

        var reader = new SequenceReader<char>(seq);

        var parser = new IntegerTextParser();
        var parsed = new List<int>();

        while (reader.TryReadTo(out ReadOnlySequence<char> column, ',', '\\'))
        {
            ReadValue(in column);
        }

        ReadValue(reader.UnreadSequence);

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, parsed);

        void ReadValue(in ReadOnlySequence<char> column)
        {
            parser.TryParse(column.FirstSpan, out int value);
            parsed.Add(value);
        }
    }

    public class Reada<T>
    {
        private readonly List<(MemberInfo member, ICsvParser<char> parser)> _config = new();

        public Reada<T> ReadMember<TProperty>(
            Expression<Func<T, TProperty>> propertyExpression,
            ICsvParser<char, TProperty> parser)
        {
            var memberInfo = ((MemberExpression)propertyExpression.Body).Member;
            _config.Add((memberInfo, parser));
            return this;
        }

        public T Read(IEnumerable<string> columns)
        {
            throw new NotSupportedException();
        }
    }
}
