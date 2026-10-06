// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Represents one segment of a symbolic path.</summary>
/// <param name="name">The symbol name.</param>
/// <param name="indices">The array coordinates.</param>
internal sealed class S7SymbolPath(string name, int[] indices)
{
    /// <summary>The native protocol value 64.</summary>
    private const int ProtocolValue64 = 64;

    /// <summary>The native protocol value 4096.</summary>
    private const int ProtocolValue4096 = 4096;

    /// <summary>Gets the symbol name.</summary>
    internal string Name { get; } = name;

    /// <summary>Gets the array coordinates.</summary>
    internal int[] Indices { get; } = indices;

    /// <summary>Parses a symbolic path into its segments.</summary>
    /// <param name="path">The symbolic path.</param>
    /// <returns>The parsed segments.</returns>
    internal static S7SymbolPath[] Parse(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > ProtocolValue4096)
        {
            throw new ArgumentException("A bounded symbolic path is required.", nameof(path));
        }

        var parts = new List<S7SymbolPath>();
        var position = 0;
        while (position < path.Length)
        {
            var name = ReadName(path, ref position);
            var indices = ReadIndices(path, ref position);

            parts.Add(new(name, indices));
            if (parts.Count > ProtocolValue64)
            {
                throw new ArgumentException("The symbolic path is too deep.", nameof(path));
            }

            if (position == path.Length)
            {
                break;
            }

            var separator = path[position];
            position++;
            if (separator != '.' || position == path.Length)
            {
                throw new ArgumentException("A symbol separator is invalid.", nameof(path));
            }
        }

        return parts.ToArray();
    }

    /// <summary>Reads a symbol name.</summary>
    /// <param name="path">The symbolic path.</param>
    /// <param name="position">The current character position.</param>
    /// <returns>The symbol name.</returns>
    private static string ReadName(string path, ref int position)
    {
        var name = new StringBuilder();
        if (path[position] == '"')
        {
            ReadQuotedName(path, ref position, name);
        }
        else
        {
            while (position < path.Length && path[position] is not '.' and not '[')
            {
                if (path[position] is ']' or '"' || char.IsWhiteSpace(path[position]))
                {
                    throw new ArgumentException("Quote symbol names containing whitespace or punctuation.", nameof(path));
                }

                _ = name.Append(path[position]);
                position++;
            }
        }

        if (name.Length == 0)
        {
            throw new ArgumentException("A symbol name is empty.", nameof(path));
        }

        return name.ToString();
    }

    /// <summary>Reads a quoted symbol name.</summary>
    /// <param name="path">The symbolic path.</param>
    /// <param name="position">The current character position.</param>
    /// <param name="name">The name receiving decoded characters.</param>
    private static void ReadQuotedName(string path, ref int position, StringBuilder name)
    {
        position++;
        var closed = false;
        while (position < path.Length)
        {
            var character = path[position];
            position++;
            if (character != '"')
            {
                _ = name.Append(character);
            }
            else if (position < path.Length && path[position] == '"')
            {
                _ = name.Append('"');
                position++;
            }
            else
            {
                closed = true;
                break;
            }
        }

        if (closed)
        {
            return;
        }

        throw new ArgumentException("A quoted symbol name is unterminated.", nameof(path));
    }

    /// <summary>Reads optional array coordinates.</summary>
    /// <param name="path">The symbolic path.</param>
    /// <param name="position">The current character position.</param>
    /// <returns>The array coordinates.</returns>
    private static int[] ReadIndices(string path, ref int position)
    {
        var indices = new List<int>();
        if (position < path.Length && path[position] == '[')
        {
            position++;
            var end = path.IndexOf(']', position);
            if (end < 0)
            {
                throw new ArgumentException("An array index is unterminated.", nameof(path));
            }

            foreach (var coordinate in path.Substring(position, end - position).Split(','))
            {
                if (!int.TryParse(coordinate.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var index))
                {
                    throw new ArgumentException("An array coordinate is invalid.", nameof(path));
                }

                indices.Add(index);
            }

            position = end + 1;
        }

        return indices.ToArray();
    }
}
