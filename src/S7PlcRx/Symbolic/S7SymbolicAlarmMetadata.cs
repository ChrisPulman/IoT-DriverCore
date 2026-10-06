// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Xml;
using Org.BouncyCastle.Utilities.Zlib;

#if REACTIVE_SHIM
namespace IoT.Driver.S7PlcRx.Reactive.Symbolic;
#else
namespace IoT.Driver.S7PlcRx.Symbolic;
#endif

/// <summary>Typed PLC metadata including alarm texts, associated values and comment attributes.</summary>
public sealed class S7SymbolicAlarmMetadata
{
    /// <summary>The CpuAlarmIdAttribute protocol constant.</summary>
    private const int CpuAlarmIdAttribute = 2_670;

    /// <summary>The AlarmStateAttribute protocol constant.</summary>
    private const int AlarmStateAttribute = 2_671;

    /// <summary>The AlarmDomainAttribute protocol constant.</summary>
    private const int AlarmDomainAttribute = 2_672;

    /// <summary>The ComingAttribute protocol constant.</summary>
    private const int ComingAttribute = 2_673;

    /// <summary>The GoingAttribute protocol constant.</summary>
    private const int GoingAttribute = 2_677;

    /// <summary>The AssociatedValuesAttribute protocol constant.</summary>
    private const int AssociatedValuesAttribute = 3_476;

    /// <summary>The AlarmTexts protocol constant.</summary>
    private const int AlarmTexts = 2_715;

    /// <summary>The LanguageWordBits protocol constant.</summary>
    private const int LanguageWordBits = 16;

    /// <summary>The ObjectComment protocol constant.</summary>
    private const int ObjectComment = 4_288;

    /// <summary>The LineComments protocol constant.</summary>
    private const int LineComments = 2_546;

    /// <summary>The MaximumSubscriptionItems protocol constant.</summary>
    private const int MaximumCommentCharacters = 16_777_216;

    /// <summary>The requested comment attributes.</summary>
    private static readonly uint[] _commentAttributes = [ObjectComment, LineComments];

    /// <summary>Initializes a new instance of the <see cref="S7SymbolicAlarmMetadata"/> class.</summary>
    /// <param name="objectId">The object id.</param>
    /// <param name="classId">The class id.</param>
    /// <param name="attributes">The attributes.</param>
    /// <param name="languageId">The language id.</param>
    internal S7SymbolicAlarmMetadata(uint objectId, uint classId, IDictionary<uint, S7SymbolicValue> attributes, uint languageId = 0)
    {
        ObjectId = objectId;
        ClassId = classId;
        LanguageId = languageId;
        Attributes = new ReadOnlyDictionary<uint, S7SymbolicValue>(new Dictionary<uint, S7SymbolicValue>(attributes));
    }

    /// <summary>Gets the PLC object identifier.</summary>
    /// <value>The stored value.</value>
    public uint ObjectId { get; }

    /// <summary>Gets the PLC class identifier.</summary>
    /// <value>The stored value.</value>
    public uint ClassId { get; }

    /// <summary>Gets the requested localization LCID; zero requests all languages.</summary>
    /// <value>The stored value.</value>
    public uint LanguageId { get; }

    /// <summary>Gets text for the requested LCID, indexed by PLC text number.</summary>
    /// <value>The stored value.</value>
    public IReadOnlyDictionary<uint, string> Texts => GetTexts(LanguageId);

    /// <summary>Gets the alarm identifier when included in the PLC object.</summary>
    /// <value>The stored value.</value>
    public ulong? CpuAlarmId => Attributes.TryGetValue(CpuAlarmIdAttribute, out var value) && value.Value is ulong id ? id : null;

    /// <summary>Gets the current alarm state flags when available.</summary>
    /// <value>The stored value.</value>
    public byte? State => Attributes.TryGetValue(AlarmStateAttribute, out var value) && value.Value is byte state ? state : null;

    /// <summary>Gets the alarm domain when available.</summary>
    /// <value>The stored value.</value>
    public ushort? Domain => Attributes.TryGetValue(AlarmDomainAttribute, out var value) && value.Value is ushort domain ? domain : null;

