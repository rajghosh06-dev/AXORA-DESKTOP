namespace Axora.Desktop.Services.Contracts;

/// <summary>
/// Migration contract for versioned Scholar Kit persistence schemas.
/// Dispatches version transitions when legacy formats are loaded.
/// </summary>
public interface IScholarPersistenceMigrator
{
    int TargetSchemaVersion { get; }
    string MigrateJson(string json, int sourceVersion, string entityType);
}
