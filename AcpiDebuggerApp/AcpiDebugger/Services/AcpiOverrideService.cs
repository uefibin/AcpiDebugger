namespace AcpiDebugger.Services;

public sealed class AcpiOverrideService
{
    public bool IsTestSigningEnabled() => AcpiLoader.IsTestSigningEnabled();

    public Task<string> StageAsync(string amlFilePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => AcpiLoader.LoadAcpiTable(amlFilePath),
            cancellationToken);
    }

    public Task<string> RemoveAsync(
        string amlFilePath,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => AcpiLoader.RemoveAcpiTable(amlFilePath, cancellationToken),
            cancellationToken);
    }

    public Task<string> RemoveAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => AcpiLoader.RemoveAllAcpiOverrides(cancellationToken),
            cancellationToken);
    }
}
