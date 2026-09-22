/******************************************************************************
 *
 * Module Name: osefixf - EFI OSL interfaces
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

#define _COMPONENT          ACPI_OS_SERVICES
        ACPI_MODULE_NAME    ("osefixf")


/* Local prototypes */

static BOOLEAN
AcpiEfiCompareGuid (
    ACPI_EFI_GUID           *Guid1,
    ACPI_EFI_GUID           *Guid2);

static ACPI_PHYSICAL_ADDRESS
AcpiEfiGetRsdpViaGuid (
    ACPI_EFI_GUID           *Guid);

static ACPI_EFI_PCI_IO *
AcpiEfiGetPciDev (
    ACPI_PCI_ID             *PciId);


/* Global variables */

#ifdef _EDK2_EFI
struct _ACPI_EFI_SYSTEM_TABLE        *ST;
struct _ACPI_EFI_BOOT_SERVICES       *BS;
struct _ACPI_EFI_RUNTIME_SERVICES    *RT;
#endif


/******************************************************************************
 *
 * FUNCTION:    AcpiEfiCompareGuid
 *
 * PARAMETERS:  Guid1               - GUID to compare
 *              Guid2               - GUID to compare
 *
 * RETURN:      TRUE if Guid1 == Guid2
 *
 * DESCRIPTION: Compares two GUIDs
 *
 *****************************************************************************/

static BOOLEAN
AcpiEfiCompareGuid (
    ACPI_EFI_GUID           *Guid1,
    ACPI_EFI_GUID           *Guid2)
{
    INT32                   *g1;
    INT32                   *g2;
    INT32                   r;


    g1 = (INT32 *) Guid1;
    g2 = (INT32 *) Guid2;

    r  = g1[0] - g2[0];
    r |= g1[1] - g2[1];
    r |= g1[2] - g2[2];
    r |= g1[3] - g2[3];

    return (r ? FALSE : TRUE);
}


/******************************************************************************
 *
 * FUNCTION:    AcpiEfiGetRsdpViaGuid
 *
 * PARAMETERS:  None
 *
 * RETURN:      RSDP address if found
 *
 * DESCRIPTION: Find RSDP address via EFI using specified GUID.
 *
 *****************************************************************************/

