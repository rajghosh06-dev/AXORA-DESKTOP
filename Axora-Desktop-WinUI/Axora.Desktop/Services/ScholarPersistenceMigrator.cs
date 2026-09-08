using System;
using Axora.Desktop.Models;
using Axora.Desktop.Services.Contracts;

namespace Axora.Desktop.Services;

/// <summary>
/// Default schema version migrator for Scholar Kit persistence.
/// For Schema Version 1, validates version parity and guards against unsupported future schemas.
/// </summary>
public sealed class ScholarPersistenceMigrator : IScholarPersistenceMigrator
{
    public int TargetSchemaVersion => ScholarPersistenceConstants.CurrentSchemaVersion;

    public string MigrateJson(string json, int sourceVersion, string entityType)
    {
        if (sourceVersion == TargetSchemaVersion)
        {
            return json;
        }

        if (sourceVersion > TargetSchemaVersion)
        {
            throw new UnsupportedSchemaVersionException(sourceVersion, TargetSchemaVersion);
        }

        // Migration step dispatch: future phases can add:
        // if (sourceVersion == 1 && TargetSchemaVersion == 2) { json = MigrateV1ToV2(json); }
        return json;
    }
}
