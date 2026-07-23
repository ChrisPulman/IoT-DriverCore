// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace IoT.DriverCore.OmronPlcRx.Reactive.Core.Enums;
#else
namespace IoT.DriverCore.OmronPlcRx.Core.Enums;
#endif

/// <summary>Represents the s er ia lg at ew ay fu nc ti on co de enumeration.</summary>
internal enum SerialGatewayFunctionCode
{
    /// <summary>Represents the c on ve rt to co mp ow ay fc om ma nd enum value.</summary>
    ConvertToCompoWayFCommand = 0x03,
    /// <summary>Represents the c on ve rt to mo db us rt uc om ma nd enum value.</summary>
    ConvertToModbusRTUCommand = 0x04,
    /// <summary>Represents the c on ve rt to mo db us as ci ic om ma nd enum value.</summary>
    ConvertToModbusASCIICommand = 0x05,
}
