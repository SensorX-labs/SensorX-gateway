using System.Threading.Tasks;
using MassTransit;
using SensorX.Gateway.Domain.Enums;
using SensorX.Gateway.Domain.Interfaces.Repositories;

namespace SensorX.Gateway.Application.Events.Consumers.StaffStatusChanged;

public class StaffStatusChangedConsumer(
    IAccountRepository _accountRepository,
    IUnitOfWork _unitOfWork
) : IConsumer<StaffStatusChangedEvent>
{
    public async Task Consume(ConsumeContext<StaffStatusChangedEvent> context)
    {
        var message = context.Message;
        var account = await _accountRepository.GetByIdAsync(message.AccountId);
        if (account == null) return;

        if (message.Status == StaffStatus.Resigned)
        {
            account.LockAccount();
        }
        else
        {
            account.UnlockAccount();
        }

        await _unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}
