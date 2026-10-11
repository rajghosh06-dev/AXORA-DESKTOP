using Axora.Studio.Models.Rendering;
namespace Axora.Studio.Services.Rendering;

public sealed class StudioRendererSession(Func<RendererCoordinator> factory)
{
    private readonly object _gate = new();
    private RendererCoordinator? _coordinator;
    private Task? _shutdown;
    public bool IsCreated { get { lock (_gate) return _coordinator is not null; } }
    public Task<RendererResult> RenderAsync(RendererRequest request, CancellationToken token = default)
    {
        lock (_gate)
        {
            if (_shutdown is not null) return Task.FromResult(new RendererResult(request.OperationId, RendererOutcome.AdmissionClosed, "AdmissionClosed"));
            return (_coordinator ??= factory()).RenderAsync(request, token);
        }
    }
    public Task ShutdownAsync()
    {
        lock (_gate) return _shutdown ??= _coordinator?.ShutdownAsync() ?? Task.CompletedTask;
    }
}
