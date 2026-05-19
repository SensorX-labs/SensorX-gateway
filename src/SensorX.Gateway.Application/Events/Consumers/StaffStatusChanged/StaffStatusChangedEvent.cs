using System;
using MassTransit;
using SensorX.Gateway.Domain.Enums;

namespace SensorX.Gateway.Application.Events.Consumers.StaffStatusChanged;

[MessageUrn("staff-status-changed")]
[EntityName("staff-status-changed")]
public sealed record StaffStatusChangedEvent(
    Guid Id,
    Guid AccountId,
    StaffStatus Status
);
