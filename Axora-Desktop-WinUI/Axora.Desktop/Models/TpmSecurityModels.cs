using System;

namespace Axora.Desktop.Models;

public enum HardwareProtectionScope
{
    /// <summary>Protects key material to current Windows user account (LOCAL=user).</summary>
    UserAccount,

    /// <summary>Protects key material to the local physical machine TPM/DPAPI boundary (LOCAL=machine).</summary>
    LocalMachine
}

public sealed class TpmSecurityProfile
{
    public bool IsHardwareTpmDetected { get; set; } = true;
    public string AttestationStatus { get; set; } = "Hardware Root of Trust Verified";
    public string ProtectionDescriptor { get; set; } = "LOCAL=user";
    public string TpmSpecificationVersion { get; set; } = "2.0";
    public string ManufacturerName { get; set; } = "Microsoft Virtual / Intel PTT / AMD fTPM";
    public bool IsMasterKeySealed { get; set; }
    public DateTimeOffset? SealedTimestamp { get; set; }
    public string KeyFingerprint { get; set; } = string.Empty;
}
