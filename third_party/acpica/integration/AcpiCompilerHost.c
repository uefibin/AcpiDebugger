#define _CRT_SECURE_NO_WARNINGS

#include "aslcompiler.h"
#include "acapps.h"
#include "acdisasm.h"

#include <windows.h>
#include <fcntl.h>
#include <io.h>
#include <share.h>
#include <setjmp.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

/*
 * ACPICA's compiler is intentionally global and is not re-entrant. The host
 * serializes calls so the WPF process can safely invoke compile/disassemble
 * operations from background tasks without racing ACPICA state or stdout.
 */
static INIT_ONCE AcpiCompilerHostInitOnce = INIT_ONCE_STATIC_INIT;
static CRITICAL_SECTION AcpiCompilerHostLock;
static jmp_buf *AcpiCompilerHostAbortJump;
static FILE *AcpiCompilerHostLogFile;
static char *AcpiCompilerHostInputName;
static BOOLEAN AcpiCompilerHostAcpiInitialized;

void __cdecl
AcpiCompilerHostExit (
    int Code)
{
    if (AcpiCompilerHostAbortJump)
    {
        longjmp (*AcpiCompilerHostAbortJump, Code ? Code : 1);
    }

    ExitProcess ((UINT) Code);
}

static void
AcpiCompilerHostUseLogForCompilerErrors (
    void)
{
    if (AcpiCompilerHostLogFile && AslGbl_Files)
    {
        /* AePrintErrorLog writes through the compiler file table, not the
         * AcpiOsPrintf redirection used for normal ACPICA output. */
        AslGbl_Files[ASL_FILE_STDERR].Handle = AcpiCompilerHostLogFile;
    }
}

static void
AcpiCompilerHostDumpErrors (
    const char *LogPath)
{
    FILE *ErrorFile;

    if (AcpiCompilerHostLogFile && AslGbl_Files)
    {
        /* Reuse the host-owned stream. Re-opening the same path while the
         * compiler stream is active can be rejected by the CRT sharing rules
         * and silently lose diagnostics on Windows. */
        AcpiCompilerHostUseLogForCompilerErrors ();
        AePrintErrorLog (ASL_FILE_STDERR);
        fflush (AcpiCompilerHostLogFile);
        AslGbl_Files[ASL_FILE_STDERR].Handle = stderr;
        return;
    }

    if (!LogPath || !LogPath[0] || !AslGbl_Files)
    {
        return;
    }

    ErrorFile = fopen (LogPath, "a");
    if (ErrorFile)
    {
        AslGbl_Files[ASL_FILE_STDERR].Handle = ErrorFile;
        AePrintErrorLog (ASL_FILE_STDERR);
        fflush (ErrorFile);
        fclose (ErrorFile);
        AslGbl_Files[ASL_FILE_STDERR].Handle = stderr;
    }
}

static BOOL CALLBACK
AcpiCompilerHostInitializeLock (
    PINIT_ONCE InitOnce,
    PVOID      Parameter,
    PVOID      *Context)
{
    UNREFERENCED_PARAMETER (InitOnce);
    UNREFERENCED_PARAMETER (Parameter);
    UNREFERENCED_PARAMETER (Context);

    InitializeCriticalSection (&AcpiCompilerHostLock);
    return TRUE;
}

static void
AcpiCompilerHostLockEnter (
    void)
{
    InitOnceExecuteOnce (
        &AcpiCompilerHostInitOnce,
        AcpiCompilerHostInitializeLock,
        NULL,
        NULL);
    EnterCriticalSection (&AcpiCompilerHostLock);
}

static void
AcpiCompilerHostLockLeave (
    void)
{
    LeaveCriticalSection (&AcpiCompilerHostLock);
}

static void
AcpiCompilerHostResetLineBuffers (
    void)
{
    UtFreeLineBuffers ();
    AslGbl_CurrentLineBuffer = NULL;
    AslGbl_MainTokenBuffer = NULL;
    AslGbl_MacroTokenBuffer = NULL;
    AslGbl_MacroTokenReplaceBuffer = NULL;
    AslGbl_ExpressionTokenBuffer = NULL;
    AslGbl_LineBufPtr = NULL;
}

