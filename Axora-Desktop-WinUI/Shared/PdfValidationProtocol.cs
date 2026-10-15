using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Axora.PdfValidation;

// Foundation protocol only. Neither status confers structural/font/content validity.
// Linked source: the worker has no dependency on Studio or its UI assemblies.
internal static class PdfValidationProtocol
{
    internal const ushort Version = 1, PolicyVersion = 1;
    internal const int PdfLimit = 16 * 1024 * 1024, RequestHeader = 144, ReplyBody = 192;
    internal const int ReplyLimit = ReplyBody + 4, PageLimit = 64, RasterLimit = 2 * 1024 * 1024;
    internal static string PolicySha256 { get; } = Hash("AXORA.PdfValidation.Foundation.1:NoPdfValidityOrPromotion"u8);

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    internal static bool IsHash(string value) => value.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'A' and <= 'F');
    private static InvalidDataException Invalid() => new("PdfFoundationProtocolRejected");

    internal static byte[] EncodeRequest(PdfFoundationRequest request)
    {
        byte[] pdf = request.CopyPdf();
        try
        {
            byte[] frame = new byte[4 + RequestHeader + pdf.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(frame, checked((uint)(RequestHeader + pdf.Length)));
            Span<byte> h = frame.AsSpan(4, RequestHeader);
            "AXPFREQ1"u8.CopyTo(h);
            BinaryPrimitives.WriteUInt16LittleEndian(h[8..], Version);
            BinaryPrimitives.WriteUInt16LittleEndian(h[10..], PolicyVersion);
            request.RequestId.TryWriteBytes(h[12..28], bigEndian: true, out _);
            request.OperationId.TryWriteBytes(h[28..44], bigEndian: true, out _);
            BinaryPrimitives.WriteUInt32LittleEndian(h[44..], (uint)pdf.Length);
            Convert.FromHexString(request.PdfSha256).CopyTo(h[48..80]);
            Convert.FromHexString(request.InputIdentitySha256).CopyTo(h[80..112]);
            Convert.FromHexString(request.BundleSha256).CopyTo(h[112..144]);
            pdf.CopyTo(frame, 4 + RequestHeader);
            return frame;
        }
        finally { Array.Clear(pdf); }
    }

    internal static PdfFoundationRequest ReadRequest(Stream input)
    {
        byte[] prefix = new byte[4], header = new byte[RequestHeader];
        input.ReadExactly(prefix);
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(prefix);
        if (size < RequestHeader + 1 || size > RequestHeader + (uint)PdfLimit) throw Invalid();
        input.ReadExactly(header);
        ReadOnlySpan<byte> h = header;
        if (!h[..8].SequenceEqual("AXPFREQ1"u8) || BinaryPrimitives.ReadUInt16LittleEndian(h[8..]) != Version ||
            BinaryPrimitives.ReadUInt16LittleEndian(h[10..]) != PolicyVersion) throw Invalid();
        Guid request = new(h[12..28], bigEndian: true), operation = new(h[28..44], bigEndian: true);
        uint pdfSize = BinaryPrimitives.ReadUInt32LittleEndian(h[44..]);
        if (request == Guid.Empty || operation == Guid.Empty || pdfSize is < 1 or > PdfLimit ||
            (ulong)RequestHeader + pdfSize != size) throw Invalid();
        byte[] pdf = new byte[checked((int)pdfSize)];
        try
        {
            input.ReadExactly(pdf);
            if (input.ReadByte() != -1 || !CryptographicOperations.FixedTimeEquals(h[48..80], SHA256.HashData(pdf))) throw Invalid();
            return new(request, operation, Convert.ToHexString(h[80..112]), Convert.ToHexString(h[112..144]), pdf);
        }
        finally { Array.Clear(pdf); }
    }

    internal static byte[] EncodeReply(PdfFoundationReply reply)
    {
        RequireReplyShape(reply);
        byte[] frame = new byte[ReplyLimit];
        BinaryPrimitives.WriteUInt32LittleEndian(frame, ReplyBody);
        Span<byte> h = frame.AsSpan(4);
        "AXPFRES1"u8.CopyTo(h);
        BinaryPrimitives.WriteUInt16LittleEndian(h[8..], Version);
        BinaryPrimitives.WriteUInt16LittleEndian(h[10..], PolicyVersion);
        reply.RequestId.TryWriteBytes(h[12..28], bigEndian: true, out _);
        reply.OperationId.TryWriteBytes(h[28..44], bigEndian: true, out _);
        BinaryPrimitives.WriteUInt16LittleEndian(h[44..], (ushort)reply.Status);
        BinaryPrimitives.WriteUInt16LittleEndian(h[46..], (ushort)reply.Code);
        BinaryPrimitives.WriteInt32LittleEndian(h[48..], reply.ParserPages);
        BinaryPrimitives.WriteInt32LittleEndian(h[52..], reply.NativePages);
        BinaryPrimitives.WriteInt64LittleEndian(h[56..], reply.RasterBytes);
        Convert.FromHexString(reply.RequestSha256).CopyTo(h[64..96]);
        Convert.FromHexString(reply.PdfSha256).CopyTo(h[96..128]);
        Convert.FromHexString(reply.InputIdentitySha256).CopyTo(h[128..160]);
        Convert.FromHexString(reply.BundleSha256).CopyTo(h[160..192]);
        return frame;
    }

    internal static PdfFoundationReply ReadReply(Stream input)
    {
        byte[] prefix = new byte[4], body = new byte[ReplyBody];
        input.ReadExactly(prefix);
        if (BinaryPrimitives.ReadUInt32LittleEndian(prefix) != ReplyBody) throw Invalid();
        input.ReadExactly(body);
        if (input.ReadByte() != -1) throw Invalid();
        ReadOnlySpan<byte> h = body;
        if (!h[..8].SequenceEqual("AXPFRES1"u8) || BinaryPrimitives.ReadUInt16LittleEndian(h[8..]) != Version ||
            BinaryPrimitives.ReadUInt16LittleEndian(h[10..]) != PolicyVersion) throw Invalid();
        var reply = new PdfFoundationReply(new(h[12..28], bigEndian: true), new(h[28..44], bigEndian: true),
            (PdfFoundationStatus)BinaryPrimitives.ReadUInt16LittleEndian(h[44..]), (PdfFoundationCode)BinaryPrimitives.ReadUInt16LittleEndian(h[46..]),
            BinaryPrimitives.ReadInt32LittleEndian(h[48..]), BinaryPrimitives.ReadInt32LittleEndian(h[52..]), BinaryPrimitives.ReadInt64LittleEndian(h[56..]),
            Convert.ToHexString(h[64..96]), Convert.ToHexString(h[96..128]), Convert.ToHexString(h[128..160]), Convert.ToHexString(h[160..192]));
        RequireReplyShape(reply);
        return reply;
    }

    internal static void RequireReplyIdentity(PdfFoundationReply reply, PdfFoundationRequest request, string frameSha256)
    {
        RequireReplyShape(reply);
        if (reply.RequestId != request.RequestId || reply.OperationId != request.OperationId || reply.RequestSha256 != frameSha256 ||
            reply.PdfSha256 != request.PdfSha256 || reply.InputIdentitySha256 != request.InputIdentitySha256 || reply.BundleSha256 != request.BundleSha256) throw Invalid();
    }

    private static void RequireReplyShape(PdfFoundationReply r)
    {
        if (r.RequestId == Guid.Empty || r.OperationId == Guid.Empty || !Enum.IsDefined(r.Status) || !Enum.IsDefined(r.Code) ||
            r.ParserPages is < 0 or > PageLimit || r.NativePages is < 0 or > PageLimit || r.RasterBytes is < 0 or > RasterLimit ||
            !IsHash(r.RequestSha256) || !IsHash(r.PdfSha256) || !IsHash(r.InputIdentitySha256) || !IsHash(r.BundleSha256) ||
            (r.Status == PdfFoundationStatus.FoundationObserved) != (r.Code == PdfFoundationCode.None) ||
            r.Status == PdfFoundationStatus.FoundationObserved && (r.ParserPages == 0 || r.NativePages == 0 || r.RasterBytes == 0)) throw Invalid();
    }
}

