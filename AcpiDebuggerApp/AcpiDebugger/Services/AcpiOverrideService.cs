namespace AcpiDebugger.Services;

public sealed class AcpiOverrideService
{
    private readonly string _toolsDirectory;

    public AcpiOverrideService(string toolsDirectory)
    {
        _toolsDirectory = toolsDirectory;
    }

    public bool IsTestSigningEnabled() => AcpiLoader.IsTestSigningEnabled();

    public Task<string> StageAsync(string amlFilePath, CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () => AcpiLoader.LoadAcpiTable(amlFilePath, _toolsDirectory),
            cancellationToken);
    }
}
