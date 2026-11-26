namespace DevBench.Api.Models.Dtos;

public sealed record BackupInfo(string Id, string Name, string SourceDevice, string Platform,
    int ProjectCount, int RepositoryCount, string CreatedAt);
