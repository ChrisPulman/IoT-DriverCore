// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace S7PlcRx.Reactive;
#else
namespace S7PlcRx;
#endif

/// <summary>Provides compositional operations for managing S7 tags.</summary>
public static class TagOperations
{
    /// <summary>Adds or updates a scalar tag.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="type">The tag value type.</param>
    /// <param name="tagName">The tag name.</param>
    /// <param name="address">The PLC address.</param>
    /// <returns>The registered tag and PLC.</returns>
    public static TagRegistration AddUpdateTagItem(
        IRxS7 plc,
        Type type,
        string tagName,
        string address) =>
        AddUpdateTagItemCore(plc, type, tagName, address, null);

    /// <summary>Adds or updates an array or fixed-length string tag.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="type">The tag value type.</param>
    /// <param name="tagName">The tag name.</param>
    /// <param name="address">The PLC address.</param>
    /// <param name="arrayLength">The fixed array or string length.</param>
    /// <returns>The registered tag and PLC.</returns>
    public static TagRegistration AddUpdateTagItem(
        IRxS7 plc,
        Type type,
        string tagName,
        string address,
        int arrayLength) =>
        AddUpdateTagItem(plc, type, tagName, address, (int?)arrayLength);

    /// <summary>Adds or updates a tag with a run-time fixed length.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="type">The tag value type.</param>
    /// <param name="tagName">The tag name.</param>
    /// <param name="address">The PLC address.</param>
    /// <param name="arrayLength">The fixed length, or <see langword="null"/> for a scalar tag.</param>
    /// <returns>The registered tag and PLC.</returns>
    public static TagRegistration AddUpdateTagItem(
        IRxS7 plc,
        Type type,
        string tagName,
        string address,
        int? arrayLength) =>
        AddUpdateTagItemCore(plc, type, tagName, address, arrayLength);

    /// <summary>Gets a tag by name.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="tagName">The tag name.</param>
    /// <returns>The tag registration.</returns>
    public static TagRegistration GetTag(IRxS7 plc, string tagName)
    {
        ValidatePlcAndName(plc, tagName);
        return plc.TagList[tagName] is Tag tag
            ? new TagRegistration(tag, plc)
            : new TagRegistration(null, plc);
    }

    /// <summary>Removes a named tag.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="tagName">The tag name.</param>
    public static void RemoveTagItem(IRxS7 plc, string tagName)
    {
        ValidatePlcAndName(plc, tagName);
        if (plc is not RxS7 concretePlc)
        {
            return;
        }

        concretePlc.RemoveTagItemInternal(tagName);
    }

    /// <summary>Projects non-null observable values with a tag name.</summary>
    /// <typeparam name="TValue">The observed value type.</typeparam>
    /// <param name="source">The observable source.</param>
    /// <param name="tag">The associated tag name.</param>
    /// <returns>The tagged values.</returns>
    public static IObservable<(string Tag, TValue Value)> ToTagValue<TValue>(
        IObservable<TValue?> source,
        string tag)
    {
        Guard.NotNull(source, nameof(source));
        return source.SelectMany(value => CreateTaggedSequence(tag, value));
    }

    /// <summary>Projects tag values into a dictionary snapshot stream.</summary>
    /// <param name="source">The tag observable source.</param>
    /// <returns>The latest non-null value for each tag name.</returns>
    public static IObservable<IDictionary<string, object>> TagToDictionary(IObservable<Tag?> source)
    {
        Guard.NotNull(source, nameof(source));
        var values = new Dictionary<string, object>();
        return source.SelectMany(CreateTagValueSequence)
            .Select(item => UpdateSnapshot(values, item.Name, item.Value));
    }

    /// <summary>Adds or updates a tag using an optional fixed length.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="type">The tag value type.</param>
    /// <param name="tagName">The tag name.</param>
    /// <param name="address">The PLC address.</param>
    /// <param name="arrayLength">The optional fixed length.</param>
    /// <returns>The tag registration.</returns>
    private static TagRegistration AddUpdateTagItemCore(
        IRxS7 plc,
        Type type,
        string tagName,
        string address,
        int? arrayLength)
    {
        ValidatePlcAndName(plc, tagName);
        Guard.NotNull(type, nameof(type));
        Guard.NotNullOrWhiteSpace(address, nameof(address));

        var tag = CreateTag(type, tagName, address, arrayLength);
        if (plc is not RxS7 concretePlc)
        {
            return new TagRegistration(tag, plc);
        }

        concretePlc.AddUpdateTagItemInternal(tag);
        return new TagRegistration(tag, plc);
    }

    /// <summary>Creates a tag using the optional fixed length when required.</summary>
    /// <param name="type">The tag value type.</param>
    /// <param name="tagName">The tag name.</param>
    /// <param name="address">The PLC address.</param>
    /// <param name="arrayLength">The optional fixed length.</param>
    /// <returns>The created tag.</returns>
    private static Tag CreateTag(Type type, string tagName, string address, int? arrayLength) =>
        (type == typeof(string) || type.IsArray) && arrayLength.HasValue
            ? new Tag(tagName, address, type, arrayLength.Value)
            : new Tag(tagName, address, type);

    /// <summary>Creates a zero-or-one item tagged-value sequence.</summary>
    /// <typeparam name="TValue">The value type.</typeparam>
    /// <param name="tag">The tag name.</param>
    /// <param name="value">The optional value.</param>
    /// <returns>The tagged-value sequence.</returns>
    private static IObservable<(string Tag, TValue Value)> CreateTaggedSequence<TValue>(
        string tag,
        TValue? value) => value switch
        {
            TValue typedValue => Observable.Return((Tag: tag, Value: typedValue)),
            _ => Observable.Empty<(string Tag, TValue Value)>(),
        };

    /// <summary>Creates a zero-or-one item tag-value sequence.</summary>
    /// <param name="tag">The optional tag.</param>
    /// <returns>The tag-name and value sequence.</returns>
    private static IObservable<(string Name, object Value)> CreateTagValueSequence(Tag? tag) => tag switch
    {
        { Name: string name, Value: { } value } => Observable.Return((Name: name, Value: value)),
        _ => Observable.Empty<(string Name, object Value)>(),
    };

    /// <summary>Updates and returns a tag-value dictionary.</summary>
    /// <param name="values">The dictionary to update.</param>
    /// <param name="name">The tag name.</param>
    /// <param name="value">The tag value.</param>
    /// <returns>The updated dictionary.</returns>
    private static Dictionary<string, object> UpdateSnapshot(
        Dictionary<string, object> values,
        string name,
        object value)
    {
        values[name] = value;
        return values;
    }

    /// <summary>Validates a PLC and tag name.</summary>
    /// <param name="plc">The PLC instance.</param>
    /// <param name="tagName">The tag name.</param>
    private static void ValidatePlcAndName(IRxS7 plc, string tagName)
    {
        Guard.NotNull(plc, nameof(plc));
        Guard.NotNullOrWhiteSpace(tagName, nameof(tagName));
    }
}
