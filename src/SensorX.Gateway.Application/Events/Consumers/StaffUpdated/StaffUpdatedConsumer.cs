using System.Threading.Tasks;
using MassTransit;
using SensorX.Gateway.Domain.Interfaces.Repositories;

namespace SensorX.Gateway.Application.Events.Consumers.StaffUpdated;

public class StaffUpdatedConsumer(
    IAccountRepository _accountRepository,
    IUnitOfWork _unitOfWork
) : IConsumer<UpdateStaffEvent>
{
    public async Task Consume(ConsumeContext<UpdateStaffEvent> context)
    {
        var message = context.Message;
        var account = await _accountRepository.GetByEmailAsync(message.Email);
        if (account == null) return;

        account.UpdateProfile(message.Name, account.AvatarUrl);

        await _unitOfWork.SaveChangesAsync(context.CancellationToken);
    }
}
