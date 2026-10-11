using System.Security.Cryptography;
using System.Text;
using Axora.Studio.Models.Rendering;

namespace Axora.Studio.Services.Rendering;

internal static class TrustedRendererFixture
{
    internal const int Version = 1;
    internal static byte[] Bytes(RendererFixture fixture) => Encoding.UTF8.GetBytes(fixture switch
    {
        RendererFixture.Ascii => "#set page(width: 210mm, height: 297mm)\n= AXORA synthetic fixture\nASCII process foundation\n- one\n- two\nURL text: https://example.invalid/\n",
        RendererFixture.UnicodeProbe => "#set page(width: 210mm, height: 297mm)\n= AXORA synthetic fixture\nUnicode: café Ω\n- one\n- two\nURL text: https://example.invalid/\nHindi process probe: नमस्ते\n",
        _ => throw new InvalidDataException("Unknown fixture")
    });
    internal static string Hash(RendererFixture fixture) => Convert.ToHexString(SHA256.HashData(Bytes(fixture)));
}
