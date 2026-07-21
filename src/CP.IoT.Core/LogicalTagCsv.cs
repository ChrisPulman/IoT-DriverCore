// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace CP.IoT.Core;

/// <summary>Imports and exports logical tags as RFC 4180 CSV.</summary>
public static class LogicalTagCsv
{
    /// <summary>Comma delimiter used when no explicit delimiter is specified.</summary>
    private const char DefaultDelimiter = ',';

    /// <summary>Fixed CSV header field names in column order.</summary>
    private static readonly string[] Header =
    [
        "Name",
        "Address",
        "DataType",
        "GroupName",
        "Description",
        "Metadata",
        "AccessMode",
        "ScanIntervalMilliseconds",
    ];

    /// <summary>Exports tags to <paramref name="writer"/> using a comma delimiter and no cancellation.</summary>
    /// <param name="tags">The tags to export.</param>
    /// <param name="writer">The destination text writer.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    public static Task ExportAsync(IEnumerable<LogicalTag> tags, TextWriter writer) =>
        ExportAsync(tags, writer, DefaultDelimiter, CancellationToken.None);

    /// <summary>Exports tags to <paramref name="writer"/> using a comma delimiter.</summary>
    /// <param name="tags">The tags to export.</param>
    /// <param name="writer">The destination text writer.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    public static Task ExportAsync(IEnumerable<LogicalTag> tags, TextWriter writer, CancellationToken cancellationToken) =>
        ExportAsync(tags, writer, DefaultDelimiter, cancellationToken);

    /// <summary>Exports tags to <paramref name="writer"/> using the specified delimiter and no cancellation.</summary>
    /// <param name="tags">The tags to export.</param>
    /// <param name="writer">The destination text writer.</param>
    /// <param name="delimiter">The field delimiter character.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    public static Task ExportAsync(IEnumerable<LogicalTag> tags, TextWriter writer, char delimiter) =>
        ExportAsync(tags, writer, delimiter, CancellationToken.None);