static void
AcpiCompilerHostResetFlags (
    BOOLEAN Disassemble)
{
    AslGbl_DoCompile = Disassemble ? FALSE : TRUE;
    AslGbl_DoSignon = FALSE;
    AslGbl_PreprocessOnly = FALSE;
    AslGbl_PreprocessFlag = TRUE;
    AslGbl_DisassembleAll = FALSE;
    AslGbl_UseDefaultAmlFilename = TRUE;
    AslGbl_MapfileFlag = FALSE;
    AslGbl_NsOutputFlag = FALSE;
    AslGbl_PreprocessorOutputFlag = FALSE;
    AslGbl_KeepPreprocessorTempFile = FALSE;
    AslGbl_DebugFlag = FALSE;
    AslGbl_CrossReferenceOutput = FALSE;
    AslGbl_AsmOutputFlag = FALSE;
    AslGbl_C_OutputFlag = FALSE;
    AslGbl_C_OffsetTableFlag = FALSE;
    AslGbl_AsmIncludeOutputFlag = FALSE;
    AslGbl_C_IncludeOutputFlag = FALSE;
    AslGbl_ListingFlag = FALSE;
    AslGbl_IgnoreErrors = FALSE;
    AslGbl_SourceOutputFlag = FALSE;
    AslGbl_ParseOnlyFlag = FALSE;
    AslGbl_CompileTimesFlag = FALSE;
    AslGbl_FoldConstants = TRUE;
    AslGbl_VerboseErrors = TRUE;
    AslGbl_WarningsAsErrors = FALSE;
    AslGbl_NoResourceChecking = FALSE;
    AslGbl_IntegerOptimizationFlag = TRUE;
    AslGbl_ReferenceOptimizationFlag = TRUE;
    AslGbl_DisplayRemarks = TRUE;
    AslGbl_DisplayWarnings = TRUE;
    AslGbl_DisplayOptimizations = FALSE;
    AslGbl_Deterministic = TRUE;
    AslGbl_WarningLevel = ASL_WARNING;
    AslGbl_DoTypechecking = TRUE;
    AslGbl_EnableReferenceTypechecking = FALSE;
    AslGbl_DoExternalsInPlace = FALSE;
    AslGbl_DoAslConversion = FALSE;
    AslGbl_OptimizeTrivialParseNodes = TRUE;
    AslGbl_HexOutputFlag = HEX_OUTPUT_NONE;

    AcpiGbl_DisasmFlag = Disassemble;
    AcpiGbl_ForceAmlDisassembly = FALSE;
    AcpiGbl_DmOpt_Disasm = Disassemble;
    AcpiGbl_DmOpt_Verbose = FALSE;
    AcpiGbl_IntegerBitWidth = 32;
    AcpiGbl_IntegerNybbleWidth = 8;
    AcpiGbl_IntegerByteWidth = 4;
    AcpiGbl_NoResourceDisassembly = FALSE;
    AcpiGbl_CstyleDisassembly = TRUE;
    AcpiGbl_CaptureComments = FALSE;
    AcpiGbl_DoDisassemblerOptimizations = TRUE;
}

static int
AcpiCompilerHostAddExternalFiles (
    const char *ExternalFiles)
{
    char *List;
    char *Context = NULL;
    char *Token;
    ACPI_STATUS Status = AE_OK;

    if (!ExternalFiles || !ExternalFiles[0])
    {
        return 0;
    }

    List = _strdup (ExternalFiles);
    if (!List)
    {
        return -1;
    }

    Token = strtok_s (List, ";", &Context);
    while (Token)
    {
        if (Token[0])
        {
            Status = AcpiDmAddToExternalFileList (Token);
            if (ACPI_FAILURE (Status))
            {
                free (List);
                return -1;
            }
        }
        Token = strtok_s (NULL, ";", &Context);
    }

    free (List);
    return 0;
}

