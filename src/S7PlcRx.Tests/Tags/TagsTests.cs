// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PlcTag = global::IoT.DriverCore.S7PlcRx.Tag;
using TagCollection = global::IoT.DriverCore.S7PlcRx.Tags;

namespace IoT.DriverCore.S7PlcRx.Tests.Tags;

/// <summary>Tests for `Tags` collection helpers.</summary>
[System.Diagnostics.DebuggerDisplay("{DebuggerDisplay,nq}")]
public class TagsTests
{
    /// <summary>Gets the address used by the first tag.</summary>
    private const string FirstTagAddress = "DB1.DBX0.0";

    /// <summary>Gets the debugger display text.</summary>
    [System.Diagnostics.DebuggerBrowsable(System.Diagnostics.DebuggerBrowsableState.Never)]
    private string DebuggerDisplay
    {
        get => ToString() ?? string.Empty;
    }

    /// <summary>Ensures `AddRange` validates input.</summary>
    [Test]
    public void AddRange_WhenNull_ShouldThrow()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var tags = new TagCollection();
        _ = Assert.Throws<ArgumentNullException>(() => tags.AddRange(null!));
    }

    /// <summary>Ensures `AddRange` skips tags with null values.</summary>
    [Test]
    public void AddRange_WhenTagValueNull_ShouldSkip()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var tags = new TagCollection();
        tags.AddRange([
            new PlcTag("T0", FirstTagAddress, typeof(bool)),
            new PlcTag("T1", "DB1.DBX0.1", typeof(bool)) { Value = null },
        ]);

        Assert.That(tags["T0"], Is.Not.Null);
        Assert.That(tags["T1"], Is.NullValue);
    }

    /// <summary>Ensures indexer by `Tag` resolves by name.</summary>
    [Test]
    public void Indexer_ByTag_ShouldReturnByName()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var tag = new PlcTag("T0", FirstTagAddress, typeof(bool));
        var tags = new TagCollection { tag };

        Assert.That(tags[tag], Is.SameAs(tag));
    }

    /// <summary>Ensures `GetTags` returns only tags with non-null values.</summary>
    [Test]
    public void GetTags_ShouldReturnOnlyNonNullValues()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var tags = new TagCollection { new PlcTag("T0", FirstTagAddress, typeof(bool)) };
        tags.Add(new PlcTag("T1", "DB1.DBX0.1", typeof(bool)) { Value = null });

        var filtered = tags.GetTags();
        Assert.That(filtered["T0"], Is.Not.Null);
        Assert.That(filtered["T1"], Is.NullValue);
    }

    /// <summary>Ensures `ToList` returns empty when collection is empty.</summary>
    [Test]
    public void ToList_WhenEmpty_ShouldReturnEmpty()
    {
        Assert.That(DebuggerDisplay, Is.Not.Null);
        var tags = new TagCollection();
        Assert.That(tags.ToList(), Is.EmptyValue);
    }
}
