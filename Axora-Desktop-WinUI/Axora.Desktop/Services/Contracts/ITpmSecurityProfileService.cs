using System;
using System.Threading;
using System.Threading.Tasks;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Hardware-bound TPM 2.0 and Windows Data Protection API (DPAPI-NG) enterprise security profile service.
/// Seals master cryptographic keys to physical hardware root-of-trust boundaries.
/// </summary>
public interface ITpmSecurityProfileService
{
    /// <summary>
    /// Probes and returns the active hardware security profile and TPM status.
    /// </summary>
    Task<TpmSecurityProfile> GetHardwareSecurityProfileAsync();

    /// <summary>
    /// Seals raw key material to the hardware boundary using the requested protection scope.
    /// </summary>
    Task<byte[]> SealKeyToHardwareAsync(byte[] rawKeyMaterial, HardwareProtectionScope scope, CancellationToken ct = default);

    /// <summary>
    /// Unseals a hardware-protected cryptographic blob back to raw key material.
    /// </summary>
    Task<byte[]> UnsealKeyFromHardwareAsync(byte[] sealedBlob, CancellationToken ct = default);

    /// <summary>
    /// Performs a cryptographic hardware attestation check verifying the platform TPM boundary.
    /// </summary>
    Task<bool> VerifyTpmAttestationAsync(CancellationToken ct = default);
}
