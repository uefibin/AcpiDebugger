/******************************************************************************
 *
 * Module Name: ahaml - AML opcode decoding for acpihelp utility
 *
 *****************************************************************************/

/******************************************************************************
 *
 * Copyright (c) 1999 - 2026, Intel Corp.
 * SPDX-License-Identifier: BSD-3-Clause OR GPL-2.0-only
 *
 *****************************************************************************/

#include "acpihelp.h"


/* Local prototypes */

static void
AhDisplayAmlOpcode (
    const AH_AML_OPCODE     *Op);

static void
AhDisplayAmlType (
    const AH_AML_TYPE       *Op);


/*******************************************************************************
 *
 * FUNCTION:    AhFindAmlOpcode (entry point for AML opcode name search)
 *
 * PARAMETERS:  Name                - Name or prefix for an AML opcode.
 *                                    NULL means "find all"
 *
 * RETURN:      None
 *
 * DESCRIPTION: Find all AML opcodes that match the input Name or name
 *              prefix.
 *
 ******************************************************************************/

void
AhFindAmlOpcode (
    char                    *Name)
{
    const AH_AML_OPCODE     *Op;
    BOOLEAN                 Found = FALSE;


    AcpiUtStrupr (Name);

    /* Find/display all opcode names that match the input name prefix */

    for (Op = Gbl_AmlOpcodeInfo; Op->OpcodeString; Op++)
    {
        if (!Op->OpcodeName) /* Unused opcodes */
        {
            continue;
        }

        if (!Name || (Name[0] == '*'))
        {
            AhDisplayAmlOpcode (Op);
            Found = TRUE;
            continue;
        }

        /* Upper case the opcode name before substring compare */

        strcpy (Gbl_Buffer, Op->OpcodeName);
        AcpiUtStrupr (Gbl_Buffer);

        if (strstr (Gbl_Buffer, Name) == Gbl_Buffer)
        {
            AhDisplayAmlOpcode (Op);
            Found = TRUE;
        }
    }

    if (!Found && Name)
    {
        printf ("%s, no matching AML opcodes\n", Name);
    }
}


/*******************************************************************************
 *
 * FUNCTION:    AhDecodeAmlOpcode (entry point for AML opcode search)
 *
 * PARAMETERS:  OpcodeString        - String version of AML opcode
 *
 * RETURN:      None
 *
 * DESCRIPTION: Display information about the input AML opcode
 *
 ******************************************************************************/

void
AhDecodeAmlOpcode (
    char                    *OpcodeString)
{
    const AH_AML_OPCODE     *Op;
    UINT32                  Opcode;
    UINT8                   Prefix;


    if (!OpcodeString)
    {
        AhFindAmlOpcode (NULL);
        return;
    }

    Opcode = strtoul (OpcodeString, NULL, 16);
    if (Opcode > ACPI_UINT16_MAX)
    {
        printf ("Invalid opcode (more than 16 bits)\n");
        return;
    }

    /* Only valid opcode extension is 0x5B */

    Prefix = (Opcode & 0x0000FF00) >> 8;
    if (Prefix && (Prefix != 0x5B))
    {
        printf ("Invalid opcode (invalid extension prefix 0x%X)\n",
            Prefix);
        return;
    }

    /* Find/Display the opcode. May fall within an opcode range */

    for (Op = Gbl_AmlOpcodeInfo; Op->OpcodeString; Op++)
    {
        if ((Opcode >= Op->OpcodeRangeStart) &&
            (Opcode <= Op->OpcodeRangeEnd))
        {
            AhDisplayAmlOpcode (Op);
        }
    }
}


/*******************************************************************************
 *
 * FUNCTION:    AhDisplayAmlOpcode
 *
 * PARAMETERS:  Op                  - An opcode info struct
 *
 * RETURN:      None
 *
 * DESCRIPTION: Display the contents of an AML opcode information struct
 *
 ******************************************************************************/

static void
AhDisplayAmlOpcode (
    const AH_AML_OPCODE     *Op)
{

    if (!Op->OpcodeName)
    {
        printf ("%18s: Opcode=%-9s\n", "Reserved opcode", Op->OpcodeString);
        return;
    }

    /* Opcode name and value(s) */

    printf ("%18s: Opcode=%-9s Type (%s)",
        Op->OpcodeName, Op->OpcodeString, Op->Type);

    /* Optional fixed/static arguments */

    if (Op->FixedArguments)
    {
        printf (" FixedArgs (");
        AhPrintOneField (37, 36 + 7 + strlen (Op->Type) + 12,
            AH_MAX_AML_LINE_LENGTH, Op->FixedArguments);
        printf (")");
    }

    /* Optional variable-length argument list */

    if (Op->VariableArguments)
    {
        if (Op->FixedArguments)
        {
            printf ("\n%*s", 36, " ");
        }
        printf (" VariableArgs (");
        AhPrintOneField (37, 15, AH_MAX_AML_LINE_LENGTH, Op->VariableArguments);
        printf (")");
    }
    printf ("\n");

    /* Grammar specification */

    if (Op->Grammar)
    {
        AhPrintOneField (37, 0, AH_MAX_AML_LINE_LENGTH, Op->Grammar);
        printf ("\n");
    }
}


/*******************************************************************************
 *
 * FUNCTION:    AhFindAmlTypes (entry point for AML grammar keyword search)
 *
 * PARAMETERS:  Name                - Name or prefix for an AML grammar element.
 *                                    NULL means "find all"
 *
 * RETURN:      None
 *
 * DESCRIPTION: Find all AML grammar keywords that match the input Name or name
 *              prefix.
 *
 ******************************************************************************/

void
AhFindAmlTypes (
    char                    *Name)
{
    const AH_AML_TYPE       *Keyword;
    BOOLEAN                 Found = FALSE;


    AcpiUtStrupr (Name);

    for (Keyword = Gbl_AmlTypesInfo; Keyword->Name; Keyword++)
    {
        if (!Name)
        {
            printf ("    %s\n", Keyword->Name);
            Found = TRUE;
            continue;
        }

        if (*Name == '*')
        {
            AhDisplayAmlType (Keyword);
            Found = TRUE;
            continue;
        }

        /* Upper case the operator name before substring compare */

        strcpy (Gbl_Buffer, Keyword->Name);
        AcpiUtStrupr (Gbl_Buffer);

        if (strstr (Gbl_Buffer, Name) == Gbl_Buffer)
        {
            AhDisplayAmlType (Keyword);
            Found = TRUE;
        }
    }

    if (!Found)
    {
        printf ("%s, no matching AML grammar type\n", Name);
    }
}


/*******************************************************************************
 *
 * FUNCTION:    AhDisplayAmlType
 *
 * PARAMETERS:  Op                  - Pointer to AML grammar info
 *
 * RETURN:      None
 *
 * DESCRIPTION: Format and display info for an AML grammar element.
 *
 ******************************************************************************/

static void
AhDisplayAmlType (
    const AH_AML_TYPE       *Op)
{
    char                    *Description;


    Description = Op->Description;
    printf ("%4s", " ");    /* Primary indent */

    /* Emit the entire description string */

    while (*Description)
    {
        /* Description can be multiple lines, must indent each */

        while (*Description != '\n')
        {
            printf ("%c", *Description);
            Description++;
        }

        printf ("\n");
        Description++;

        /* Do indent */

        if (*Description)
        {
            printf ("%8s", " ");    /* Secondary indent */

            /* Index extra for a comment */

            if ((Description[0] == '/') &&
                (Description[1] == '/'))
            {
                printf ("%4s", " ");
            }
        }
    }

    printf ("\n");
}
