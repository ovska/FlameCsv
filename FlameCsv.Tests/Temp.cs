using System.Buffers;
using System.Linq.Expressions;
using System.Reflection;
using FlameCsv.Binding;
using FlameCsv.Binding.Attributes;
using FlameCsv.Extensions;
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

    class Testii
    {
        public int Id { get; }
        public string Name { get; }
        public bool IsEnabled { get; init; }

        public Testii(int id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    [Fact]
    public void ReflectionStuff()
    {
        var props = typeof(Testii).GetProperties();

        var ctor = typeof(Testii).GetConstructors()[0];
        var x = ctor.GetParameters();
        var fac = CsvStateExtensions.CreateValueFactory(new CsvBindingCollection<Testii>(new List<CsvBinding> {
            new CsvBinding(0, ctor.GetParameters()[0]),
            new CsvBinding(1, ctor.GetParameters()[1]),
            new CsvBinding(2, typeof(Testii).GetProperty(nameof(Testii.IsEnabled))!),
        }));

        var fac2 = (Func<int, string, bool, Testii>)fac;
        var obj = fac2(123, "test", true);
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
            _ = parser.TryParse(column.FirstSpan, out int value);
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
    }
}
