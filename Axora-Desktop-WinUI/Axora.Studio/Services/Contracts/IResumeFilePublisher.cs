using Axora.Studio.Models;
namespace Axora.Studio.Services.Contracts;
public interface IResumeFilePublisher
{
    Task PreserveImportAsync(ReadOnlyMemory<byte> capturedBytes, string sha256);
    Task<ResumePublished> PublishAsync(ResumePublication publication);
}