    /// <summary>Gets whether this object reports a coming alarm.</summary>
    /// <value>The stored value.</value>
    public bool IsComing => Attributes.ContainsKey(ComingAttribute);

    /// <summary>Gets whether this object reports a going alarm.</summary>
    /// <value>The stored value.</value>
    public bool IsGoing => Attributes.ContainsKey(GoingAttribute);

    /// <summary>Gets the coming or going event structure, including timestamp and associated values.</summary>
    /// <value>The stored value.</value>
    public S7SymbolicStruct? Event
    {
        get
        {
            if (Attributes.TryGetValue(ComingAttribute, out var coming))
            {
                return coming.Value as S7SymbolicStruct;
            }

            return Attributes.TryGetValue(GoingAttribute, out var going) ? going.Value as S7SymbolicStruct : null;
        }
    }

    /// <summary>Gets associated values in their PLC wire representation.</summary>
    /// <value>The stored value.</value>
    public S7SymbolicValue? AssociatedValues => Event is { } value && value.Fields.TryGetValue(AssociatedValuesAttribute, out var associated) ? associated : null;

    /// <summary>Gets typed attributes, preserving localized text and associated value structures.</summary>
    /// <value>The stored value.</value>
    public IReadOnlyDictionary<uint, S7SymbolicValue> Attributes { get; }

    /// <summary>Gets localized text by text number; zero LCID preserves packed language/text identifiers for all languages.</summary>
    /// <param name="languageId">The language id.</param>
    /// <returns>The operation result.</returns>
    public IReadOnlyDictionary<uint, string> GetTexts(uint languageId)
    {
        var result = new Dictionary<uint, string>();
        if (Attributes.TryGetValue(AlarmTexts, out var texts) && texts.Value is Dictionary<uint, object?> entries)
        {
            foreach (var entry in entries)
            {
                if ((languageId == 0 || (entry.Key >> LanguageWordBits) == languageId) && entry.Value is S7SymbolicBlob blob)
                {
                    var key = languageId == 0 ? entry.Key : entry.Key & 0xffff;
                    result.Add(key, new UTF8Encoding(false, true).GetString(blob.Data));
                }
            }
        }

        return new ReadOnlyDictionary<uint, string>(result);
    }

    /// <summary>Decodes comment documents that do not require preset dictionaries.</summary>
    /// <returns>XML documents indexed by attribute and localization key.</returns>
    public IReadOnlyDictionary<string, string> GetCommentXml() => GetCommentXml(null);

