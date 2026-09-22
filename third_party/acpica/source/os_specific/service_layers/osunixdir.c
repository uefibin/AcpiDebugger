/******************************************************************************
 *
 * Module Name: osunixdir - Unix directory access interfaces
 *
 *****************************************************************************/

/******************************************************************************
 *
 * Copyright (c) 1999 - 2026, Intel Corp.
 * SPDX-License-Identifier: BSD-3-Clause OR GPL-2.0-only
 *
 *****************************************************************************/

#include "acpi.h"

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <dirent.h>
#include <fnmatch.h>
#include <ctype.h>
#include <sys/stat.h>

/*
 * Allocated structure returned from OsOpenDirectory
 */
typedef struct ExternalFindInfo
{
    char                        *DirPathname;
    DIR                         *DirPtr;
    char                        temp_buffer[256];
    char                        *WildcardSpec;
    char                        RequestedFileType;

} EXTERNAL_FIND_INFO;


/*******************************************************************************
 *
 * FUNCTION:    AcpiOsOpenDirectory
 *
 * PARAMETERS:  DirPathname         - Full pathname to the directory
 *              WildcardSpec        - string of the form "*.c", etc.
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
    EXTERNAL_FIND_INFO      *ExternalInfo;
    DIR                     *dir;


    /* Allocate the info struct that will be returned to the caller */

    ExternalInfo = calloc (1, sizeof (EXTERNAL_FIND_INFO));
    if (!ExternalInfo)
    {
        return (NULL);
    }

    /* Get the directory stream */

    dir = opendir (DirPathname);
    if (!dir)
    {
        fprintf (stderr, "Cannot open directory - %s\n", DirPathname);
        free (ExternalInfo);
        return (NULL);
    }

    /* Save the info in the return structure */

    ExternalInfo->WildcardSpec = WildcardSpec;
    ExternalInfo->RequestedFileType = RequestedFileType;
    ExternalInfo->DirPathname = DirPathname;
    ExternalInfo->DirPtr = dir;
    return (ExternalInfo);
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
    EXTERNAL_FIND_INFO      *ExternalInfo = DirHandle;
    struct dirent           *dir_entry;
    char                    *temp_str;
    int                     str_len;
    struct stat             temp_stat;
    int                     err;


    while ((dir_entry = readdir (ExternalInfo->DirPtr)))
    {
        if (!fnmatch (ExternalInfo->WildcardSpec, dir_entry->d_name, 0))
        {
            if (dir_entry->d_name[0] == '.')
            {
                continue;
            }

            str_len = strlen (dir_entry->d_name) +
                        strlen (ExternalInfo->DirPathname) + 2;

            temp_str = calloc (str_len, 1);
            if (!temp_str)
            {
                fprintf (stderr,
                    "Could not allocate buffer for temporary string\n");
                return (NULL);
            }

            strcpy (temp_str, ExternalInfo->DirPathname);
            strcat (temp_str, "/");
            strcat (temp_str, dir_entry->d_name);

            err = stat (temp_str, &temp_stat);
            if (err == -1)
            {
                fprintf (stderr,
                    "Cannot stat file (should not happen) - %s\n",
                    temp_str);
                free (temp_str);
                return (NULL);
            }

            free (temp_str);

            if ((S_ISDIR (temp_stat.st_mode)
                && (ExternalInfo->RequestedFileType == REQUEST_DIR_ONLY))
               ||
               ((!S_ISDIR (temp_stat.st_mode)
                && ExternalInfo->RequestedFileType == REQUEST_FILE_ONLY)))
            {
                /* copy to a temp buffer because dir_entry struct is on the stack */

                strcpy (ExternalInfo->temp_buffer, dir_entry->d_name);
                return (ExternalInfo->temp_buffer);
            }
        }
    }

    return (NULL);
}


/*******************************************************************************
 *
 * FUNCTION:    AcpiOsCloseDirectory
 *
 * PARAMETERS:  DirHandle           - Created via AcpiOsOpenDirectory
 *
 * RETURN:      None.
 *
 * DESCRIPTION: Close the open directory and cleanup.
 *
 ******************************************************************************/

void
AcpiOsCloseDirectory (
    void                    *DirHandle)
{
    EXTERNAL_FIND_INFO      *ExternalInfo = DirHandle;


    /* Close the directory and free allocations */

    closedir (ExternalInfo->DirPtr);
    free (DirHandle);
}