static int
AcpiCompilerHostRun (
    const char *InputPath,
    const char *ExternalFiles,
    const char *LogPath,
    BOOLEAN     Disassemble)
{
    ACPI_STATUS Status = AE_OK;
    volatile int ReturnStatus = 0;
    jmp_buf AbortJump;
    int AbortCode;

    AcpiCompilerHostLogFile = NULL;
    AcpiCompilerHostInputName = NULL;
    AcpiCompilerHostAcpiInitialized = FALSE;

    AcpiCompilerHostAbortJump = &AbortJump;
    AbortCode = setjmp (AbortJump);
    if (AbortCode != 0)
    {
        ReturnStatus = -1;
        goto Cleanup;
    }

    if (!InputPath || !InputPath[0])
    {
        ReturnStatus = -1;
        goto Cleanup;
    }

    if (LogPath && LogPath[0])
    {
        AcpiCompilerHostLogFile = _fsopen (LogPath, "w", _SH_DENYNO);
    }

    AcpiOsInitialize ();
    ACPI_DEBUG_INITIALIZE ();
    AcpiGbl_ExternalFileList = NULL;
    AcpiDbgLevel = 0;
    PrInitializePreprocessor ();

    AcpiCompilerHostResetLineBuffers ();
    AslGbl_LineBufferSize = ASL_DEFAULT_LINE_BUFFER_SIZE / 2;
    UtExpandLineBuffers ();
    AcpiCompilerHostResetFlags (Disassemble);
    AslInitializeGlobals ();

    if (AcpiCompilerHostAddExternalFiles (ExternalFiles) != 0)
    {
        ReturnStatus = -1;
        goto Cleanup;
    }

    Status = AdInitialize ();
    if (ACPI_FAILURE (Status))
    {
        ReturnStatus = -1;
        goto Cleanup;
    }
    AcpiCompilerHostAcpiInitialized = TRUE;

    if (AcpiCompilerHostLogFile)
    {
        AcpiGbl_DebugFile = AcpiCompilerHostLogFile;
        AcpiOsRedirectOutput (AcpiCompilerHostLogFile);
    }

    AcpiCompilerHostInputName = _strdup (InputPath);
    if (!AcpiCompilerHostInputName)
    {
        ReturnStatus = -1;
        goto Cleanup;
    }

    Status = AslDoOneFile (AcpiCompilerHostInputName);
    if (ACPI_FAILURE (Status))
    {
        AcpiCompilerHostUseLogForCompilerErrors ();
    }
    if (ACPI_FAILURE (Status) && !AslGbl_ParseTreeRoot)
    {
        AcpiCompilerHostDumpErrors (LogPath);

        /*
         * AslDoOneFile returns early for parser/preprocessor errors. The
         * command-line compiler normally reaches CmCleanupAndExit from its
         * main loop, but this host calls the compiler entry point directly.
         * Run the same file/cache cleanup here so a failed invocation cannot
         * leave ACPICA globals pointing at the previous parse.
         */
        if (AslGbl_FilesList)
        {
            (void) CmCleanupAndExit ();
        }

        ReturnStatus = -1;
        goto Cleanup;
    }

    if (!Disassemble && AslGbl_ParseTreeRoot)
    {
        int BackendStatus = CmDoAslMiddleAndBackEnd ();
        if (BackendStatus != 0)
        {
            AcpiCompilerHostUseLogForCompilerErrors ();
            AcpiCompilerHostDumpErrors (LogPath);
            ReturnStatus = -1;
        }

        int CleanupStatus = CmCleanupAndExit ();
        if (CleanupStatus != 0)
        {
            ReturnStatus = -1;
        }
    }

Cleanup:
    AcpiCompilerHostAbortJump = NULL;
    free (AcpiCompilerHostInputName);
    AcpiCompilerHostInputName = NULL;
    AslParserCleanup ();
    AcpiDmClearExternalFileList ();
    PrTerminatePreprocessor ();
    AeClearErrorLog ();
    UtDeleteLocalCaches ();
    if (AcpiCompilerHostAcpiInitialized)
    {
        (void) AcpiTerminate ();
    }
    AcpiCompilerHostResetLineBuffers ();
    AslGbl_OutputFilenamePrefix = NULL;
    AslGbl_Files = NULL;
    AslGbl_FilesList = NULL;
    AcpiGbl_DisasmFlag = FALSE;

    if (AcpiCompilerHostLogFile)
    {
        fflush (AcpiCompilerHostLogFile);
        AcpiGbl_DebugFile = NULL;
        AcpiOsRedirectOutput (stdout);
        fclose (AcpiCompilerHostLogFile);
        AcpiCompilerHostLogFile = NULL;
    }

    return (int) ReturnStatus;
}

__declspec (dllexport) int __cdecl
AcpiCompilerHost_Compile (
    const char *InputPath,
    const char *LogPath)
{
    int Result;

    AcpiCompilerHostLockEnter ();
    Result = AcpiCompilerHostRun (InputPath, NULL, LogPath, FALSE);
    AcpiCompilerHostLockLeave ();
    return Result;
}

__declspec (dllexport) int __cdecl
AcpiCompilerHost_Disassemble (
    const char *InputPath,
    const char *ExternalFiles,
    const char *LogPath)
{
    int Result;

    AcpiCompilerHostLockEnter ();
    Result = AcpiCompilerHostRun (InputPath, ExternalFiles, LogPath, TRUE);
    AcpiCompilerHostLockLeave ();
    return Result;
}
