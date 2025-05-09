namespace FlameCsv.Reading.Unescaping;

internal interface IIndexOfUnescaper<T>
    where T : unmanaged, IBinaryInteger<T>
{
    static abstract int UnescapedLength(int fieldLength, uint specialCount);

    bool AllSpecialConsumed { get; }

    /// <summary>
    /// Finds the index of the next escape character sequence.
    /// </summary>
    /// <param name="value">Field to seek in</param>
    /// <returns></returns>
    int FindNext(ReadOnlySpan<T> value);

    /// <summary>
    /// Ensure that all special characters have been consumed.
    /// </summary>
    void ValidateState(ReadOnlySpan<T> field);
}
