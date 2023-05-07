using System.Buffers;
using System.Linq.Expressions;
using System.Reflection;
using FlameCsv.Binding;
using FlameCsv.Parsers;
using FlameCsv.Parsers.Text;
using FlameCsv.Tests.TestData;

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
        public string? Name { get; set; }
        public bool IsEnabled { get; set; }
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
    public void Should_Parse()
    {
        var seq = new ReadOnlySequence<char>("1,2,3,4,5,6".ToCharArray());

        var reader = new SequenceReader<char>(seq);

        var parser = new IntegerTextParser();
        var parsed = new List<int>();

        while (reader.TryReadTo(out ReadOnlySequence<char> field, ',', '\\'))
        {
            ReadValue(in field);
        }

        ReadValue(reader.UnreadSequence);

        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 }, parsed);

        void ReadValue(in ReadOnlySequence<char> field)
        {
            _ = parser.TryParse(field.FirstSpan, out int value);
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
