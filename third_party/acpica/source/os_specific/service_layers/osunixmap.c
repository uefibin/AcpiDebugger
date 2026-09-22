/******************************************************************************
 *
 * Module Name: osunixmap - Unix OSL for file mappings
 *
 *****************************************************************************/

/******************************************************************************
 *
 * Copyright (c) 1999 - 2026, Intel Corp.
 * SPDX-License-Identifier: BSD-3-Clause OR GPL-2.0-only
 *
 *****************************************************************************/

#include "acpidump.h"
#include <unistd.h>
#include <sys/mman.h>
#ifdef _FreeBSD
#include <sys/param.h>
#endif

#define _COMPONENT          ACPI_OS_SERVICES
        ACPI_MODULE_NAME    ("osunixmap")


#ifndef O_BINARY
#define O_BINARY 0
#endif

#if defined(_DragonFly) || defined(_FreeBSD) || defined(_QNX)
#define MMAP_FLAGS          MAP_SHARED
#else
#define MMAP_FLAGS          MAP_PRIVATE
#endif

#define SYSTEM_MEMORY       "/dev/mem"


/*******************************************************************************
 *
 * FUNCTION:    AcpiOsGetPageSize
 *
 * PARAMETERS:  None
 *
 * RETURN:      Page size of the platform.
 *
 * DESCRIPTION: Obtain page size of the platform.
 *
 ******************************************************************************/

static ACPI_SIZE
AcpiOsGetPageSize (
    void)
{

#ifdef PAGE_SIZE
    return PAGE_SIZE;
#else
    return sysconf (_SC_PAGESIZE);
#endif
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsMapMemory
 *
 * PARAMETERS:  Where               - Physical address of memory to be mapped
 *              Length              - How much memory to map
 *
 * RETURN:      Pointer to mapped memory. Null on error.
 *
 * DESCRIPTION: Map physical memory into local address space.
 *
 *****************************************************************************/

void *
AcpiOsMapMemory (
    ACPI_PHYSICAL_ADDRESS   Where,
    ACPI_SIZE               Length)
{
    UINT8                   *MappedMemory;
    ACPI_PHYSICAL_ADDRESS   Offset;
    ACPI_SIZE               PageSize;
    int                     fd;


    fd = open (SYSTEM_MEMORY, O_RDONLY | O_BINARY);
    if (fd < 0)
    {
        fprintf (stderr, "Cannot open %s\n", SYSTEM_MEMORY);
        return (NULL);
    }

    /* Align the offset to use mmap */

    PageSize = AcpiOsGetPageSize ();
    Offset = Where % PageSize;

    /* Map the table header to get the length of the full table */

    MappedMemory = mmap (NULL, (Length + Offset), PROT_READ, MMAP_FLAGS,
        fd, (Where - Offset));
    if (MappedMemory == MAP_FAILED)
    {
        fprintf (stderr, "Cannot map %s\n", SYSTEM_MEMORY);
        close (fd);
        return (NULL);
    }

    close (fd);
    return (ACPI_CAST8 (MappedMemory + Offset));
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsUnmapMemory
 *
 * PARAMETERS:  Where               - Logical address of memory to be unmapped
 *              Length              - How much memory to unmap
 *
 * RETURN:      None.
 *
 * DESCRIPTION: Delete a previously created mapping. Where and Length must
 *              correspond to a previous mapping exactly.
 *
 *****************************************************************************/

void
AcpiOsUnmapMemory (
    void                    *Where,
    ACPI_SIZE               Length)
{
    ACPI_PHYSICAL_ADDRESS   Offset;
    ACPI_SIZE               PageSize;


    PageSize = AcpiOsGetPageSize ();
    Offset = ACPI_TO_INTEGER (Where) % PageSize;
    munmap ((UINT8 *) Where - Offset, (Length + Offset));
}