    /// <summary>Exports tags to <paramref name="writer"/> using the specified delimiter.</summary>
    /// <param name="tags">The tags to export.</param>
    /// <param name="writer">The destination text writer.</param>
    /// <param name="delimiter">The field delimiter character.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous export operation.</returns>
    public static async Task ExportAsync(
        IEnumerable<LogicalTag> tags,
        TextWriter writer,
        char delimiter,
        CancellationToken cancellationToken)
    {
        if (tags is null)
        {
            throw new ArgumentNullException(nameof(tags));
        }

        if (writer is null)
        {
            throw new ArgumentNullException(nameof(writer));
        }

        ValidateDelimiter(delimiter);

        await WriteRowAsync(writer, Header, delimiter, cancellationToken).ConfigureAwait(false);

        foreach (var tag in tags)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (tag is null)
            {
                throw new ArgumentException("Tags cannot contain null entries.", nameof(tags));
            }

            await WriteRowAsync(
                writer,
                [
                    tag.Name,
                    tag.Address,
                    tag.DataType,
                    tag.GroupName,
                    tag.Description,
                    MetadataCodec.Encode(tag.Metadata),
                    tag.AccessMode.ToString(),
                    tag.ScanInterval?.TotalMilliseconds.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                ],
                delimiter,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Imports logical tags from <paramref name="reader"/> using a comma delimiter and no cancellation.</summary>
    /// <param name="reader">The source text reader containing RFC 4180 CSV.</param>
    /// <returns>The parsed logical tags.</returns>
    public static Task<IReadOnlyList<LogicalTag>> ImportAsync(TextReader reader) =>
        ImportAsync(reader, DefaultDelimiter, CancellationToken.None);

    /// <summary>Imports logical tags from <paramref name="reader"/> using a comma delimiter.</summary>
    /// <param name="reader">The source text reader containing RFC 4180 CSV.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The parsed logical tags.</returns>
    public static Task<IReadOnlyList<LogicalTag>> ImportAsync(TextReader reader, CancellationToken cancellationToken) =>
        ImportAsync(reader, DefaultDelimiter, cancellationToken);

    /// <summary>Imports logical tags from <paramref name="reader"/> using the specified delimiter and no cancellation.</summary>
    /// <param name="reader">The source text reader containing RFC 4180 CSV.</param>
    /// <param name="delimiter">The field delimiter character.</param>
    /// <returns>The parsed logical tags.</returns>
    public static Task<IReadOnlyList<LogicalTag>> ImportAsync(TextReader reader, char delimiter) =>
        ImportAsync(reader, delimiter, CancellationToken.None);

    /// <summary>Imports logical tags from <paramref name="reader"/> using the specified delimiter.</summary>
    /// <param name="reader">The source text reader containing RFC 4180 CSV.</param>
    /// <param name="delimiter">The field delimiter character.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The parsed logical tags.</returns>
    public static async Task<IReadOnlyList<LogicalTag>> ImportAsync(
        TextReader reader,
        char delimiter,
        CancellationToken cancellationToken)
    {
        if (reader is null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        ValidateDelimiter(delimiter);
        cancellationToken.ThrowIfCancellationRequested();

        var rows = Parse(await reader.ReadToEndAsync().ConfigureAwait(false), delimiter);

        if (rows.Count == 0)
        {
            return [];
        }

        if (!rows[0].SequenceEqual(Header, StringComparer.OrdinalIgnoreCase))
        {
            throw new FormatException(
                "The CSV header must include Name,Address,DataType,GroupName,Description,Metadata,AccessMode,ScanIntervalMilliseconds.");
        }

        var tags = new List<LogicalTag>(Math.Max(rows.Count - 1, 0));

        for (var index = 1; index < rows.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var row = rows[index];

            if (row.Count == 1 && row[0].Length == 0)
            {
                continue;
            }

            if (row.Count != Header.Length)
            {
                throw new FormatException(
                    $"CSV row {index + 1} contains {row.Count} fields; expected {Header.Length}.");
            }

            tags.Add(ParseTagRow(row, index + 1));
        }

        return tags;
    }

    /// <summary>Parses a single CSV data row into a <see cref="LogicalTag"/>.</summary>
    /// <param name="row">The field values from one CSV row.</param>
    /// <param name="rowNumber">The 1-based row number used in error messages.</param>
    /// <returns>The parsed <see cref="LogicalTag"/>.</returns>
    private static LogicalTag ParseTagRow(List<string> row, int rowNumber)
    {
        if (!Enum.TryParse<LogicalTagAccessMode>(row[6], true, out var accessMode)
            || !Enum.IsDefined(typeof(LogicalTagAccessMode), accessMode))
        {
            throw new FormatException($"CSV row {rowNumber} contains an invalid access mode.");
        }

        var scanInterval = ParseScanInterval(row[7], rowNumber);

        return new LogicalTag(
            row[0],
            row[1],
            row[2],
            new LogicalTagOptions
            {
                GroupName = row[3],
                Description = row[4],
                Metadata = MetadataCodec.Decode(row[5]),
                AccessMode = accessMode,
                ScanInterval = scanInterval,
            });
    }

    /// <summary>Parses an optional scan interval from its millisecond string representation.</summary>
    /// <param name="value">The raw field value; may be empty.</param>
    /// <param name="rowNumber">The 1-based row number used in error messages.</param>
    /// <returns>The parsed <see cref="TimeSpan"/>, or <see langword="null"/> when <paramref name="value"/> is empty.</returns>
    private static TimeSpan? ParseScanInterval(string value, int rowNumber)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (!double.TryParse(
                value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var milliseconds)
            || milliseconds <= 0)
        {
            throw new FormatException($"CSV row {rowNumber} contains an invalid scan interval.");
        }

        return TimeSpan.FromMilliseconds(milliseconds);
    }

    /// <summary>Writes one CSV row including a CRLF line ending.</summary>
    /// <param name="writer">The destination text writer.</param>
    /// <param name="fields">The field values to write.</param>
    /// <param name="delimiter">The field delimiter character.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that represents the asynchronous write operation.</returns>
    private static async Task WriteRowAsync(
        TextWriter writer,
        IEnumerable<string> fields,
        char delimiter,
        CancellationToken cancellationToken)
    {
        var escaped = string.Join(delimiter.ToString(), fields.Select(field => Escape(field ?? string.Empty, delimiter)));
        cancellationToken.ThrowIfCancellationRequested();
        await writer.WriteAsync($"{escaped}\r\n").ConfigureAwait(false);
    }

    /// <summary>RFC 4180-escapes a single CSV field.</summary>
    /// <param name="value">The raw field value.</param>
    /// <param name="delimiter">The active field delimiter.</param>
    /// <returns>The field value, quoted if necessary.</returns>
    private static string Escape(string value, char delimiter) =>
        value.IndexOfAny([delimiter, '"', '\r', '\n']) < 0
            ? value
            : '"' + value.Replace("\"", "\"\"") + '"';

    /// <summary>Parses CSV text into a list of rows, each containing a list of field values.</summary>
    /// <param name="text">The complete CSV document text.</param>
    /// <param name="delimiter">The field delimiter character.</param>
    /// <returns>The parsed rows.</returns>
    private static List<List<string>> Parse(string text, char delimiter)
    {
        var context = new CsvParseContext();

        for (var index = 0; index < text.Length; index++)
        {
            index = context.ProcessChar(text, index, delimiter);
        }

        context.Complete();
        return context.Rows;
    }

    /// <summary>Validates that <paramref name="delimiter"/> is a legal CSV delimiter character.</summary>
    /// <param name="delimiter">The delimiter to validate.</param>
    private static void ValidateDelimiter(char delimiter)
    {
        if (delimiter is not ('"' or '\r' or '\n'))
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(delimiter));
    }

    /// <summary>Holds mutable state for the RFC 4180 CSV parser.</summary>
    private sealed class CsvParseContext
    {
        /// <summary>Backing field for <see cref="CurrentRow"/>.</summary>
        private List<string> _currentRow = [];

        /// <summary>Gets the completed rows accumulated so far.</summary>
        internal List<List<string>> Rows { get; } = [];

        /// <summary>Gets the fields of the row currently being assembled.</summary>
        private List<string> CurrentRow => _currentRow;

        /// <summary>Gets the characters of the field currently being assembled.</summary>
        private StringBuilder Field { get; } = new();

        /// <summary>Gets or sets a value indicating whether the parser is inside a quoted field.</summary>
        private bool Quoted { get; set; }

        /// <summary>Gets or sets a value indicating whether the parser just closed a quoted field.</summary>
        private bool AfterClosingQuote { get; set; }

        /// <summary>Gets or sets a value indicating whether the parser is at the start of a field.</summary>
        private bool AtStartOfField { get; set; } = true;

        /// <summary>Processes one character of the CSV input and returns the updated character index.</summary>
        /// <param name="text">The full CSV text.</param>
        /// <param name="index">The current character index.</param>
        /// <param name="delimiter">The active field delimiter.</param>
        /// <returns>The index to continue from (may be advanced for CRLF pairs or escaped quotes).</returns>
        internal int ProcessChar(string text, int index, char delimiter)
        {
            var current = text[index];

            if (Quoted)
            {
                return ProcessQuoted(text, index, current);
            }

            if (current == '"')
            {
                return ProcessOpenQuote(index);
            }

            if (current == delimiter)
            {
                return HandleDelimiter(index);
            }

            return current is '\r' or '\n' ? HandleLineEnding(text, index, current) : HandleRegularChar(index, current);
        }

        /// <summary>Finalizes parsing by flushing any trailing partial row.</summary>
        internal void Complete()
        {
            if (Quoted)
            {
                throw new FormatException("A quoted CSV field was not terminated.");
            }

            if (Field.Length == 0 && CurrentRow.Count == 0)
            {
                return;
            }

            FinalizeField();
            Rows.Add(CurrentRow);
        }

        /// <summary>Processes one character while inside a quoted field.</summary>
        /// <param name="text">The full CSV text.</param>
        /// <param name="index">The current character index.</param>
        /// <param name="current">The current character.</param>
        /// <returns>The updated character index.</returns>
        private int ProcessQuoted(string text, int index, char current)
        {
            if (current != '"')
            {
                _ = Field.Append(current);
                return index;
            }

            return HandleQuoteInQuoted(text, index);
        }

        /// <summary>Handles a quote character encountered while inside a quoted field.</summary>
        /// <param name="text">The full CSV text.</param>
        /// <param name="index">The current character index.</param>
        /// <returns>The updated character index.</returns>
        private int HandleQuoteInQuoted(string text, int index)
        {
            if (index + 1 < text.Length && text[index + 1] == '"')
            {
                _ = Field.Append('"');
                return index + 1;
            }

            Quoted = false;
            AfterClosingQuote = true;
            return index;
        }

        /// <summary>Handles an opening quote character at the start of a field.</summary>
        /// <param name="index">The current character index.</param>
        /// <returns>The unchanged character index.</returns>
        private int ProcessOpenQuote(int index)
        {
            if (!AtStartOfField)
            {
                throw new FormatException("An unescaped quote was found in a CSV field.");
            }

            Quoted = true;
            AtStartOfField = false;
            return index;
        }

        /// <summary>Handles a delimiter character by finalizing the current field.</summary>
        /// <param name="index">The current character index.</param>
        /// <returns>The unchanged character index.</returns>
        private int HandleDelimiter(int index)
        {
            FinalizeField();
            AtStartOfField = true;
            AfterClosingQuote = false;
            return index;
        }

        /// <summary>Handles a CR or LF character by finalizing the current row.</summary>
        /// <param name="text">The full CSV text.</param>
        /// <param name="index">The current character index.</param>
        /// <param name="current">The current character.</param>
        /// <returns>The updated character index, advanced past a CRLF pair.</returns>
        private int HandleLineEnding(string text, int index, char current)
        {
            if (current == '\r' && index + 1 < text.Length && text[index + 1] == '\n')
            {
                index++;
            }

            FinalizeField();
            Rows.Add(CurrentRow);
            _currentRow = [];
            AtStartOfField = true;
            AfterClosingQuote = false;
            return index;
        }

        /// <summary>Handles a regular (non-special) character by appending it to the current field.</summary>
        /// <param name="index">The current character index.</param>
        /// <param name="current">The current character.</param>
        /// <returns>The unchanged character index.</returns>
        private int HandleRegularChar(int index, char current)
        {
            if (AfterClosingQuote)
            {
                throw new FormatException("A quoted CSV field must be followed by a delimiter or line ending.");
            }

            _ = Field.Append(current);
            AtStartOfField = false;
            return index;
        }

        /// <summary>Moves the current field content into <see cref="CurrentRow"/> and resets <see cref="Field"/>.</summary>
        private void FinalizeField()
        {
            CurrentRow.Add(Field.ToString());
            _ = Field.Clear();
        }
    }
}
