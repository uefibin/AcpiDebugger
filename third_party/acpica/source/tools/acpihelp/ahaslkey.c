/******************************************************************************
 *
 * Module Name: ahaslkey - Table of all known ASL non-operator keywords and
 *                         table of iASL Preprocessor directives
 *
 *****************************************************************************/

/******************************************************************************
 *
 * Copyright (c) 1999 - 2026, Intel Corp.
 * SPDX-License-Identifier: BSD-3-Clause OR GPL-2.0-only
 *
 *****************************************************************************/

#include "acpihelp.h"

/*
 * ASL Keyword types and associated actual keywords.
 * This table was extracted from the ACPI specification.
 */
const AH_ASL_KEYWORD        Gbl_AslKeywordInfo[] =
{
    {"AccessAttribKeyword", "Serial Bus Attributes (with legacy SMBus aliases)",
        ":= AttribQuick (SMBusQuick) | AttribSendReceive (SMBusSendReceive) | "
        "AttribByte (SMBusByte) | AttribWord (SMBusWord) | "
        "AttribBlock (SMBusBlock) | AttribProcessCall (SMBusProcessCall) | "
        "AttribBlockProcessCall (SMBusProcessCall)"},
    {"AccessTypeKeyword", "Field Access Types",
        ":= AnyAcc | ByteAcc | WordAcc | DWordAcc | QWordAcc | BufferAcc"},
    {"AddressingModeKeyword", "Mode - Resource Descriptors",
        ":= AddressingMode7Bit | AddressingMode10Bit"},
    {"AddressKeyword", "ACPI memory range types",
        ":= AddressRangeMemory | AddressRangeReserved | "
        "AddressRangeNVS | AddressRangeACPI"},
    {"AddressSpaceKeyword", "Operation Region Address Space Types",
        ":= RegionSpaceKeyword | FFixedHW"},
    {"BusMasterKeyword", "DMA Bus Mastering",
        ":= BusMaster | NotBusMaster"},
    {"ByteLengthKeyword", "Bits per Byte - Resource Descriptors",
        ":= DataBitsFive | DataBitsSix | DataBitsSeven | DataBitsEight | DataBitsNine"},
    {"ClockPhaseKeyword", "Resource Descriptors",
        ":= ClockPhaseFirst | ClockPhaseSecond"},
    {"ClockPolarityKeyword", "Resource Descriptors",
        ":= ClockPolarityLow | ClockPolarityHigh"},
    {"DecodeKeyword", "Type of Memory Decoding - Resource Descriptors",
        ":= SubDecode | PosDecode"},
    {"DmaTypeKeyword", "DMA Types - DMA Resource Descriptor",
        ":= Compatibility | TypeA | TypeB | TypeF"},
    {"EndianKeyword", "Endian type - Resource Descriptor",
        ":= BigEndian | LittleEndian"},
    {"ExtendedAttribKeyword", "Extended Bus Attributes",
        ":= AttribBytes (AccessLength) | AttribRawBytes (AccessLength) | "
        "AttribRawProcessBytes (AccessLength)"},
    {"FlowControlKeyword", "Resource Descriptor",
        ":= FlowControlNone | FlowControlXon | FlowControlHardware"},
    {"InterruptLevelKeyword", "Interrupt Active Types",
        ":= ActiveHigh | ActiveLow | ActiveBoth"},
    {"InterruptTypeKeyword", "Interrupt Types",
        ":= Edge | Level"},
    {"IoDecodeKeyword", "I/O Decoding - IO Resource Descriptor",
        ":= Decode16 | Decode10"},
    {"IoRestrictionKeyword", "I/O Restriction - GPIO Resource Descriptors",
        ":= IoRestrictionNone | IoRestrictionInputOnly | "
        "IoRestrictionOutputOnly | IoRestrictionNoneAndPreserve"},
    {"LockRuleKeyword", "Global Lock use for Field Operator",
        ":= Lock | NoLock"},
    {"MatchOpKeyword", "Types for Match Operator",
        ":= MTR | MEQ | MLE | MLT | MGE | MGT"},
    {"MaxKeyword", "Max Range Type - Resource Descriptors",
        ":= MaxFixed | MaxNotFixed"},
    {"MemTypeKeyword", "Memory Types - Resource Descriptors",
        ":= Cacheable | WriteCombining | Prefetchable | NonCacheable"},
    {"MinKeyword", "Min Range Type - Resource Descriptors",
        ":= MinFixed | MinNotFixed"},
    {"ObjectTypeKeyword", "ACPI Object Types",
        ":= UnknownObj | IntObj | StrObj | BuffObj | PkgObj | FieldUnitObj | "
        "DeviceObj | EventObj | MethodObj | MutexObj | OpRegionObj | PowerResObj | "
        "ProcessorObj | ThermalZoneObj | BuffFieldObj | DDBHandleObj"},
    {"ParityKeyword", "Resource Descriptors",
        ":= ParityTypeNone | ParityTypeSpace | ParityTypeMark | "
        "ParityTypeOdd | ParityTypeEven"},
    {"PinConfigKeyword", "Pin Configuration - GPIO Resource Descriptors",
        ":= PullDefault | PullUp | PullDown | PullNone"},
    {"PolarityKeyword", "Resource Descriptors",
        ":= PolarityHigh | PolarityLow"},
    {"RangeTypeKeyword", "I/O Range Types - Resource Descriptors",
        ":= ISAOnlyRanges | NonISAOnlyRanges | EntireRange"},
    {"ReadWriteKeyword", "Memory Access Types - Resource Descriptors",
        ":= ReadWrite | ReadOnly"},
    {"RegionSpaceKeyword", "Operation Region Address Space Types",
        ":= UserDefRegionSpace | SystemIO | SystemMemory | PCI_Config | "
        "EmbeddedControl | SMBus | SystemCMOS | PciBarTarget | IPMI | "
        "GeneralPurposeIo, GenericSerialBus"},
    {"ResourceTypeKeyword", "Resource Usage - Resource Descriptors",
        ":= ResourceConsumer | ResourceProducer"},
    {"SerializeRuleKeyword", "Control Method Serialization",
        ":= Serialized | NotSerialized"},
    {"ShareTypeKeyword", "Interrupt Sharing - Resource Descriptors",
        ":= Shared | Exclusive | SharedAndWake | ExclusiveAndWake"},
    {"SlaveModeKeyword", "Resource Descriptors",
        ":= ControllerInitiated | DeviceInitiated"},
    {"StopBitsKeyword", "Resource Descriptors",
        ":= StopBitsZero | StopBitsOne | StopBitsOnePlusHalf | StopBitsTwo"},
    {"TransferWidthKeyword", "DMA Widths - Fixed DMA Resource Descriptor",
        ":= Width8bit | Width16bit | Width32bit | Width64bit | "
        "Width128bit | Width256bit"},
    {"TranslationKeyword", "Translation Density Types - Resource Descriptors",
        ":= SparseTranslation | DenseTranslation"},
    {"TypeKeyword", "Translation Types - Resource Descriptors",
        ":= TypeTranslation | TypeStatic"},
    {"UpdateRuleKeyword", "Field Update Rules",
        ":= Preserve | WriteAsOnes | WriteAsZeros"},
    {"UserDefRegionSpace", "User defined address spaces",
        ":= IntegerData => 0x80 - 0xFF"},
    {"WireModeKeyword", "SPI Wire Mode - Resource Descriptors",
        ":= ThreeWireMode | FourWireMode"},
    {"XferTypeKeyword", "DMA Transfer Types",
        ":= Transfer8 | Transfer16 | Transfer8_16"},
    {NULL, NULL, NULL}
};

