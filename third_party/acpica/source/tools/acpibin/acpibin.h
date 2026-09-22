/******************************************************************************
 *
 * Module Name: acpibinh - Include file for AcpiBin utility
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

#define DB_CONSOLE_OUTPUT            0x02
#define ACPI_DB_REDIRECTABLE_OUTPUT  0x01

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


/* Globals */

EXTERN BOOLEAN              INIT_GLOBAL (Gbl_TerseMode, FALSE);
EXTERN BOOLEAN              INIT_GLOBAL (AbGbl_DisplayAllMiscompares, FALSE);
EXTERN UINT32               INIT_GLOBAL (AbGbl_CompareOffset, 0);


/* Prototypes */

int
AbCompareAmlFiles (
    char                    *File1Path,
    char                    *File2Path);

int
AbDumpAmlFile (
    char                    *File1Path,
    char                    *File2Path);

void
AbComputeChecksum (
    char                    *File1Path);

void
AbDisplayHeader (
    char                    *File1Path);
