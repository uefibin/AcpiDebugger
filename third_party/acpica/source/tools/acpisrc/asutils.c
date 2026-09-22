/******************************************************************************
 *
 * Module Name: asutils - common utilities
 *
 *****************************************************************************/

/******************************************************************************
 *
 * Copyright (c) 1999 - 2026, Intel Corp.
 * SPDX-License-Identifier: BSD-3-Clause OR GPL-2.0-only
 *
 *****************************************************************************/

#include "acpisrc.h"


/******************************************************************************
 *
 * FUNCTION:    AsSkipUntilChar
 *
 * DESCRIPTION: Find the next instance of the input character
 *
 ******************************************************************************/

char *
AsSkipUntilChar (
    char                    *Buffer,
    char                    Target)
{

    while (*Buffer != Target)
    {
        if (!*Buffer)
        {
            return (NULL);
        }

        Buffer++;
    }

    return (Buffer);
}


/******************************************************************************
 *
 * FUNCTION:    AsSkipPastChar
 *
 * DESCRIPTION: Find the next instance of the input character, return a buffer
 *              pointer to this character+1.
 *
 ******************************************************************************/

char *
AsSkipPastChar (
    char                    *Buffer,
    char                    Target)
{

    while (*Buffer != Target)
    {
        if (!*Buffer)
        {
            return (NULL);
        }

        Buffer++;
    }

    Buffer++;
    return (Buffer);
}


/******************************************************************************
 *
 * FUNCTION:    AsReplaceData
 *
 * DESCRIPTION: This function inserts and removes data from the file buffer.
 *              if more data is inserted than is removed, the data in the buffer
 *              is moved to make room. If less data is inserted than is removed,
 *              the remaining data is moved to close the hole.
 *
 ******************************************************************************/

char *
AsReplaceData (
    char                    *Buffer,
    UINT32                  LengthToRemove,
    char                    *BufferToAdd,
    UINT32                  LengthToAdd)
{
    UINT32                  BufferLength;


    /*
     * Buffer is a string, so the length must include the terminating zero
     */
    BufferLength = strlen (Buffer) + 1;

    if (LengthToRemove != LengthToAdd)
    {
        /*
         * Move some of the existing data
         * 1) If adding more bytes than removing, make room for the new data
         * 2) if removing more bytes than adding, delete the extra space
         */
        Gbl_MadeChanges = TRUE;
        memmove ((Buffer + LengthToAdd), (Buffer + LengthToRemove),
            (BufferLength - LengthToRemove));
    }

    /*
     * Now we can move in the new data
     */
    if (LengthToAdd > 0)
    {
        Gbl_MadeChanges = TRUE;
        memmove (Buffer, BufferToAdd, LengthToAdd);
    }

    return (Buffer + LengthToAdd);
}


/******************************************************************************
 *
 * FUNCTION:    AsInsertData
 *
 * DESCRIPTION: This function inserts and removes data from the file buffer.
 *              if more data is inserted than is removed, the data in the buffer
 *              is moved to make room. If less data is inserted than is removed,
 *              the remaining data is moved to close the hole.
 *
 ******************************************************************************/

char *
AsInsertData (
    char                    *Buffer,
    char                    *BufferToAdd,
    UINT32                  LengthToAdd)
{
    UINT32                  BufferLength;


    if (LengthToAdd > 0)
    {
        /*
         * Buffer is a string, so the length must include the terminating zero
         */
        BufferLength = strlen (Buffer) + 1;

        /*
         * Move some of the existing data
         * 1) If adding more bytes than removing, make room for the new data
         * 2) if removing more bytes than adding, delete the extra space
         */
        Gbl_MadeChanges = TRUE;
        memmove ((Buffer + LengthToAdd), Buffer, BufferLength);

        /*
         * Now we can move in the new data
         */
        memmove (Buffer, BufferToAdd, LengthToAdd);
    }

    return (Buffer + LengthToAdd);
}


/******************************************************************************
 *
 * FUNCTION:    AsRemoveData
 *
 * DESCRIPTION: This function inserts and removes data from the file buffer.
 *              if more data is inserted than is removed, the data in the buffer
 *              is moved to make room. If less data is inserted than is removed,
 *              the remaining data is moved to close the hole.
 *
 ******************************************************************************/

char *
AsRemoveData (
    char                    *StartPointer,
    char                    *EndPointer)
{
    UINT32                  BufferLength;


    /*
     * Buffer is a string, so the length must include the terminating zero
     */
    BufferLength = strlen (EndPointer) + 1;

    Gbl_MadeChanges = TRUE;
    memmove (StartPointer, EndPointer, BufferLength);

    return (StartPointer);
}
