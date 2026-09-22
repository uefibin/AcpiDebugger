/******************************************************************************
 *
 * Module Name: exstubs - Stub routines for the Example program
 *
 *****************************************************************************/

/******************************************************************************
 *
 * Copyright (c) 1999 - 2026, Intel Corp.
 * SPDX-License-Identifier: BSD-3-Clause OR GPL-2.0-only
 *
 *****************************************************************************/

#include "examples.h"

#include <acutils.h>
#include <acevents.h>
#include <acdispat.h>

#define _COMPONENT          ACPI_EXAMPLE
        ACPI_MODULE_NAME    ("exstubs")


/******************************************************************************
 *
 * DESCRIPTION: Stubs used to facilitate linkage of the example program
 *
 *****************************************************************************/


/* Hardware manager */

ACPI_STATUS
AcpiHwReadPort (
    ACPI_IO_ADDRESS         Address,
    UINT32                  *Value,
    UINT32                  Width)
{
    return (AE_OK);
}

ACPI_STATUS
AcpiHwWritePort (
    ACPI_IO_ADDRESS         Address,
    UINT32                  Value,
    UINT32                  Width)
{
    return (AE_OK);
}


/* Event manager */

ACPI_STATUS
AcpiEvInitializeEvents (
    void)
{
    return (AE_OK);
}
