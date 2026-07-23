// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using IoT.DriverCore.S7PlcRx.Enums;
using PlcTag = global::IoT.DriverCore.S7PlcRx.Tag;
using TagCollection = global::IoT.DriverCore.S7PlcRx.Tags;

namespace IoT.DriverCore.S7PlcRx.Tests.Tags;

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
    [Test]
    public void Tag_ConstructorAndPolling_KeepMetadata()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var tag = new PlcTag(TagName, TagAddress, typeof(byte));

        tag.SetDoNotPoll(true);

        Assert.That(tag.Name, Is.EqualTo(TagName));
        Assert.That(tag.Address, Is.EqualTo(TagAddress));
        Assert.That(tag.Type, Is.EqualTo(typeof(byte)));
        Assert.That(tag.Value, Is.Not.Null);
        Assert.That(tag.DoNotPoll, Is.True);
    }

    /// <summary>Ensures keyed and tag-based collection operations retain the stored instance.</summary>
    [Test]
    public void Tags_KeyedAndTagBasedAdds_RetainTag()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var tag = new PlcTag(TagName, TagAddress, typeof(byte));
        var tags = new TagCollection { { (object)TagName, (object)tag } };

        Assert.That(tags[TagName], Is.SameAs(tag));
        Assert.That(tags[tag], Is.SameAs(tag));
        Assert.That(tags.Get(tag), Is.SameAs(tag));
    }

    /// <summary>Ensures registration supports polling configuration before a connection is established.</summary>
    [Test]
    public void TagOperations_Registration_ConfiguresPollingAndCanBeRemoved()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        using var plc = new RxS7(new(new(CpuType.S71500, LocalEndpoint, 0, 1)));
        var registration = TagOperations.AddUpdateTagItem(plc, typeof(byte), TagName, TagAddress);

        Assert.That(registration.Tag, Is.Not.Null);
        Assert.That(registration.Plc, Is.SameAs(plc));
        Assert.That(registration.SetPolling(false), Is.SameAs(registration));
        Assert.That(TagOperations.GetTag(plc, TagName).Tag, Is.SameAs(registration.Tag));

        TagOperations.RemoveTagItem(plc, TagName);
        Assert.That(TagOperations.GetTag(plc, TagName).Tag, Is.NullValue);
    }

    /// <summary>Ensures address errors retain their parameter, value and causal exception details.</summary>
    [Test]
    public void TagAddressOutOfRangeException_PublicOverloads_RetainDetails()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var tag = new PlcTag(TagName, TagAddress, typeof(byte));
        var cause = new InvalidOperationException(TagName);
        var withTag = new TagAddressOutOfRangeException(tag, cause);
        var withValue = new TagAddressOutOfRangeException("address", TagAddress, TagName);

        Assert.That(withTag.ParamName, Is.EqualTo("Address"));
        Assert.That(withTag.InnerException, Is.SameAs(cause));
        Assert.That(withValue.ParamName, Is.EqualTo("address"));
        Assert.That(withValue.ActualValue, Is.EqualTo(TagAddress));
    }
}
