using System;
using MassTransit;

namespace SensorX.Gateway.Application.Events.Consumers.CustomerAvatarUpdated;

[MessageUrn("customer-avatar-updated")]
[EntityName("customer-avatar-updated")]
public sealed record CustomerAvatarUpdatedEvent(
    Guid AccountId,
    string AvatarUrl
);