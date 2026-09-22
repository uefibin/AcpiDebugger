/******************************************************************************
 *
 * Module Name: ahmain - Main module for the acpi help utility
 *
 *****************************************************************************/

/******************************************************************************
 *
 * Copyright (c) 1999 - 2026, Intel Corp.
 * SPDX-License-Identifier: BSD-3-Clause OR GPL-2.0-only
 *
 *****************************************************************************/

#define DEFINE_AHELP_GLOBALS
#include "acpihelp.h"


/* Local prototypes */

static void
AhDisplayUsage (
    void);

#define AH_UTILITY_NAME             "ACPI Help Utility"
#define AH_SUPPORTED_OPTIONS        "adeghikmopstuv^x"


#if defined ACPI_OPTION
#undef ACPI_OPTION
#endif

#define ACPI_OPTION(Name, Description) \
    AcpiOsPrintf ("  %-24s%s\n", Name, Description);


/******************************************************************************
 *
 * FUNCTION:    AhDisplayUsage
 *
 * DESCRIPTION: Usage message
 *
 ******************************************************************************/

static void
AhDisplayUsage (
    void)
{

    ACPI_USAGE_HEADER ("acpihelp <options> [Name/Prefix | HexValue]");
    ACPI_OPTION ("-h",                      "Display help");
    ACPI_OPTION ("-v",                      "Display version information");
    ACPI_OPTION ("-vd",                     "Display build date and time");

    ACPI_USAGE_TEXT ("\nAML Names and Encodings (ACPI Machine Language):\n");
    ACPI_OPTION ("-a [Name/Prefix | *]",    "Display both ASL operator and AML opcode name(s)");
    ACPI_OPTION ("-g [Name/Prefix | *]",    "Display AML grammar elements(s)");
    ACPI_OPTION ("-m [Name/Prefix | *]",    "Display AML opcode name(s)");

    ACPI_USAGE_TEXT ("\nACPI Values:\n");
    ACPI_OPTION ("-e [HexValue]",           "Decode ACPICA exception code");
    ACPI_OPTION ("-o [HexValue]",           "Decode hex AML opcode");
    ACPI_OPTION ("-x [HexValue]",           "Decode iASL exception code");

    ACPI_USAGE_TEXT ("\nASL Names and Symbols (ACPI Source Language):\n");
    ACPI_OPTION ("-k [Name/Prefix | *]",    "Display ASL non-operator keyword(s)");
    ACPI_OPTION ("-p [Name/Prefix | *]",    "Display ASL predefined method name(s)");
    ACPI_OPTION ("-s [Name/Prefix | *]",    "Display ASL operator name(s)");

    ACPI_USAGE_TEXT ("\nOther miscellaneous ACPI Names:\n");
    ACPI_OPTION ("-i [Name/Prefix | *]",    "Display ACPI/PNP Hardware ID(s)");
    ACPI_OPTION ("-d",                      "Display iASL Preprocessor directives");
    ACPI_OPTION ("-t",                      "Display supported ACPI tables");
    ACPI_OPTION ("-u",                      "Display ACPI-related UUIDs");

    ACPI_USAGE_TEXT ("\nName/Prefix or HexValue not specified means \"Display All\"\n");
    ACPI_USAGE_TEXT ("\nDefault search with valid Name/Prefix and no options:\n");
    ACPI_USAGE_TEXT ("    Find ASL/AML operator names - if NamePrefix does not start with underscore\n");
    ACPI_USAGE_TEXT ("    Find ASL predefined method names - if NamePrefix starts with underscore\n");
}


/******************************************************************************
 *
 * FUNCTION:    main
 *
 * DESCRIPTION: C main function for AcpiHelp utility.
 *
 ******************************************************************************/

