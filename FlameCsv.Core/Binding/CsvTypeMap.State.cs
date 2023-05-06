using CommunityToolkit.HighPerformance.Helpers;

namespace FlameCsv.Binding;

public abstract partial class CsvTypeMap<T, TValue>
{
    /// <summary>
    /// Internal implementation detail of CsvTypeMap.
    /// </summary>
    protected struct BindingState
    {
        /// <summary>
        /// How many fields have been read so far.
        /// </summary>
        public readonly int Count => _count;

        /// <summary>
        /// Whether to include CSV content in exception messages.
        /// </summary>
        public bool ExposeContent { get; }

        /// <summary>
        /// Current reader options.
        /// </summary>
        public CsvReaderOptions<T> Options { get; }

        private readonly IEqualityComparer<string>? _stringComparer;
        private ulong _fieldMask;
        private int _count;

        internal BindingState(in CsvReadingContext<T> context)
        {
            Options = context.Options;
            ExposeContent = context.ExposeContent;

            var comparer = context.Options.Comparer;

            if (!ReferenceEquals(comparer, StringComparer.OrdinalIgnoreCase))
            {
                _stringComparer = comparer;
            }
        }

        /// <summary>
        /// Compares the header value to a member using the comparer from options.
        /// </summary>
        public readonly bool FieldMatches(string? first, string? second)
        {
            return (_stringComparer ?? StringComparer.OrdinalIgnoreCase).Equals(first, second);
        }

        /// <summary>
        /// Returns whether the field with the specified ID has been read.
        /// </summary>
        public readonly bool IsFieldRead(byte id)
        {
            return BitHelper.HasFlag(_fieldMask, id);
        }

        /// <summary>
        /// Sets the field with the specified ID as read, or returns false if it already was.
        /// </summary>
        public bool SetFieldRead(byte index)
        {
            if (BitHelper.HasFlag(_fieldMask, index))
            {
                _count++;
                return true;
            }

            BitHelper.SetFlag(ref _fieldMask, index, true);
            return false;
        }
    }
}
