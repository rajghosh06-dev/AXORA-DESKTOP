namespace Axora.Desktop.Models;

/// <summary>
/// Output destination file collision resolution policy for the Universal Converter.
/// </summary>
public enum CollisionPolicy
{
    /// <summary>Automatically append a numeric index, e.g. "document (1).pdf". Safe default.</summary>
    AutoRename,

    /// <summary>Explicitly overwrite the existing destination file.</summary>
    Overwrite,

    /// <summary>Skip conversion if the target destination file already exists.</summary>
    Skip,

    /// <summary>Prompt the user interactively before proceeding with conversion.</summary>
    Prompt
}