int ACPI_SYSTEM_XFACE
main (
    int                     argc,
    char                    *argv[])
{
    char                    *Name;
    UINT32                  DecodeType;
    int                     j;


    AcpiOsInitialize ();
    ACPI_DEBUG_INITIALIZE (); /* For debug version only */
    printf (ACPI_COMMON_SIGNON (AH_UTILITY_NAME));
    DecodeType = AH_DECODE_DEFAULT;

    if (argc < 2)
    {
        AhDisplayUsage ();
        return (0);
    }

    /* Command line options */

    while ((j = AcpiGetopt (argc, argv, AH_SUPPORTED_OPTIONS)) != ACPI_OPT_END) switch (j)
    {
    case 'a':

        DecodeType = AH_DECODE_ASL_AML;
        break;

    case 'd':

        DecodeType = AH_DISPLAY_DIRECTIVES;
        break;

    case 'e':

        DecodeType = AH_DECODE_EXCEPTION;
        break;

    case 'g':

        DecodeType = AH_DECODE_AML_TYPE;
        break;

    case 'i':

        DecodeType = AH_DISPLAY_DEVICE_IDS;
        break;

    case 'k':

        DecodeType = AH_DECODE_ASL_KEYWORD;
        break;

    case 'm':

        DecodeType = AH_DECODE_AML;
        break;

    case 'o':

        DecodeType = AH_DECODE_AML_OPCODE;
        break;

    case 'p':

        DecodeType = AH_DECODE_PREDEFINED_NAME;
        break;

    case 's':

        DecodeType = AH_DECODE_ASL;
        break;

    case 't':

        DecodeType = AH_DISPLAY_TABLES;
        break;

    case 'u':

        DecodeType = AH_DISPLAY_UUIDS;
        break;

    case 'x':

        DecodeType = AH_DECODE_ASL_EXCEPTION;
        break;

    case 'v': /* -v: (Version): signon already emitted, just exit */

        switch (AcpiGbl_Optarg[0])
        {
        case '^':  /* -v: (Version) */

            return (1);

        case 'd':

            printf (ACPI_COMMON_BUILD_TIME);
            return (1);

        default:

            printf ("Unknown option: -v%s\n", AcpiGbl_Optarg);
            return (-1);
        }
        break;

    case 'h':
    default:

        AhDisplayUsage ();
        return (-1);
    }

    /* Missing (null) name means "display all" */

    Name = argv[AcpiGbl_Optind];

    switch (DecodeType)
    {
    case AH_DECODE_ASL_AML:

        AhFindAslAndAmlOperators (Name);
        break;

    case AH_DECODE_AML:

        AhFindAmlOpcode (Name);
        break;

    case AH_DECODE_AML_OPCODE:

        AhDecodeAmlOpcode (Name);
        break;

    case AH_DECODE_AML_TYPE:

        AhFindAmlTypes (Name);
        break;

    case AH_DECODE_PREDEFINED_NAME:

        AhFindPredefinedNames (Name);
        break;

    case AH_DECODE_ASL:

        AhFindAslOperators (Name);
        break;

    case AH_DECODE_ASL_KEYWORD:

        AhFindAslKeywords (Name);
        break;

    case AH_DISPLAY_DEVICE_IDS:

        AhDisplayDeviceIds (Name);
        break;

    case AH_DECODE_EXCEPTION:

        AhDecodeException (Name);
        break;

    case AH_DISPLAY_UUIDS:

        AhDisplayUuids ();
        break;

    case AH_DISPLAY_TABLES:

        AhDisplayTables ();
        break;

    case AH_DISPLAY_DIRECTIVES:

        AhDisplayDirectives ();
        break;

    case AH_DECODE_ASL_EXCEPTION:

        AhDecodeAslException (Name);
        break;

   default:

        if (!Name)
        {
            AhFindAslOperators (Name);
            break;
        }

        if (*Name == '_')
        {
            AhFindPredefinedNames (Name);
        }
        else
        {
            AhFindAslAndAmlOperators (Name);
        }
        break;
    }

    return (0);
}
