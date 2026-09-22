/******************************************************************************
 *
 * Module Name: acpidump.h - Include file for AcpiDump utility
 *
 *****************************************************************************/

/******************************************************************************
 *
 * Copyright (c) 1999 - 2026, Intel Corp.
 * SPDX-License-Identifier: BSD-3-Clause OR GPL-2.0-only
 *
 *****************************************************************************/

/*
 * Global variables. Defined in main.c only, externed in all other files
 */
#ifdef _DECLARE_GLOBALS
#define EXTERN
#define INIT_GLOBAL(a,b)        a=b
#else
#define EXTERN                  extern
#define INIT_GLOBAL(a,b)        a
#endif

#include "acpi.h"
#include "accommon.h"
#include "actables.h"
#include "acapps.h"

/* Globals */

EXTERN BOOLEAN              INIT_GLOBAL (Gbl_SummaryMode, FALSE);
EXTERN BOOLEAN              INIT_GLOBAL (Gbl_VerboseMode, FALSE);
EXTERN BOOLEAN              INIT_GLOBAL (Gbl_BinaryMode, FALSE);
EXTERN BOOLEAN              INIT_GLOBAL (Gbl_DumpCustomizedTables, TRUE);
EXTERN BOOLEAN              INIT_GLOBAL (Gbl_DoNotDumpXsdt, FALSE);
EXTERN ACPI_FILE            INIT_GLOBAL (Gbl_OutputFile, NULL);
EXTERN char                 INIT_GLOBAL (*Gbl_OutputFilename, NULL);
EXTERN UINT64               INIT_GLOBAL (Gbl_RsdpBase, 0);

/* Action table used to defer requested options */

typedef struct ap_dump_action
{
    char                    *Argument;
    UINT32                  ToBeDone;

} AP_DUMP_ACTION;

#define AP_MAX_ACTIONS              32

#define AP_DUMP_ALL_TABLES          0
#define AP_DUMP_TABLE_BY_ADDRESS    1
#define AP_DUMP_TABLE_BY_NAME       2
#define AP_DUMP_TABLE_BY_FILE       3

#define AP_MAX_ACPI_FILES           256 /* Prevent infinite loops */

/* Minimum FADT sizes for various table addresses */

#define MIN_FADT_FOR_DSDT           (ACPI_FADT_OFFSET (Dsdt) + sizeof (UINT32))
#define MIN_FADT_FOR_FACS           (ACPI_FADT_OFFSET (Facs) + sizeof (UINT32))
#define MIN_FADT_FOR_XDSDT          (ACPI_FADT_OFFSET (XDsdt) + sizeof (UINT64))
#define MIN_FADT_FOR_XFACS          (ACPI_FADT_OFFSET (XFacs) + sizeof (UINT64))


/*
 * apdump - Table get/dump routines
 */
int
ApDumpTableFromFile (
    char                    *Pathname);

int
ApDumpTableByName (
    char                    *Signature);

int
ApDumpTableByAddress (
    char                    *AsciiAddress);

int
ApDumpAllTables (
    void);

BOOLEAN
ApIsValidHeader (
    ACPI_TABLE_HEADER       *Table);

BOOLEAN
ApIsValidChecksum (
    ACPI_TABLE_HEADER       *Table);

UINT32
ApGetTableLength (
    ACPI_TABLE_HEADER       *Table);


/*
 * apfiles - File I/O utilities
 */
int
ApOpenOutputFile (
    char                    *Pathname);

int
ApWriteToBinaryFile (
    ACPI_TABLE_HEADER       *Table,
    UINT32                  Instance);

ACPI_TABLE_HEADER *
ApGetTableFromFile (
    char                    *Pathname,
    UINT32                  *FileSize);
