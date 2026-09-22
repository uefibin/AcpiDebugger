/******************************************************************************
 *
 * Module Name: ahasl - ASL operator decoding for acpihelp utility
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
AhDisplayAslOperator (
    const AH_ASL_OPERATOR   *Op);

static void
AhDisplayOperatorKeywords (
    const AH_ASL_OPERATOR   *Op);

static void
AhDisplayAslKeyword (
    const AH_ASL_KEYWORD    *Op);


/*******************************************************************************
 *
 * FUNCTION:    AhFindAslKeywords (entry point for ASL keyword search)
 *
 * PARAMETERS:  Name                - Name or prefix for an ASL keyword.
 *                                    NULL means "find all"
 *
 * RETURN:      None
 *
 * DESCRIPTION: Find all ASL keywords that match the input Name or name
 *              prefix.
 *
 ******************************************************************************/

void
AhFindAslKeywords (
    char                    *Name)
{
    const AH_ASL_KEYWORD    *Keyword;
    BOOLEAN                 Found = FALSE;


    AcpiUtStrupr (Name);

    for (Keyword = Gbl_AslKeywordInfo; Keyword->Name; Keyword++)
    {
        if (!Name || (Name[0] == '*'))
        {
            AhDisplayAslKeyword (Keyword);
            Found = TRUE;
            continue;
        }

        /* Upper case the operator name before substring compare */

        strcpy (Gbl_Buffer, Keyword->Name);
        AcpiUtStrupr (Gbl_Buffer);

        if (strstr (Gbl_Buffer, Name) == Gbl_Buffer)
        {
            AhDisplayAslKeyword (Keyword);
            Found = TRUE;
        }
    }

    if (!Found)
    {
        printf ("%s, no matching ASL keywords\n", Name);
    }
}


/*******************************************************************************
 *
 * FUNCTION:    AhDisplayAslKeyword
 *
 * PARAMETERS:  Op                  - Pointer to ASL keyword with syntax info
 *
 * RETURN:      None
 *
 * DESCRIPTION: Format and display syntax info for an ASL keyword. Splits
 *              long lines appropriately for reading.
 *
 ******************************************************************************/

static void
AhDisplayAslKeyword (
    const AH_ASL_KEYWORD    *Op)
{

    /* ASL keyword name and description */

    printf ("%22s: %s\n", Op->Name, Op->Description);
    if (!Op->KeywordList)
    {
        return;
    }

    /* List of actual keywords */

    AhPrintOneField (24, 0, AH_MAX_ASL_LINE_LENGTH, Op->KeywordList);
    printf ("\n");
}


/*******************************************************************************
 *
 * FUNCTION:    AhFindAslAndAmlOperators
 *
 * PARAMETERS:  Name                - Name or prefix for an ASL operator.
 *                                    NULL means "find all"
 *
 * RETURN:      None
 *
 * DESCRIPTION: Find all ASL operators that match the input Name or name
 *              prefix. Also displays the AML information if only one entry
 *              matches.
 *
 ******************************************************************************/

void
AhFindAslAndAmlOperators (
    char                    *Name)
{
    UINT32                  MatchCount;


    MatchCount = AhFindAslOperators (Name);
    if (MatchCount == 1)
    {
        AhFindAmlOpcode (Name);
    }
}


/*******************************************************************************
 *
 * FUNCTION:    AhFindAslOperators (entry point for ASL operator search)
 *
 * PARAMETERS:  Name                - Name or prefix for an ASL operator.
 *                                    NULL means "find all"
 *
 * RETURN:      Number of operators that matched the name prefix.
 *
 * DESCRIPTION: Find all ASL operators that match the input Name or name
 *              prefix.
 *
 ******************************************************************************/

UINT32
AhFindAslOperators (
    char                    *Name)
{
    const AH_ASL_OPERATOR   *Operator;
    BOOLEAN                 MatchCount = 0;


    AcpiUtStrupr (Name);

    /* Find/display all names that match the input name prefix */

    for (Operator = Gbl_AslOperatorInfo; Operator->Name; Operator++)
    {
        if (!Name || (Name[0] == '*'))
        {
            AhDisplayAslOperator (Operator);
            MatchCount++;
            continue;
        }

        /* Upper case the operator name before substring compare */

        strcpy (Gbl_Buffer, Operator->Name);
        AcpiUtStrupr (Gbl_Buffer);

        if (strstr (Gbl_Buffer, Name) == Gbl_Buffer)
        {
            AhDisplayAslOperator (Operator);
            MatchCount++;
        }
    }

    if (!MatchCount)
    {
        printf ("%s, no matching ASL operators\n", Name);
    }

    return (MatchCount);
}


/*******************************************************************************
 *
 * FUNCTION:    AhDisplayAslOperator
 *
 * PARAMETERS:  Op                  - Pointer to ASL operator with syntax info
 *
 * RETURN:      None
 *
 * DESCRIPTION: Format and display syntax info for an ASL operator. Splits
 *              long lines appropriately for reading.
 *
 ******************************************************************************/

static void
AhDisplayAslOperator (
    const AH_ASL_OPERATOR   *Op)
{

    /* ASL operator name and description */

    printf ("%16s: %s\n", Op->Name, Op->Description);
    if (!Op->Syntax)
    {
        return;
    }

    /* Syntax for the operator */

    AhPrintOneField (18, 0, AH_MAX_ASL_LINE_LENGTH, Op->Syntax);
    printf ("\n");

    AhDisplayOperatorKeywords (Op);
    printf ("\n");
}


/*******************************************************************************
 *
 * FUNCTION:    AhDisplayOperatorKeywords
 *
 * PARAMETERS:  Op                  - Pointer to ASL keyword with syntax info
 *
 * RETURN:      None
 *
 * DESCRIPTION: Display any/all keywords that are associated with the ASL
 *              operator.
 *
 ******************************************************************************/

static void
AhDisplayOperatorKeywords (
    const AH_ASL_OPERATOR   *Op)
{
    char                    *Token;
    char                    *Separators = "(){}, ";
    BOOLEAN                 FirstKeyword = TRUE;


    if (!Op || !Op->Syntax)
    {
        return;
    }

    /*
     * Find all parameters that have the word "keyword" within, and then
     * display the info about that keyword
     */
    strcpy (Gbl_LineBuffer, Op->Syntax);
    Token = strtok (Gbl_LineBuffer, Separators);
    while (Token)
    {
        if (strstr (Token, "Keyword"))
        {
            if (FirstKeyword)
            {
                printf ("\n");
                FirstKeyword = FALSE;
            }

            /* Found a keyword, display keyword information */

            AhFindAslKeywords (Token);
        }

        Token = strtok (NULL, Separators);
    }
}
