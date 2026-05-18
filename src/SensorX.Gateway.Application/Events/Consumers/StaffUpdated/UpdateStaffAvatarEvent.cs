using System;
using MassTransit;

namespace SensorX.Gateway.Application.Events.Consumers.StaffUpdated;

[MessageUrn("staff-avatar-updated")]
[EntityName("staff-avatar-updated")]
public sealed record UpdateStaffAvatarEvent(
    Guid Id,
    Guid AccountId,
    string AvatarUrl
);