    /// <summary>Decodes and validates localized XML comments with bounded decompression.</summary>
    /// <param name="dictionaryResolver">Resolves caller-supplied preset dictionary bytes by the required Adler32 identifier.</param>
    /// <returns>XML documents indexed by attribute and localization key.</returns>
    public IReadOnlyDictionary<string, string> GetCommentXml(Func<uint, byte[]?>? dictionaryResolver)
    {
        var result = new Dictionary<string, string>();
        foreach (var attributeId in _commentAttributes
        )
        {
            if (!Attributes.TryGetValue(attributeId, out var attribute) || attribute.Value is not Dictionary<uint, object?> entries)
            {
                continue;
            }

            foreach (var entry in entries)
            {
                var xml = DecodeCommentEntry(entry.Value, dictionaryResolver);
                if (xml is null)
                {
                    continue;
                }

                using var input = new StringReader(xml);
                using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumCommentCharacters });
                var document = new XmlDocument
                {
                    XmlResolver = null
                };
                document.Load(reader);
                result.Add($"{attributeId}:{entry.Key}", xml);
            }
        }

        return new ReadOnlyDictionary<string, string>(result);
    }

    /// <summary>Decodes one localized comment value.</summary>
    /// <param name="value">The localized value.</param>
    /// <param name="dictionaryResolver">The caller dictionary resolver.</param>
    /// <returns>XML when the value represents a comment document.</returns>
    private static string? DecodeCommentEntry(object? value, Func<uint, byte[]?>? dictionaryResolver) => value switch
    {
        string text => text,
        S7SymbolicBlob blob => DecodeComment(blob.Data, dictionaryResolver),
        _ => null,
    };

    /// <summary>Decodes a prefixed zlib comment blob using caller-supplied dictionaries.</summary>
    /// <param name="data">The original PLC blob.</param>
    /// <param name="dictionaryResolver">The optional resolver for preset dictionaries.</param>
    /// <returns>The original XML text.</returns>
    private static string DecodeComment(byte[] data, Func<uint, byte[]?>? dictionaryResolver)
    {
        const int prefixLength = 4;
        const int maximumLength = 16_777_216;
        const int chunkLength = 4096;
        if (data.Length <= prefixLength)
        {
            throw new InvalidDataException("Truncated compressed comment blob.");
        }

        var inflater = new ZStream();
        if (inflater.inflateInit() != JZlib.Z_OK)
        {
            throw new InvalidDataException("Comment inflater initialization failed.");
        }

        try
        {
            inflater.next_in = data;
            inflater.next_in_index = prefixLength;
            inflater.avail_in = data.Length - prefixLength;
            using var output = new MemoryStream();
            var buffer = new byte[chunkLength];
            var status = JZlib.Z_OK;
            while (status != JZlib.Z_STREAM_END)
            {
                inflater.next_out = buffer;
                inflater.next_out_index = 0;
                inflater.avail_out = buffer.Length;
                var previousInput = inflater.total_in;
                status = inflater.inflate(JZlib.Z_NO_FLUSH);
                if (status == JZlib.Z_NEED_DICT)
                {
                    SupplyDictionary(inflater, dictionaryResolver);
                    status = JZlib.Z_OK;
                    continue;
                }

                var count = buffer.Length - inflater.avail_out;
                if (output.Length + count > maximumLength)
                {
                    throw new InvalidDataException("Decoded comments exceed the size limit.");
                }

                output.Write(buffer, 0, count);
                ValidateProgress(inflater, status, count, previousInput);
            }

            if (inflater.avail_in != 0)
            {
                throw new InvalidDataException("Unexpected compressed comment trailer.");
            }

            return new UTF8Encoding(false, true).GetString(output.ToArray());
        }
        finally
        {
            _ = inflater.inflateEnd();
        }
    }

    /// <summary>Supplies and verifies the dictionary requested by the zlib stream.</summary>
    /// <param name="inflater">The inflater requesting a preset dictionary.</param>
    /// <param name="dictionaryResolver">The caller resolver keyed by Adler32.</param>
    private static void SupplyDictionary(ZStream inflater, Func<uint, byte[]?>? dictionaryResolver)
    {
        var dictionaryId = unchecked((uint)inflater.adler);
        var dictionary = dictionaryResolver?.Invoke(dictionaryId)
            ?? throw new InvalidDataException($"PLC comment dictionary 0x{dictionaryId:X8} is required; supply dictionaryResolver. Raw bytes remain in Attributes.");
        if (CalculateAdler(dictionary) == dictionaryId && inflater.inflateSetDictionary(dictionary, dictionary.Length) == JZlib.Z_OK)
        {
            return;
        }

        throw new InvalidDataException($"The supplied comment dictionary does not match Adler32 0x{dictionaryId:X8}.");
    }

    /// <summary>Rejects malformed or stalled compressed input.</summary>
    /// <param name="inflater">The inflater state.</param>
    /// <param name="status">The returned inflater status.</param>
    /// <param name="count">The produced byte count.</param>
    /// <param name="previousInput">The consumed input before inflating.</param>
    private static void ValidateProgress(ZStream inflater, int status, int count, long previousInput)
    {
        var valid = status switch
        {
            JZlib.Z_STREAM_END => true,
            JZlib.Z_OK => count > 0 || inflater.total_in != previousInput,
            _ => false,
        };

        if (valid)
        {
            return;
        }

        throw new InvalidDataException($"Invalid or truncated compressed comment blob: {inflater.msg}");
    }

    /// <summary>Checks the supplied dictionary against its RFC 1950 Adler32 identifier.</summary>
    /// <param name="data">The caller dictionary bytes.</param>
    /// <returns>The Adler32 dictionary identifier.</returns>
    private static uint CalculateAdler(byte[] data)
    {
        const uint modulus = 65_521;
        const int wordBits = 16;
        uint first = 1;
        uint second = 0;
        foreach (var octet in data)
        {
            first = (first + octet) % modulus;
            second = (second + first) % modulus;
        }

        return (second << wordBits) | first;
    }
}
