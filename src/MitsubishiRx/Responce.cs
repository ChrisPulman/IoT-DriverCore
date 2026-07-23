// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM

namespace IoT.DriverCore.MitsubishiRx.Reactive;
#else

namespace IoT.DriverCore.MitsubishiRx;
#endif

/// <summary>Provides the Responce type.</summary>
public class Responce
{
    /// <summary>Stores the time provider used for timestamping.</summary>
    private readonly TimeProvider _timeProvider;

    /// <summary>Initializes a new instance of the <see cref="Responce"/> class using <see cref="TimeProvider.System"/>.</summary>
    public Responce()
        : this(TimeProvider.System)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Responce"/> class.</summary>
    /// <param name="timeProvider">The time provider used to stamp <see cref="InitialTime"/>.</param>
    public Responce(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        InitialTime = _timeProvider.GetUtcNow();
    }

    /// <summary>Gets or sets the IsSucceed property.</summary>
    public bool IsSucceed { get; set; } = true;

    /// <summary>Gets or sets the Err property.</summary>
    public string Err
    {
        get => field ?? string.Empty;
        set
        {
            field = value;
            AddErr2List();
        }
    }

    /// <summary>Gets or sets the ErrCode property.</summary>
    public int ErrCode { get; set; }

    /// <summary>Gets or sets the Exception property.</summary>
    public Exception? Exception { get; set; }

    /// <summary>Gets or sets the ErrList property.</summary>
    public List<string> ErrList { get; } = new();

    /// <summary>Gets or sets the Request property.</summary>
    public string? Request { get; set; }

    /// <summary>Gets or sets the Response property.</summary>
    public string? Response { get; set; }

    /// <summary>Gets or sets the Request2 property.</summary>
    public string? Request2 { get; set; }

    /// <summary>Gets or sets the Response2 property.</summary>
    public string? Response2 { get; set; }

    /// <summary>Gets the TimeConsuming property.</summary>
    public double? TimeConsuming { get; private set; }

    /// <summary>Gets the InitialTime property.</summary>
    public DateTimeOffset InitialTime { get; protected set; }

    /// <summary>Executes the SetErrInfo operation.</summary>
    /// <param name="result">The result parameter.</param>
    /// <returns>The SetErrInfo operation result.</returns>
    public Responce SetErrInfo(Responce result)
    {
        if (result is null)
        {
            return this;
        }

        IsSucceed = result.IsSucceed;
        Err = result.Err;
        ErrCode = result.ErrCode;
        Exception = result.Exception;
        foreach (var err in result.ErrList)
        {
            if (!ErrList.Contains(err))
            {
                ErrList.Add(err);
            }
        }

        return this;
    }

    /// <summary>Executes the AddErr2List operation.</summary>
    public void AddErr2List()
    {
        if (ErrList.Contains(Err))
        {
            return;
        }

        ErrList.Add(Err);
    }

    /// <summary>Executes the EndTime operation.</summary>
    /// <returns>The EndTime operation result.</returns>
    internal Responce EndTime()
    {
        TimeConsuming = (_timeProvider.GetUtcNow() - InitialTime).TotalMilliseconds;
        return this;
    }
}
