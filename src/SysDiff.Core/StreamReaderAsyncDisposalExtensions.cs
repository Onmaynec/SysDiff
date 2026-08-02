namespace SysDiff.Core;

internal static class StreamReaderAsyncDisposalExtensions
{
    public static ValueTask DisposeAsync(this StreamReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        reader.Dispose();
        return ValueTask.CompletedTask;
    }
}
