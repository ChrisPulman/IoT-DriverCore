// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PlcTag = global::IoT.Driver.S7PlcRx.Tag;
using TagCollection = global::IoT.Driver.S7PlcRx.Tags;

namespace IoT.Driver.S7PlcRx.Tests.Tags;

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
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task AddRange_WhenNull_ShouldThrow()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var tags = new TagCollection();
        _ = await Assert.Throws<ArgumentNullException>(() => tags.AddRange(null!));
    }

    /// <summary>Ensures `AddRange` skips tags with null values.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task AddRange_WhenTagValueNull_ShouldSkip()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var tags = new TagCollection();
        tags.AddRange([
            new PlcTag("T0", FirstTagAddress, typeof(bool)),
            new PlcTag("T1", "DB1.DBX0.1", typeof(bool)) { Value = null },
        ]);

        await Assert.That(tags["T0"], Is.Not.Null);
        await Assert.That(tags["T1"], Is.NullValue);
    }

    /// <summary>Ensures indexer by `Tag` resolves by name.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task Indexer_ByTag_ShouldReturnByName()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var tag = new PlcTag("T0", FirstTagAddress, typeof(bool));
        var tags = new TagCollection { tag };

        await Assert.That(tags[tag], Is.SameAs(tag));
    }

    /// <summary>Ensures `GetTags` returns only tags with non-null values.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task GetTags_ShouldReturnOnlyNonNullValues()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var tagWithValue = new PlcTag("T0", FirstTagAddress, typeof(bool));
        var tagWithoutValue = new PlcTag("T1", "DB1.DBX0.1", typeof(bool)) { Value = null };
        var tags = new TagCollection { tagWithValue };
        tags.Add(tagWithoutValue);

        var filtered = tags.GetTags();
        await Assert.That(filtered["T0"], Is.Not.Null);
        await Assert.That(filtered["T1"], Is.NullValue);
    }

    /// <summary>Ensures `ToList` returns empty when collection is empty.</summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous unit test.</returns>
    [Test]
    public async Task ToList_WhenEmpty_ShouldReturnEmpty()
    {
        await Assert.That(DebuggerDisplay, Is.Not.Null);
        var tags = new TagCollection();
        await Assert.That(tags.ToList(), Is.EmptyValue);
    }
}
