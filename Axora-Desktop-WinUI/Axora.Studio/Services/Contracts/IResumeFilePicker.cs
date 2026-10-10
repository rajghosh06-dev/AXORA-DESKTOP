namespace Axora.Studio.Services.Contracts;
public interface IResumeFilePicker
{
    Task<string?> SelectImportAsync(nint owner);
}
