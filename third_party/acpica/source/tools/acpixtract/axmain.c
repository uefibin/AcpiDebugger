/******************************************************************************
 *
 * Module Name: axmain - main module for acpixtract utility
 *
 *****************************************************************************/

/******************************************************************************
 *
 * Copyright (c) 1999 - 2026, Intel Corp.
 * SPDX-License-Identifier: BSD-3-Clause OR GPL-2.0-only
 *
 *****************************************************************************/

#define DEFINE_ACPIXTRACT_GLOBALS
#include "acpixtract.h"


/* Local prototypes */

static void
DisplayUsage (
    void);


/******************************************************************************
 *
 * FUNCTION:    DisplayUsage
 *
 * DESCRIPTION: Usage message
 *
 ******************************************************************************/

static void
DisplayUsage (
    void)
{

    ACPI_USAGE_HEADER ("acpixtract [option] <InputFile>");

    ACPI_OPTION ("-a",                  "Extract all tables, not just DSDT/SSDT");
    ACPI_OPTION ("-f",                  "Force extraction, even if there are errors");
    ACPI_OPTION ("-l",                  "List table summaries, do not extract");
    ACPI_OPTION ("-m",                  "Extract multiple DSDT/SSDTs to a single file");
    ACPI_OPTION ("-s <signature>",      "Extract all tables with <signature>");
    ACPI_OPTION ("-v",                  "Display version information");
    ACPI_OPTION ("-vd",                 "Display build date and time");

    ACPI_USAGE_TEXT ("\nExtract binary ACPI tables from text acpidump output\n");
    ACPI_USAGE_TEXT ("Default invocation extracts the DSDT and all SSDTs\n");
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
    char                    *Filename;
    int                     AxAction;
    int                     Status;
    int                     j;


    Gbl_TableCount = 0;
    Gbl_TableListHead = NULL;
    Gbl_ForceExtraction = FALSE;
    AxAction = AX_EXTRACT_AML_TABLES; /* Default: DSDT & SSDTs */

    ACPI_DEBUG_INITIALIZE (); /* For debug version only */
    AcpiOsInitialize ();
    printf (ACPI_COMMON_SIGNON (AX_UTILITY_NAME));

    if (argc < 2)
    {
        DisplayUsage ();
        return (0);
    }

    /* Command line options */

    while ((j = AcpiGetopt (argc, argv, AX_SUPPORTED_OPTIONS)) != ACPI_OPT_END) switch (j)
    {
    case 'a':

        AxAction = AX_EXTRACT_ALL;          /* Extract all tables found */
        break;

    case 'f':

        Gbl_ForceExtraction = TRUE;           /* Ignore errors */
        break;

    case 'l':

        AxAction = AX_LIST_ALL;             /* List tables only, do not extract */
        break;

    case 'm':

        AxAction = AX_EXTRACT_MULTI_TABLE;  /* Make single file for all DSDT/SSDTs */
        break;

    case 's':

        AxAction = AX_EXTRACT_SIGNATURE;    /* Extract only tables with this sig */
        break;

    case 'v':

        switch (AcpiGbl_Optarg[0])
        {
        case '^':  /* -v: (Version): signon already emitted, just exit */

            exit (0);

        case 'd':

            printf (ACPI_COMMON_BUILD_TIME);
            return (0);

        default:

            printf ("Unknown option: -v%s\n", AcpiGbl_Optarg);
            return (-1);
        }
        break;

    case 'h':
    default:

        DisplayUsage ();
        return (0);
    }

    /* Input filename is always required */

    Filename = argv[AcpiGbl_Optind];
    if (!Filename)
    {
        printf ("Missing required input filename\n");
        return (-1);
    }

    /* Perform requested action */

    switch (AxAction)
    {
    case AX_EXTRACT_ALL:

        Status = AxExtractTables (Filename, NULL, AX_OPTIONAL_TABLES);
        break;

    case AX_EXTRACT_MULTI_TABLE:

        Status = AxExtractToMultiAmlFile (Filename);
        break;

    case AX_LIST_ALL:

        Status = AxListAllTables (Filename);
        break;

    case AX_EXTRACT_SIGNATURE:

        Status = AxExtractTables (Filename, AcpiGbl_Optarg, AX_REQUIRED_TABLE);
        break;

    default:
        /*
         * Default output is the DSDT and all SSDTs. One DSDT is required,
         * any SSDTs are optional.
         */
        Status = AxExtractTables (Filename, "DSDT", AX_REQUIRED_TABLE);
        if (Status)
        {
            return (Status);
        }

        Status = AxExtractTables (Filename, "SSDT", AX_OPTIONAL_TABLES);
        break;
    }

    return (Status);
}
