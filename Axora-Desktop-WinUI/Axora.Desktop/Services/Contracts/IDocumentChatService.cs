using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Axora.Desktop.Services.Contracts;

public sealed class DocumentChatResult
{
    public string Answer { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public IReadOnlyList<string> CitedPassages { get; set; } = [];
}

public interface IDocumentChatService
{
    Task IndexDocumentAsync(string documentText, CancellationToken ct = default);
    Task<DocumentChatResult> QueryDocumentAsync(string userQuery, CancellationToken ct = default);
    bool HasIndexedDocument { get; }
}
