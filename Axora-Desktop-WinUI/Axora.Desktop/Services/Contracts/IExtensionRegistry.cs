using System;
using System.Collections.Generic;
using Axora.Desktop.Models;

namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Registry containing all registered external extensions, components, and optional tools.
/// </summary>
public interface IExtensionRegistry
{
    IReadOnlyList<ExtensionModel> GetAll();
    ExtensionModel? GetById(string id);
    void Register(ExtensionModel extension);
    bool Unregister(string id);
    event EventHandler<ExtensionModel>? ExtensionRegistered;
    event EventHandler<ExtensionModel>? ExtensionUpdated;
}