/* Preprocessor directives */

const AH_DIRECTIVE_INFO      Gbl_PreprocessorDirectives[] =
{
    {"#include \"Filename\"",               "Standard include of an ASCII ASL source code file"},
    {"#include <Filename>",                 "Alternate syntax for #include, alternate search path"},
    {"#includebuffer \"Filename\" <Name>",  "Include a binary file to create AML Buffer with ASL namepath"},
    {"#includebuffer <Filename> <Name>",    "Alternate syntax for #includebuffer, alternate search path"},

    {"",  ""},
    {"#define <Name>, <Defined name>",      "Simple macro definition (full macros not supported at this time)"},
    {"#define <Expression>, <Defined name>","Simple macro definition (full macros not supported at this time)"},
    {"#undef <Defined name>",               "Delete a previous #define"},

    {"",  ""},
    {"#if <Expression>",                    "Evaluate <Expression> and test return value"},
    {"#ifdef <Defined name>",               "Test existence of the <Defined Name>"},
    {"#ifndef <Defined name>",              "Test non-existence of the <Defined Name>"},
    {"#elif <Expression>",                  "Else-If contraction - evaluate #if <Expression>, test return value"},
    {"#else",                               "Execute alternate case for a previous #if, #ifdef or #ifndef block"},
    {"#endif",                              "Close a previous #if, #ifdef or #ifndef block"},

    {"",   ""},
    {"#line <LineNumber> [Filename]",       "Set the current ASL source code line number, optional filename"},

    {"",   ""},
    {"#error \"String\"",                   "Emit error message and abort compilation"},
    {"#warning \"String\"",                 "Emit an iASL warning at this location in the ASL source"},

    {"",  ""},
    {"#pragma disable (Error number)",      "Disable an iASL error or warning number"},
    {"#pragma message \"String\"",          "Emit an informational message to the output file(s)"},

    {"",  ""},
    {"__FILE__",                            "Return the simple filename of the current ASL file"},
    {"__PATH__",                            "Return the full pathname of the current ASL file"},
    {"__LINE__",                            "Return the current line number within the current ASL file"},
    {"__DATE__",                            "Return the current date"},
    {"__METHOD__",                          "Return the declared name of the current control method"},
    {"__IASL__",                            "Permanently defined for the iASL compiler"},
    {NULL,                                   NULL}
};
