using System;
using MassTransit;
using SensorX.Gateway.Domain.Enums;

namespace SensorX.Gateway.Application.Events.Consumers.StaffUpdated;

public enum Department
{
    Sale,
    Warehouse,
    Manager,
}

[MessageUrn("staff-updated")]
[EntityName("staff-updated")]
public sealed record UpdateStaffEvent(
    Guid Id,
    string Name,
    string? Phone,
    string Email,
    string? CitizenId,
    string? Biography,
    DateTimeOffset JoinDate,
    Department Department,
    StaffStatus Status
);
