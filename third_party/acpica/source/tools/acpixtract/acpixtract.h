/******************************************************************************
 *
 * Module Name: acpixtract.h - Include for acpixtract utility
 *
 *****************************************************************************/

/******************************************************************************
 *
 * Copyright (c) 1999 - 2026, Intel Corp.
 * SPDX-License-Identifier: BSD-3-Clause OR GPL-2.0-only
 *
 *****************************************************************************/

#include "acpi.h"
#include "accommon.h"
#include "acapps.h"
#include <stdio.h>


#undef ACPI_GLOBAL

#ifdef DEFINE_ACPIXTRACT_GLOBALS
#define ACPI_GLOBAL(type,name) \
    extern type name; \
    type name

#else
#define ACPI_GLOBAL(type,name) \
    extern type name
#endif


/* Options */

#define AX_EXTRACT_ALL              0
#define AX_LIST_ALL                 1
#define AX_EXTRACT_SIGNATURE        2
#define AX_EXTRACT_AML_TABLES       3
#define AX_EXTRACT_MULTI_TABLE      4

#define AX_OPTIONAL_TABLES          0
#define AX_REQUIRED_TABLE           1

#define AX_UTILITY_NAME             "ACPI Binary Table Extraction Utility"
#define AX_SUPPORTED_OPTIONS        "afhlms:v^"
#define AX_MULTI_TABLE_FILENAME     "amltables.dat"
#define AX_TABLE_INFO_FORMAT        "  %4.4s - %7u bytes written (0x%8.8X) - %s\n"

/* Extraction states */

#define AX_STATE_FIND_HEADER        0
#define AX_STATE_EXTRACT_DATA       1

/* Miscellaneous constants */

#define AX_LINE_BUFFER_SIZE         256
#define AX_MIN_BLOCK_HEADER_LENGTH  6   /* strlen ("DSDT @") */
#define AX_HEX_DATA_LENGTH          49  /*  (3 * 16) + 1 for the colon delimiter */
#define AX_IS_TABLE_BLOCK_HEADER    (strlen (Gbl_LineBuffer) < AX_HEX_DATA_LENGTH && \
                                    strstr (Gbl_LineBuffer, " @ "))


typedef struct AxTableInfo
{
    UINT32                  Signature;
    unsigned int            Instances;
    unsigned int            NextInstance;
    struct AxTableInfo      *Next;

} AX_TABLE_INFO;


/* Globals */

ACPI_GLOBAL (char,           Gbl_LineBuffer[AX_LINE_BUFFER_SIZE]);
ACPI_GLOBAL (char,           Gbl_HeaderBuffer[AX_LINE_BUFFER_SIZE]);
ACPI_GLOBAL (char,           Gbl_InstanceBuffer[AX_LINE_BUFFER_SIZE]);
ACPI_GLOBAL (AX_TABLE_INFO, *Gbl_TableListHead);
ACPI_GLOBAL (char,           Gbl_OutputFilename[32]);
ACPI_GLOBAL (unsigned char,  Gbl_BinaryData[16]);
ACPI_GLOBAL (unsigned int,   Gbl_TableCount);
ACPI_GLOBAL (BOOLEAN,        Gbl_ForceExtraction);


/*
 * acpixtract.c
 */
int
AxExtractTables (
    char                    *InputPathname,
    char                    *Signature,
    unsigned int            MinimumInstances);

int
AxExtractToMultiAmlFile (
    char                    *InputPathname);

int
AxListAllTables (
    char                    *InputPathname);


/*
 * axutils.c
 */
unsigned int
AxCountTableInstances (
    char                    *InputPathname,
    char                    *Signature);

unsigned int
AxGetNextInstance (
    char                    *InputPathname,
    char                    *Signature);

void
AxNormalizeSignature (
    char                    *Signature);

void
AxCheckAscii (
    char                    *Name,
    int                     Count);

BOOLEAN
AxIsFileAscii (
    FILE                    *Handle);

BOOLEAN
AxIsHexDataLine (
    void);

BOOLEAN
AxIsEmptyLine (
    char                    *Buffer);

BOOLEAN
AxIsDataBlockHeader (
    void);

int
AxConvertAndWrite (
    FILE                    *OutputFile,
    char                    *ThisSignature);

int
AxConvertToBinary (
    char                    *InputLine,
    unsigned char           *OutputData);

void
AxDumpTableHeader (
    unsigned char           *Header);
