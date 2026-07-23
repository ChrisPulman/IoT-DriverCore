// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Enums;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Enums;
#endif

/// <summary>Represents the f un ct io nc od e enumeration.</summary>
internal enum FunctionCode
{
    /// <summary>Represents the m em or ya re a enum value.</summary>
    MemoryArea = 0x01,
    /// <summary>Represents the p ar am et er ar ea enum value.</summary>
    ParameterArea = 0x02,
    /// <summary>Represents the p ro gr am ar ea enum value.</summary>
    ProgramArea = 0x03,
    /// <summary>Represents the o pe ra ti ng mo de enum value.</summary>
    OperatingMode = 0x04,
    /// <summary>Represents the m ac hi ne co nf ig ur at io n enum value.</summary>
    MachineConfiguration = 0x05,
    /// <summary>Represents the s ta tu s enum value.</summary>
    Status = 0x06,
    /// <summary>Represents the t im ed at a enum value.</summary>
    TimeData = 0x07,
    /// <summary>Represents the m es sa ge di sp la y enum value.</summary>
    MessageDisplay = 0x09,
    /// <summary>Represents the a cc es sr ig ht s enum value.</summary>
    AccessRights = 0x0C,
    /// <summary>Represents the e rr or lo g enum value.</summary>
    ErrorLog = 0x21,
    /// <summary>Represents the FINS write log function group.</summary>
    FINSWriteLog = ErrorLog,
    /// <summary>Represents the file memory function group.</summary>
    FileMemory = 0x22,
    /// <summary>Represents the d eb ug gi ng enum value.</summary>
    Debugging = 0x23,
    /// <summary>Represents the s er ia lg at ew ay enum value.</summary>
    SerialGateway = 0x28,
}
