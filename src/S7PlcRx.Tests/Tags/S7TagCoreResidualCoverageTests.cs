// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.Driver.S7PlcRx.Enums;
using PlcTag = global::IoT.Driver.S7PlcRx.Tag;
using TagCollection = global::IoT.Driver.S7PlcRx.Tags;

namespace IoT.Driver.S7PlcRx.Tests.Tags;

/// <summary>Exercises remaining public tag model behavior without a PLC connection.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public sealed class S7TagCoreResidualCoverageTests
{
    /// <summary>Gets the deterministic local endpoint used by registrations.</summary>
    private const string LocalEndpoint = "127.0.0.1";

    /// <summary>Gets the address used by the residual tag.</summary>
    private const string TagAddress = "DB1.DBB0";

    /// <summary>Gets the name used by the residual tag.</summary>
    private const string TagName = "residual";

    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay => GetType().Name;

    /// <summary>Ensures constructor metadata and polling state remain independently mutable.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Tag_ConstructorAndPolling_KeepMetadata()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var tag = new PlcTag(TagName, TagAddress, typeof(byte));

        tag.SetDoNotPoll(true);

        await Assert.That(tag.Name, Is.EqualTo(TagName));
        await Assert.That(tag.Address, Is.EqualTo(TagAddress));
        await Assert.That(tag.Type, Is.EqualTo(typeof(byte)));
        await Assert.That(tag.Value, Is.Not.Null);
        await Assert.That(tag.DoNotPoll, Is.True);
    }

    /// <summary>Ensures keyed and tag-based collection operations retain the stored instance.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Tags_KeyedAndTagBasedAdds_RetainTag()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var tag = new PlcTag(TagName, TagAddress, typeof(byte));
        var tags = new TagCollection { { (object)TagName, (object)tag } };

        await Assert.That(tags[TagName], Is.SameAs(tag));
        await Assert.That(tags[tag], Is.SameAs(tag));
        await Assert.That(tags.Get(tag), Is.SameAs(tag));
    }

    /// <summary>Ensures registration supports polling configuration before a connection is established.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task TagOperations_Registration_ConfiguresPollingAndCanBeRemoved()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        using var plc = new RxS7(new(new(CpuType.S71500, LocalEndpoint, 0, 1)));
        var registration = TagOperations.AddUpdateTagItem(plc, typeof(byte), TagName, TagAddress);

        await Assert.That(registration.Tag, Is.Not.Null);
        await Assert.That(registration.Plc, Is.SameAs(plc));
        await Assert.That(registration.SetPolling(false), Is.SameAs(registration));
        await Assert.That(TagOperations.GetTag(plc, TagName).Tag, Is.SameAs(registration.Tag));

        TagOperations.RemoveTagItem(plc, TagName);
        await Assert.That(TagOperations.GetTag(plc, TagName).Tag, Is.NullValue);
    }

    /// <summary>Ensures address errors retain their parameter, value and causal exception details.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task TagAddressOutOfRangeException_PublicOverloads_RetainDetails()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var tag = new PlcTag(TagName, TagAddress, typeof(byte));
        var cause = new InvalidOperationException(TagName);
        var withTag = new TagAddressOutOfRangeException(tag, cause);
        var withValue = new TagAddressOutOfRangeException("address", TagAddress, TagName);

        await Assert.That(withTag.ParamName, Is.EqualTo("Address"));
        await Assert.That(withTag.InnerException, Is.SameAs(cause));
        await Assert.That(withValue.ParamName, Is.EqualTo("address"));
        await Assert.That(withValue.ActualValue, Is.EqualTo(TagAddress));
    }
}
