using System.Security.Cryptography;

namespace Axora.Studio.Models.ResumeRendering;

public sealed class ResumeRenderSnapshot
{
    private readonly byte[] semanticBytes;
    private readonly byte[] snapshotBytes;
    public int SnapshotSchemaVersion => ResumeRenderLimits.SnapshotSchemaVersion;
    public int DerivationVersion => ResumeRenderLimits.DerivationVersion;
    public string DocumentId { get; }
    public long Revision { get; }
    public ResumeSnapshotDocument Document { get; }
    public string SemanticSha256 { get; }
    public string SnapshotSha256 { get; }
    public int SemanticByteLength => semanticBytes.Length;
    public int SnapshotByteLength => snapshotBytes.Length;

    internal ResumeRenderSnapshot(string documentId, long revision, ResumeSnapshotDocument document, byte[] semantic, byte[] snapshot)
    {
        DocumentId = documentId;
        Revision = revision;
        Document = document;
        semanticBytes = semantic;
        snapshotBytes = snapshot;
        SemanticSha256 = Convert.ToHexString(SHA256.HashData(semanticBytes));
        SnapshotSha256 = Convert.ToHexString(SHA256.HashData(snapshotBytes));
    }
    public byte[] CopySemanticBytes() => (byte[])semanticBytes.Clone();
    public byte[] CopySnapshotBytes() => (byte[])snapshotBytes.Clone();
}
