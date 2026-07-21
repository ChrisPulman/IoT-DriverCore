// Copyright (c) 2019-2026 Chris Pulman and contributors. All rights reserved.
// Chris Pulman and contributors licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if REACTIVE_SHIM
namespace OmronPlcRx.Reactive.Core.Enums;
#else
namespace OmronPlcRx.Core.Enums;
#endif

/// <summary>Represents file memory function codes.</summary>
internal enum FileMemoryFunctionCode
{
    /// <summary>Represents the r ea df il en am e enum value.</summary>
    ReadFileName = 0x01,
    /// <summary>Represents the r ea ds in gl ef il e enum value.</summary>
    ReadSingleFile = 0x02,
    /// <summary>Represents the w ri te si ng le fi le enum value.</summary>
    WriteSingleFile = 0x03,
    /// <summary>Represents the f or ma tm em or y enum value.</summary>
    FormatMemory = 0x04,
    /// <summary>Represents the d el et ef il e enum value.</summary>
    DeleteFile = 0x05,
    /// <summary>Represents the c op yf il e enum value.</summary>
    CopyFile = 0x07,
    /// <summary>Represents the c ha ng ef il en am e enum value.</summary>
    ChangeFileName = 0x08,
    /// <summary>Represents the m em or ya re at ra ns fe r enum value.</summary>
    MemoryAreaTransfer = 0x0A,
    /// <summary>Represents the p ar am et er ar ea tr an sf er enum value.</summary>
    ParameterAreaTransfer = 0x0B,
    /// <summary>Represents the p ro gr am ar ea tr an sf er enum value.</summary>
    ProgramAreaTransfer = 0x0C,
    /// <summary>Represents the c re at eo rd el et ed ir ec to ry enum value.</summary>
    CreateOrDeleteDirectory = 0x15,
}
