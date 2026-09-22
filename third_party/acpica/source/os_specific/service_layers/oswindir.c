/******************************************************************************
 *
 * Module Name: oswindir - Windows directory access interfaces
 *
 *****************************************************************************/

/******************************************************************************
 *
 * Copyright (c) 1999 - 2026, Intel Corp.
 * SPDX-License-Identifier: BSD-3-Clause OR GPL-2.0-only
 *
 *****************************************************************************/

#include <acpi.h>

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <io.h>

typedef struct ExternalFindInfo
{
    struct _finddata_t          DosInfo;
    char                        *FullWildcardSpec;
    long                        FindHandle;
    char                        State;
    char                        RequestedFileType;

} EXTERNAL_FIND_INFO;


/*******************************************************************************
 *
 * FUNCTION:    AcpiOsOpenDirectory
 *
 * PARAMETERS:  DirPathname         - Full pathname to the directory
 *              WildcardSpec        - string of the form "*.c", etc.
 *              RequestedFileType   - Either a directory or normal file
 *
 * RETURN:      A directory "handle" to be used in subsequent search operations.
 *              NULL returned on failure.
 *
 * DESCRIPTION: Open a directory in preparation for a wildcard search
 *
 ******************************************************************************/

void *
AcpiOsOpenDirectory (
    char                    *DirPathname,
    char                    *WildcardSpec,
    char                    RequestedFileType)
{
    long                    FindHandle;
    char                    *FullWildcardSpec;
    EXTERNAL_FIND_INFO      *SearchInfo;


    /* No directory path means "use current directory" - use a dot */

    if (!DirPathname || strlen (DirPathname) == 0)
    {
        DirPathname = ".";
    }

    /* Allocate the info struct that will be returned to the caller */

    SearchInfo = calloc (sizeof (EXTERNAL_FIND_INFO), 1);
    if (!SearchInfo)
    {
        return (NULL);
    }

    /* Allocate space for the full wildcard path */

    FullWildcardSpec = calloc (
        strlen (DirPathname) + strlen (WildcardSpec) + 2, 1);
    if (!FullWildcardSpec)
    {
        printf ("Could not allocate buffer for wildcard pathname\n");
        free (SearchInfo);
        return (NULL);
    }

    /* Create the full wildcard path */

    strcpy (FullWildcardSpec, DirPathname);
    strcat (FullWildcardSpec, "/");
    strcat (FullWildcardSpec, WildcardSpec);

    /* Initialize the find functions, get first match */

    FindHandle = _findfirst (FullWildcardSpec, &SearchInfo->DosInfo);
    if (FindHandle == -1)
    {
        /* Failure means that no match was found */

        free (FullWildcardSpec);
        free (SearchInfo);
        return (NULL);
    }

    /* Save the info in the return structure */

    SearchInfo->RequestedFileType = RequestedFileType;
    SearchInfo->FullWildcardSpec = FullWildcardSpec;
    SearchInfo->FindHandle = FindHandle;
    SearchInfo->State = 0;
    return (SearchInfo);
}


/*******************************************************************************
 *
 * FUNCTION:    AcpiOsGetNextFilename
 *
 * PARAMETERS:  DirHandle           - Created via AcpiOsOpenDirectory
 *
 * RETURN:      Next filename matched. NULL if no more matches.
 *
 * DESCRIPTION: Get the next file in the directory that matches the wildcard
 *              specification.
 *
 ******************************************************************************/

char *
AcpiOsGetNextFilename (
    void                    *DirHandle)
{
    EXTERNAL_FIND_INFO      *SearchInfo = DirHandle;
    int                     Status;
    char                    FileTypeNotMatched = 1;


    /*
     * Loop while we have matched files but not found any files of
     * the requested type.
     */
    while (FileTypeNotMatched)
    {
        /* On the first call, we already have the first match */

        if (SearchInfo->State == 0)
        {
            /* No longer the first match */

            SearchInfo->State = 1;
        }
        else
        {
            /* Get the next match */

            Status = _findnext (SearchInfo->FindHandle, &SearchInfo->DosInfo);
            if (Status != 0)
            {
                return (NULL);
            }
        }

        /*
         * Found a match, now check to make sure that the file type
         * matches the requested file type (directory or normal file)
         *
         * NOTE: use of the attrib field saves us from doing a very
         * expensive stat() on the file!
         */
        switch (SearchInfo->RequestedFileType)
        {
        case REQUEST_FILE_ONLY:

            /* Anything other than A_SUBDIR is OK */

            if (!(SearchInfo->DosInfo.attrib & _A_SUBDIR))
            {
                FileTypeNotMatched = 0;
            }
            break;

        case REQUEST_DIR_ONLY:

            /* Must have A_SUBDIR bit set */

            if (SearchInfo->DosInfo.attrib & _A_SUBDIR)
            {
                FileTypeNotMatched = 0;
            }
            break;

        default:

            return (NULL);
        }
    }

    return (SearchInfo->DosInfo.name);
}


/*******************************************************************************
 *
 * FUNCTION:    AcpiOsCloseDirectory
 *
 * PARAMETERS:  DirHandle           - Created via AcpiOsOpenDirectory
 *
 * RETURN:      None
 *
 * DESCRIPTION: Close the open directory and cleanup.
 *
 ******************************************************************************/

void
AcpiOsCloseDirectory (
    void                    *DirHandle)
{
    EXTERNAL_FIND_INFO      *SearchInfo = DirHandle;


    /* Close the directory and free allocations */

    _findclose (SearchInfo->FindHandle);
    free (SearchInfo->FullWildcardSpec);
    free (DirHandle);
}
