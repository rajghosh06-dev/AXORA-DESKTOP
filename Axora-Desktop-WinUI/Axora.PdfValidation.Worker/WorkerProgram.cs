using System.Runtime.InteropServices;
using Axora.PdfValidation;
using UglyToad.PdfPig;

namespace Axora.PdfValidation.Worker;

internal static class WorkerProgram
{
    [MTAThread]
    private static int Main(string[] args)
    {
        // No modes, paths, child execution, environment options or diagnostic payloads.
        if (args.Length != 0) return 2;
        int hr = RoInitialize(1);
        if (hr < 0) return 3;
        try { return RunAsync().GetAwaiter().GetResult(); }
        catch (Exception) { return 4; } // Never log PDF bytes, text or raw parser exceptions.
        finally { RoUninitialize(); }
    }

    private static async Task<int> RunAsync()
    {
        using var input = Console.OpenStandardInput();
        var request = PdfValidationProtocol.ReadRequest(input);
        byte[] pdf = request.CopyPdf(), encoded = PdfValidationProtocol.EncodeRequest(request);
        int parserPages = 0, nativePages = 0;
        long rasterBytes = 0;
        PdfFoundationCode code = PdfFoundationCode.None;
        try
        {
            // Library/API integration observations, not B2C2 structural policy or B2C3 fidelity.
            try
            {
                using var parsed = UglyToad.PdfPig.PdfDocument.Open(pdf,
                    new ParsingOptions { UseLenientParsing = false, SkipMissingFonts = false, MaxStackDepth = 64 });
                parserPages = parsed.NumberOfPages;
                if (parserPages is < 1 or > PdfValidationProtocol.PageLimit) { parserPages = 0; code = PdfFoundationCode.ProbeLimit; }
            }
            catch (Exception) { code = PdfFoundationCode.ParserUnavailable; }
            try
            {
                using var bytes = new MemoryStream(pdf, writable: false);
                using var random = bytes.AsRandomAccessStream();
                var native = await Windows.Data.Pdf.PdfDocument.LoadFromStreamAsync(random);
                if (native.PageCount is < 1 or > PdfValidationProtocol.PageLimit) code = PdfFoundationCode.ProbeLimit;
                else
                {
                    nativePages = (int)native.PageCount;
                    // One bounded dependency probe raster. No all-page/visibility safety assertion.
                    using var page = native.GetPage(0);
                    using var sink = new BoundedProbeStream();
                    using var raster = sink.AsRandomAccessStream();
                    await page.RenderToStreamAsync(raster, new Windows.Data.Pdf.PdfPageRenderOptions { DestinationWidth = 256, DestinationHeight = 362 });
                    rasterBytes = sink.Length;
                }
            }
            catch (Exception) { code = PdfFoundationCode.NativeUnavailable; }
            var reply = new PdfFoundationReply(request.RequestId, request.OperationId,
                code == PdfFoundationCode.None ? PdfFoundationStatus.FoundationObserved : PdfFoundationStatus.DependencyRejected,
                code, parserPages, nativePages, rasterBytes, PdfValidationProtocol.Hash(encoded), request.PdfSha256,
                request.InputIdentitySha256, request.BundleSha256);
            byte[] frame = PdfValidationProtocol.EncodeReply(reply);
            using var output = Console.OpenStandardOutput();
            await output.WriteAsync(frame);
            await output.FlushAsync();
            return 0;
        }
        finally { Array.Clear(pdf); Array.Clear(encoded); request.Clear(); }
    }

    private sealed class BoundedProbeStream : MemoryStream
    {
        private void Require(int count)
        { if (count < 0 || Position < 0 || Position > PdfValidationProtocol.RasterLimit - (long)count) throw new IOException("ProbeRasterLimit"); }
        public override void Write(byte[] buffer, int offset, int count) { Require(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Require(buffer.Length); base.Write(buffer); }
        public override void WriteByte(byte value) { Require(1); base.WriteByte(value); }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
        { Require(count); return base.WriteAsync(buffer, offset, count, token); }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
        { Require(buffer.Length); return base.WriteAsync(buffer, token); }
        public override void SetLength(long value)
        { if (value is < 0 or > PdfValidationProtocol.RasterLimit) throw new IOException("ProbeRasterLimit"); base.SetLength(value); }
    }
    [DllImport("combase.dll")] private static extern int RoInitialize(uint type);
    [DllImport("combase.dll")] private static extern void RoUninitialize();
}
