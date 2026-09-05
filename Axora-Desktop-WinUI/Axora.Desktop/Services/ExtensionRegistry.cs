using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Thread-safe in-memory extension registry seeded with official external dependencies.
/// </summary>
public sealed class ExtensionRegistry : IExtensionRegistry
{
    private readonly ConcurrentDictionary<string, ExtensionModel> _extensions = new(StringComparer.OrdinalIgnoreCase);

    public event EventHandler<ExtensionModel>? ExtensionRegistered;
    public event EventHandler<ExtensionModel>? ExtensionUpdated;

    public ExtensionRegistry()
    {
        SeedDefaultExtensions();
    }

    private void SeedDefaultExtensions()
    {
        // 1. ImageMagick Q16-HDRI — First real extension (Section 8)
        var imageMagick = new ExtensionModel
        {
            Id = "imagemagick",
            DisplayName = "ImageMagick Q16-HDRI",
            Description = "Industrial-grade image manipulation engine. Enables direct extent sizing, 16-bit HDRI depth, lossless WebP, SVG rasterization, and AVX2 multi-core acceleration.",
            TargetVersion = "7.1.1-43",
            LatestVersion = "7.1.1-43",
            RequiredBy = ["Batch Image Studio", "Universal Converter (W2)"],
            IsRequired = false, // Optional component: WIC hardware fallback is always active
            InstallSource = "https://imagemagick.org/archive/binaries/ImageMagick-7.1.1-43-Q16-HDRI-x64-dll.exe",
            DetectionStrategy = "PathAndManagedDirectoryProbe",
            ExecutableName = "magick.exe",
            CapabilityInformation = "Direct Extent, 16-bit HDRI, Lossless WebP, SVG, Multi-threaded AVX2",
            SupportedArchitecture = "x64",
            SupportedOs = "Windows 10/11 (64-bit)",
            CanRepair = true,
            CanReinstall = true,
            CanCleanReinstall = true
        };
        Register(imageMagick);
    }

    public IReadOnlyList<ExtensionModel> GetAll()
    {
        return _extensions.Values.OrderBy(e => e.DisplayName).ToList();
    }

    public ExtensionModel? GetById(string id)
    {
        return _extensions.TryGetValue(id, out var ext) ? ext : null;
    }

    public void Register(ExtensionModel extension)
    {
        ArgumentNullException.ThrowIfNull(extension);
        _extensions[extension.Id] = extension;
        ExtensionRegistered?.Invoke(this, extension);
    }

    public bool Unregister(string id)
    {
        return _extensions.TryRemove(id, out _);
    }

    public void NotifyUpdated(ExtensionModel extension)
    {
        ExtensionUpdated?.Invoke(this, extension);
    }
}