static ACPI_PHYSICAL_ADDRESS
AcpiEfiGetRsdpViaGuid (
    ACPI_EFI_GUID           *Guid)
{
    ACPI_PHYSICAL_ADDRESS   Address = 0;
    UINTN                   i;


    for (i = 0; i < ST->NumberOfTableEntries; i++)
    {
        if (AcpiEfiCompareGuid (&ST->ConfigurationTable[i].VendorGuid, Guid))
        {
            Address = ACPI_PTR_TO_PHYSADDR (
                ST->ConfigurationTable[i].VendorTable);
            break;
        }
    }

    return (Address);
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsGetRootPointer
 *
 * PARAMETERS:  None
 *
 * RETURN:      RSDP physical address
 *
 * DESCRIPTION: Gets the ACPI root pointer (RSDP)
 *
 *****************************************************************************/

ACPI_PHYSICAL_ADDRESS
AcpiOsGetRootPointer (
    void)
{
    ACPI_PHYSICAL_ADDRESS   Address;
    ACPI_EFI_GUID           Guid10 = ACPI_TABLE_GUID;
    ACPI_EFI_GUID           Guid20 = ACPI_20_TABLE_GUID;


    Address = AcpiEfiGetRsdpViaGuid (&Guid20);
    if (!Address)
    {
        Address = AcpiEfiGetRsdpViaGuid (&Guid10);
    }

    return (Address);
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsPredefinedOverride
 *
 * PARAMETERS:  InitVal             - Initial value of the predefined object
 *              NewVal              - The new value for the object
 *
 * RETURN:      Status, pointer to value. Null pointer returned if not
 *              overriding.
 *
 * DESCRIPTION: Allow the OS to override predefined names
 *
 *****************************************************************************/

ACPI_STATUS
AcpiOsPredefinedOverride (
    const ACPI_PREDEFINED_NAMES *InitVal,
    ACPI_STRING                 *NewVal)
{

    if (!InitVal || !NewVal)
    {
        return (AE_BAD_PARAMETER);
    }

    *NewVal = NULL;
    return (AE_OK);
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsMapMemory
 *
 * PARAMETERS:  where               - Physical address of memory to be mapped
 *              length              - How much memory to map
 *
 * RETURN:      Pointer to mapped memory. Null on error.
 *
 * DESCRIPTION: Map physical memory into caller's address space
 *
 *****************************************************************************/

void *
AcpiOsMapMemory (
    ACPI_PHYSICAL_ADDRESS   where,
    ACPI_SIZE               length)
{

    return (ACPI_TO_POINTER ((ACPI_SIZE) where));
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsUnmapMemory
 *
 * PARAMETERS:  where               - Logical address of memory to be unmapped
 *              length              - How much memory to unmap
 *
 * RETURN:      None
 *
 * DESCRIPTION: Delete a previously created mapping. Where and Length must
 *              correspond to a previous mapping exactly.
 *
 *****************************************************************************/

void
AcpiOsUnmapMemory (
    void                    *where,
    ACPI_SIZE               length)
{

    return;
}


/******************************************************************************
 *
 * FUNCTION:    Single threaded stub interfaces
 *
 * DESCRIPTION: No-op on single threaded BIOS
 *
 *****************************************************************************/

ACPI_STATUS
AcpiOsCreateLock (
    ACPI_SPINLOCK           *OutHandle)
{
    return (AE_OK);
}

void
AcpiOsDeleteLock (
    ACPI_SPINLOCK           Handle)
{
}

ACPI_CPU_FLAGS
AcpiOsAcquireLock (
    ACPI_SPINLOCK           Handle)
{
    return (0);
}

void
AcpiOsReleaseLock (
    ACPI_SPINLOCK           Handle,
    ACPI_CPU_FLAGS          Flags)
{
}

ACPI_STATUS
AcpiOsCreateSemaphore (
    UINT32              MaxUnits,
    UINT32              InitialUnits,
    ACPI_HANDLE         *OutHandle)
{
    *OutHandle = (ACPI_HANDLE) 1;
    return (AE_OK);
}

ACPI_STATUS
AcpiOsDeleteSemaphore (
    ACPI_HANDLE         Handle)
{
    return (AE_OK);
}

ACPI_STATUS
AcpiOsWaitSemaphore (
    ACPI_HANDLE         Handle,
    UINT32              Units,
    UINT16              Timeout)
{
    return (AE_OK);
}

ACPI_STATUS
AcpiOsSignalSemaphore (
    ACPI_HANDLE         Handle,
    UINT32              Units)
{
    return (AE_OK);
}

ACPI_THREAD_ID
AcpiOsGetThreadId (
    void)
{
    return (1);
}

ACPI_STATUS
AcpiOsExecute (
    ACPI_EXECUTE_TYPE       Type,
    ACPI_OSD_EXEC_CALLBACK  Function,
    void                    *Context)
{
    return (AE_OK);
}

void
AcpiOsWaitEventsComplete (
    void)
{
    return;
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsInstallInterruptHandler
 *
 * PARAMETERS:  InterruptNumber     - Level handler should respond to.
 *              ServiceRoutine      - Address of the ACPI interrupt handler
 *              Context             - Where status is returned
 *
 * RETURN:      Handle to the newly installed handler.
 *
 * DESCRIPTION: Install an interrupt handler. Used to install the ACPI
 *              OS-independent handler.
 *
 *****************************************************************************/

UINT32
AcpiOsInstallInterruptHandler (
    UINT32                  InterruptNumber,
    ACPI_OSD_HANDLER        ServiceRoutine,
    void                    *Context)
{

    return (AE_OK);
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsRemoveInterruptHandler
 *
 * PARAMETERS:  InterruptNumber     - Level handler should respond to.
 *              ServiceRoutine      - Address of the ACPI interrupt handler
 *
 * RETURN:      Status
 *
 * DESCRIPTION: Uninstalls an interrupt handler.
 *
 *****************************************************************************/

ACPI_STATUS
AcpiOsRemoveInterruptHandler (
    UINT32                  InterruptNumber,
    ACPI_OSD_HANDLER        ServiceRoutine)
{

    return (AE_OK);
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsGetTimer
 *
 * PARAMETERS:  None
 *
 * RETURN:      Current time in 100 nanosecond units
 *
 * DESCRIPTION: Get the current system time
 *
 *****************************************************************************/

UINT64
AcpiOsGetTimer (
    void)
{
    ACPI_EFI_STATUS         EfiStatus;
    ACPI_EFI_TIME           EfiTime;
    int                     Year, Month, Day;
    int                     Hour, Minute, Second;
    UINT64                  Timer;


    EfiStatus = uefi_call_wrapper (RT->GetTime, 2, &EfiTime, NULL);
    if (ACPI_EFI_ERROR (EfiStatus))
    {
        return ((UINT64) -1);
    }

    Year = EfiTime.Year;
    Month = EfiTime.Month;
    Day = EfiTime.Day;
    Hour = EfiTime.Hour;
    Minute = EfiTime.Minute;
    Second = EfiTime.Second;

    /* 1..12 -> 11,12,1..10 */

    if (0 >= (int) (Month -= 2))
    {
        /* Feb has leap days */

        Month += 12;
        Year -= 1;
    }

    /* Calculate days */

    Timer = ((UINT64) (Year/4 - Year/100 + Year/400 + 367*Month/12 + Day) +
                       Year*365 - 719499);

    /* Calculate seconds */

    (void) AcpiUtShortMultiply (Timer, 24, &Timer);
    Timer += Hour;
    (void) AcpiUtShortMultiply (Timer, 60, &Timer);
    Timer += Minute;
    (void) AcpiUtShortMultiply (Timer, 60, &Timer);
    Timer += Second;

    /* Calculate 100 nanoseconds */

    (void) AcpiUtShortMultiply (Timer, ACPI_100NSEC_PER_SEC, &Timer);
    return (Timer + (EfiTime.Nanosecond / 100));
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsStall
 *
 * PARAMETERS:  microseconds        - Time to sleep
 *
 * RETURN:      Blocks until sleep is completed.
 *
 * DESCRIPTION: Sleep at microsecond granularity
 *
 *****************************************************************************/

void
AcpiOsStall (
    UINT32                  microseconds)
{

    if (microseconds)
    {
        uefi_call_wrapper (BS->Stall, 1, microseconds);
    }
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsSleep
 *
 * PARAMETERS:  milliseconds        - Time to sleep
 *
 * RETURN:      Blocks until sleep is completed.
 *
 * DESCRIPTION: Sleep at millisecond granularity
 *
 *****************************************************************************/

void
AcpiOsSleep (
    UINT64                  milliseconds)
{
    UINT64                  microseconds;


    AcpiUtShortMultiply (milliseconds, ACPI_USEC_PER_MSEC, &microseconds);
    while (microseconds > ACPI_UINT32_MAX)
    {
        AcpiOsStall (ACPI_UINT32_MAX);
        microseconds -= ACPI_UINT32_MAX;
    }
    AcpiOsStall ((UINT32) microseconds);
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsAllocate
 *
 * PARAMETERS:  Size                - Amount to allocate, in bytes
 *
 * RETURN:      Pointer to the new allocation. Null on error.
 *
 * DESCRIPTION: Allocate memory. Algorithm is dependent on the OS.
 *
 *****************************************************************************/

void *
AcpiOsAllocate (
    ACPI_SIZE               Size)
{
    ACPI_EFI_STATUS         EfiStatus;
    void                    *Mem;


    EfiStatus = uefi_call_wrapper (BS->AllocatePool, 3,
        AcpiEfiLoaderData, Size, &Mem);
    if (ACPI_EFI_ERROR (EfiStatus))
    {
        fprintf (stderr,
            "EFI_BOOT_SERVICES->AllocatePool(EfiLoaderData) failure.\n");
        return (NULL);
    }

    return (Mem);
}


#ifdef USE_NATIVE_ALLOCATE_ZEROED
/******************************************************************************
 *
 * FUNCTION:    AcpiOsAllocateZeroed
 *
 * PARAMETERS:  Size                - Amount to allocate, in bytes
 *
 * RETURN:      Pointer to the new allocation. Null on error.
 *
 * DESCRIPTION: Allocate and zero memory. Algorithm is dependent on the OS.
 *
 *****************************************************************************/

void *
AcpiOsAllocateZeroed (
    ACPI_SIZE               Size)
{
    void                    *Mem;


    Mem = AcpiOsAllocate (Size);
    if (Mem)
    {
        memset (Mem, 0, Size);
    }

    return (Mem);
}
#endif


/******************************************************************************
 *
 * FUNCTION:    AcpiOsFree
 *
 * PARAMETERS:  Mem                 - Pointer to previously allocated memory
 *
 * RETURN:      None
 *
 * DESCRIPTION: Free memory allocated via AcpiOsAllocate
 *
 *****************************************************************************/

void
AcpiOsFree (
    void                    *Mem)
{

    uefi_call_wrapper (BS->FreePool, 1, Mem);
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsPrintf
 *
 * PARAMETERS:  Format, ...         - Standard printf format
 *
 * RETURN:      None
 *
 * DESCRIPTION: Formatted output.
 *
 *****************************************************************************/

void ACPI_INTERNAL_VAR_XFACE
AcpiOsPrintf (
    const char              *Format,
    ...)
{
    va_list                 Args;


    va_start (Args, Format);
    AcpiOsVprintf (Format, Args);
    va_end (Args);
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsVprintf
 *
 * PARAMETERS:  Format              - Standard printf format
 *              Args                - Argument list
 *
 * RETURN:      None
 *
 * DESCRIPTION: Formatted output with arguments list pointer.
 *
 *****************************************************************************/

void
AcpiOsVprintf (
    const char              *Format,
    va_list                 Args)
{

    (void) vfprintf (ACPI_FILE_OUT, Format, Args);
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsInitialize, AcpiOsTerminate
 *
 * PARAMETERS:  None
 *
 * RETURN:      Status
 *
 * DESCRIPTION: Initialize/terminate this module.
 *
 *****************************************************************************/

ACPI_STATUS
AcpiOsInitialize (
    void)
{

    return (AE_OK);
}


ACPI_STATUS
AcpiOsTerminate (
    void)
{

    return (AE_OK);
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsSignal
 *
 * PARAMETERS:  Function            - ACPI A signal function code
 *              Info                - Pointer to function-dependent structure
 *
 * RETURN:      Status
 *
 * DESCRIPTION: Miscellaneous functions. Example implementation only.
 *
 *****************************************************************************/

ACPI_STATUS
AcpiOsSignal (
    UINT32                  Function,
    void                    *Info)
{

    switch (Function)
    {
    case ACPI_SIGNAL_FATAL:

        break;

    case ACPI_SIGNAL_BREAKPOINT:

        break;

    default:

        break;
    }

    return (AE_OK);
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsReadable
 *
 * PARAMETERS:  Pointer             - Area to be verified
 *              Length              - Size of area
 *
 * RETURN:      TRUE if readable for entire length
 *
 * DESCRIPTION: Verify that a pointer is valid for reading
 *
 *****************************************************************************/

BOOLEAN
AcpiOsReadable (
    void                    *Pointer,
    ACPI_SIZE               Length)
{

    return (TRUE);
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsWritable
 *
 * PARAMETERS:  Pointer             - Area to be verified
 *              Length              - Size of area
 *
 * RETURN:      TRUE if writable for entire length
 *
 * DESCRIPTION: Verify that a pointer is valid for writing
 *
 *****************************************************************************/

BOOLEAN
AcpiOsWritable (
    void                    *Pointer,
    ACPI_SIZE               Length)
{

    return (TRUE);
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsReadPciConfiguration
 *
 * PARAMETERS:  PciId               - Seg/Bus/Dev
 *              PciRegister         - Device Register
 *              Value               - Buffer where value is placed
 *              Width               - Number of bits
 *
 * RETURN:      Status
 *
 * DESCRIPTION: Read data from PCI configuration space
 *
 *****************************************************************************/

ACPI_STATUS
AcpiOsReadPciConfiguration (
    ACPI_PCI_ID             *PciId,
    UINT32                  PciRegister,
    UINT64                  *Value64,
    UINT32                  Width)
{
    ACPI_EFI_PCI_IO         *PciIo;
    ACPI_EFI_STATUS         EfiStatus;
    UINT8                   Value8;
    UINT16                  Value16;
    UINT32                  Value32;


    *Value64 = 0;
    PciIo = AcpiEfiGetPciDev (PciId);
    if (PciIo == NULL)
    {
        return (AE_NOT_FOUND);
    }

    switch (Width)
    {
    case 8:
        EfiStatus = uefi_call_wrapper (PciIo->Pci.Read, 5, PciIo,
            AcpiEfiPciIoWidthUint8, PciRegister, 1, (VOID *) &Value8);
        *Value64 = Value8;
        break;

    case 16:
        EfiStatus = uefi_call_wrapper (PciIo->Pci.Read, 5, PciIo,
            AcpiEfiPciIoWidthUint16, PciRegister, 1, (VOID *) &Value16);
        *Value64 = Value16;
        break;

    case 32:
        EfiStatus = uefi_call_wrapper (PciIo->Pci.Read, 5, PciIo,
            AcpiEfiPciIoWidthUint32, PciRegister, 1, (VOID *) &Value32);
        *Value64 = Value32;
        break;

    case 64:
        EfiStatus = uefi_call_wrapper (PciIo->Pci.Read, 5, PciIo,
            AcpiEfiPciIoWidthUint64, PciRegister, 1, (VOID *) Value64);
        break;

    default:
        return (AE_BAD_PARAMETER);
    }

    return (ACPI_EFI_ERROR (EfiStatus)) ? (AE_ERROR) : (AE_OK);
}


/******************************************************************************
 *
 * FUNCTION:    AcpiOsWritePciConfiguration
 *
 * PARAMETERS:  PciId               - Seg/Bus/Dev
 *              PciRegister         - Device Register
 *              Value               - Value to be written
 *              Width               - Number of bits
 *
 * RETURN:      Status.
 *
 * DESCRIPTION: Write data to PCI configuration space
 *
 *****************************************************************************/

ACPI_STATUS
AcpiOsWritePciConfiguration (
    ACPI_PCI_ID             *PciId,
    UINT32                  PciRegister,
    UINT64                  Value64,
    UINT32                  Width)
{
    ACPI_EFI_PCI_IO         *PciIo;
    ACPI_EFI_STATUS         EfiStatus;
    UINT8                   Value8;
    UINT16                  Value16;
    UINT32                  Value32;


    PciIo = AcpiEfiGetPciDev (PciId);
    if (PciIo == NULL)
    {
        return (AE_NOT_FOUND);
    }

    switch (Width)
    {
    case 8:
        Value8 = (UINT8) Value64;
        EfiStatus = uefi_call_wrapper (PciIo->Pci.Write, 5, PciIo,
            AcpiEfiPciIoWidthUint8, PciRegister, 1, (VOID *) &Value8);
        break;

    case 16:
        Value16 = (UINT16) Value64;
        EfiStatus = uefi_call_wrapper (PciIo->Pci.Write, 5, PciIo,
            AcpiEfiPciIoWidthUint16, PciRegister, 1, (VOID *) &Value16);
        break;

    case 32:
        Value32 = (UINT32) Value64;
        EfiStatus = uefi_call_wrapper (PciIo->Pci.Write, 5, PciIo,
            AcpiEfiPciIoWidthUint32, PciRegister, 1, (VOID *) &Value32);
        break;

    case 64:
        EfiStatus = uefi_call_wrapper (PciIo->Pci.Write, 5, PciIo,
            AcpiEfiPciIoWidthUint64, PciRegister, 1, (VOID *) &Value64);
        break;

    default:
        return (AE_BAD_PARAMETER);
    }

    return (ACPI_EFI_ERROR (EfiStatus)) ? (AE_ERROR) : (AE_OK);
}

/******************************************************************************
 *
 * FUNCTION:    AcpiEfiGetPciDev
 *
 * PARAMETERS:  PciId               - Seg/Bus/Dev
 *
 * RETURN:      ACPI_EFI_PCI_IO that matches PciId. Null on error.
 *
 * DESCRIPTION: Find ACPI_EFI_PCI_IO for the given PciId.
 *
 *****************************************************************************/

static ACPI_EFI_PCI_IO *
AcpiEfiGetPciDev (
    ACPI_PCI_ID             *PciId)
{
    ACPI_EFI_STATUS         EfiStatus;
    UINTN                   i;
    UINTN                   HandlesCount = 0;
    ACPI_EFI_HANDLE         *Handles = NULL;
    ACPI_EFI_GUID           EfiPciIoProtocol = ACPI_EFI_PCI_IO_PROTOCOL;
    ACPI_EFI_PCI_IO         *PciIoProtocol = NULL;
    UINTN                   Bus;
    UINTN                   Device;
    UINTN                   Function;
    UINTN                   Segment;


    EfiStatus = uefi_call_wrapper (BS->LocateHandleBuffer, 5, AcpiEfiByProtocol,
        &EfiPciIoProtocol, NULL, &HandlesCount, &Handles);
    if (ACPI_EFI_ERROR (EfiStatus))
    {
        return (NULL);
    }

    for (i = 0; i < HandlesCount; i++)
    {
        EfiStatus = uefi_call_wrapper (BS->HandleProtocol, 3, Handles[i],
            &EfiPciIoProtocol, (VOID **) &PciIoProtocol);
        if (!ACPI_EFI_ERROR (EfiStatus))
        {
            EfiStatus = uefi_call_wrapper (PciIoProtocol->GetLocation, 5,
                PciIoProtocol, &Segment, &Bus, &Device, &Function);
            if (!ACPI_EFI_ERROR (EfiStatus) &&
                Bus == PciId->Bus &&
                Device == PciId->Device &&
                Function == PciId->Function &&
                Segment == PciId->Segment)
            {
                uefi_call_wrapper (BS->FreePool, 1, Handles);
                return (PciIoProtocol);
            }
        }
    }

    uefi_call_wrapper (BS->FreePool, 1, Handles);
    return (NULL);
}


#if defined(_EDK2_EFI) && defined(USE_STDLIB)
extern EFI_RUNTIME_SERVICES                 *gRT;
extern EFI_BOOT_SERVICES                    *gBS;
extern EFI_SYSTEM_TABLE                     *gST;

int ACPI_SYSTEM_XFACE
main (
    int                     argc,
    char                    *argv[])
{
    int                     Error;


    ST = ACPI_CAST_PTR (ACPI_EFI_SYSTEM_TABLE, gST);
    BS = ACPI_CAST_PTR (ACPI_EFI_BOOT_SERVICES, gBS);
    RT = ACPI_CAST_PTR (ACPI_EFI_RUNTIME_SERVICES, gRT);

    Error = acpi_main (argc, argv);
    return (Error);
}
#endif
