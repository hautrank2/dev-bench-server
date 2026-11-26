namespace DevBench.Api.Models.Dtos;

public sealed record LoginStart(string? Challenge, string? DeviceId, string? DeviceName);