internal enum PdfFoundationStatus : ushort { FoundationObserved = 1, DependencyRejected = 2 }
internal enum PdfFoundationCode : ushort { None, ParserUnavailable, NativeUnavailable, ProbeLimit }
internal sealed record PdfFoundationReply(Guid RequestId, Guid OperationId, PdfFoundationStatus Status, PdfFoundationCode Code,
    int ParserPages, int NativePages, long RasterBytes, string RequestSha256, string PdfSha256, string InputIdentitySha256, string BundleSha256);

internal sealed class PdfFoundationRequest
{
    private readonly byte[] pdf;
    internal Guid RequestId { get; }
    internal Guid OperationId { get; }
    internal string InputIdentitySha256 { get; }
    internal string BundleSha256 { get; }
    internal string PdfSha256 { get; }
    internal PdfFoundationRequest(Guid request, Guid operation, string identity, string bundle, ReadOnlySpan<byte> bytes)
    {
        if (request == Guid.Empty || operation == Guid.Empty || !PdfValidationProtocol.IsHash(identity) ||
            !PdfValidationProtocol.IsHash(bundle) || bytes.Length is < 1 or > PdfValidationProtocol.PdfLimit)
            throw new InvalidDataException("PdfFoundationRequestRejected");
        RequestId = request; OperationId = operation; InputIdentitySha256 = identity; BundleSha256 = bundle;
        pdf = bytes.ToArray(); PdfSha256 = PdfValidationProtocol.Hash(pdf);
    }
    internal byte[] CopyPdf() => (byte[])pdf.Clone();
    internal void Clear() => Array.Clear(pdf);
}
