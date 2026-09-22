/******************************************************************************
 *
 * Module Name: abmain - Main module for the acpi binary utility
 *
 *****************************************************************************/

/******************************************************************************
 *
 * Copyright (c) 1999 - 2026, Intel Corp.
 * SPDX-License-Identifier: BSD-3-Clause OR GPL-2.0-only
 *
 *****************************************************************************/

#define _DECLARE_GLOBALS
#include "acpibin.h"

/* Local prototypes */

static void
AbDisplayUsage (
    UINT8                   OptionCount);


#define AB_UTILITY_NAME             "ACPI Binary Table Dump Utility"
#define AB_SUPPORTED_OPTIONS        "a:c:d:h:o:s:tv^"


/******************************************************************************
 *
 * FUNCTION:    AbDisplayUsage
 *
 * DESCRIPTION: Usage message
 *
 ******************************************************************************/

static void
AbDisplayUsage (
    UINT8                   OptionCount)
{

    if (OptionCount)
    {
        printf ("Option requires %u arguments\n\n", OptionCount);
    }

    ACPI_USAGE_HEADER ("acpibin [options]");

    ACPI_OPTION ("-a <File1> <File2>",      "Compare two binary AML files, dump all mismatches");
    ACPI_OPTION ("-c <File1> <File2>",      "Compare two binary AML files, dump first 100 mismatches");
    ACPI_OPTION ("-d <In> <Out>",           "Dump AML binary to text file");
    ACPI_OPTION ("-o <Value>",              "Start comparison at this offset into second file");
    ACPI_OPTION ("-h <File>",               "Display table header for binary AML file");
    ACPI_OPTION ("-s <File>",               "Update checksum for binary AML file");
    ACPI_OPTION ("-t",                      "Terse mode");
    ACPI_OPTION ("-v",                      "Display version information");
    ACPI_OPTION ("-vd",                     "Display build date and time");
}


/******************************************************************************
 *
 * FUNCTION:    main
 *
 * DESCRIPTION: C main function
 *
 ******************************************************************************/

int ACPI_SYSTEM_XFACE
main (
    int                     argc,
    char                    *argv[])
{
    int                     j;
    int                     Status = AE_OK;


    ACPI_DEBUG_INITIALIZE (); /* For debug version only */

    AcpiGbl_DebugFile = NULL;
    AcpiGbl_DbOutputFlags = DB_CONSOLE_OUTPUT;

    AcpiOsInitialize ();
    printf (ACPI_COMMON_SIGNON (AB_UTILITY_NAME));

    if (argc < 2)
    {
        AbDisplayUsage (0);
        return (0);
    }

    /* Command line options */

    while ((j = AcpiGetopt (argc, argv, AB_SUPPORTED_OPTIONS)) != ACPI_OPT_END) switch(j)
    {
    case 'a':   /* Compare Files, display all differences */

        AbGbl_DisplayAllMiscompares = TRUE;

        ACPI_FALLTHROUGH;

    case 'c':   /* Compare Files */

        if (argc < 4)
        {
            AbDisplayUsage (2);
            return (-1);
        }

        Status = AbCompareAmlFiles (AcpiGbl_Optarg, argv[AcpiGbl_Optind]);
        break;

    case 'd':   /* Dump AML file */

        if (argc < 4)
        {
            AbDisplayUsage (2);
            return (-1);
        }

        Status = AbDumpAmlFile (AcpiGbl_Optarg, argv[AcpiGbl_Optind]);
        break;

    case 'h':   /* Display ACPI table header */

        if (argc < 3)
        {
            AbDisplayUsage (1);
            return (-1);
        }

        AbDisplayHeader (AcpiGbl_Optarg);
        return (0);

    case 'o':

        AbGbl_CompareOffset = atoi (AcpiGbl_Optarg);
        continue;

    case 's':   /* Compute/update checksum */

        if (argc < 3)
        {
            AbDisplayUsage (1);
            return (-1);
        }

        AbComputeChecksum (AcpiGbl_Optarg);
        return (0);

    case 't':   /* Enable terse mode */

        Gbl_TerseMode = TRUE;
        break;

    case 'v': /* -v: (Version): signon already emitted, just exit */

        switch (AcpiGbl_Optarg[0])
        {
        case '^':  /* -v: (Version): signon already emitted, just exit */

            return (1);

        case 'd':

            printf (ACPI_COMMON_BUILD_TIME);
            return (1);

        default:

            printf ("Unknown option: -v%s\n", AcpiGbl_Optarg);
            return (-1);
        }
        break;

    default:

        AbDisplayUsage (0);
        return (-1);
    }

    return (Status);
}
