// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Xml.Serialization;

#if REACTIVE_SHIM
namespace IoT.DriverCore.TwinCATRx.Core.Reactive;
#else
namespace IoT.DriverCore.TwinCATRx.Core;
#endif

/// <summary>Base settings for Engine Settings file.</summary>
[Serializable]
[XmlInclude(typeof(WriteVariable))]
[XmlInclude(typeof(Notification))]
public class Settings : ISettings
{
    /// <summary>Gets or sets the Ads Address.</summary>
    public string AdsAddress { get; set; } = string.Empty;

    /// <summary>Gets or sets the Port of the PLC to connect to.</summary>
    public int Port { get; set; } = 801;

    /// <summary>Gets or sets Notifications of this Engine.</summary>
    public List<INotification> Notifications { get; set; } = [];

    IList<INotification> ISettings.Notifications => Notifications;

    /// <summary>Gets or sets System Identifier.</summary>
    public string? SettingsId { get; set; }

    /// <summary>Gets or sets Write variables to this Engine.</summary>
    public List<IWriteVariable> WriteVariables { get; set; } = [];

    IList<IWriteVariable> ISettings.WriteVariables => WriteVariables;

    /// <summary>Creates default settings when no persisted file exists.</summary>
    /// <typeparam name="T">The settings type to use.</typeparam>
    /// <param name="defaultSettings">The settings instance that establishes the requested type.</param>
    /// <returns>The default settings.</returns>
    public virtual T Defaults<T>(T defaultSettings)
        where T : ISettings, new()
    {
        if (defaultSettings is null)
        {
            throw new ArgumentNullException(nameof(defaultSettings));
        }

        var s = new T
        {
            SettingsId = "Defaults",
        };
        TwinCatRxExtensions.AddNotification(s, ".UIStructure");
        TwinCatRxExtensions.AddWriteVariable(s, ".FailSafeCounter");
        return s;
    }
}
