using System.Threading.Tasks;
using MassTransit;
using SensorX.Gateway.Domain.Interfaces.Repositories;
using SensorX.Gateway.Domain.Interfaces;

namespace SensorX.Gateway.Application.Events.Consumers.StaffUpdated;

public class StaffAvatarUpdatedConsumer(
    IAccountRepository _accountRepository,
    IUnitOfWork _unitOfWork
) : IConsumer<UpdateStaffAvatarEvent>
{
    public async Task Consume(ConsumeContext<UpdateStaffAvatarEvent> context)
    {
        var message = context.Message;
        var account = await _accountRepository.GetByIdAsync(message.AccountId);
        if (account == null) return;

        account.UpdateProfile(account.FullName, message.AvatarUrl);
        await _unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}