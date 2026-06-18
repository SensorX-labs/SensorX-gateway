using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using SensorX.Gateway.Application.Interfaces;
using SensorX.Gateway.Domain.Enums;
using SensorX.Gateway.Domain.Interfaces.Repositories;

namespace SensorX.Gateway.Application.Events.Consumers.SendEmail;

public class SendEmailConsumer(
    IAccountRepository accountRepository,
    IEmailSender emailSender,
    ILogger<SendEmailConsumer> logger
) : IConsumer<SendEmailCommand>
{
    public async Task Consume(ConsumeContext<SendEmailCommand> context)
    {
        var msg = context.Message;

        if (!string.IsNullOrEmpty(msg.Role))
        {
            if (Enum.TryParse<Role>(msg.Role, true, out var roleEnum))
            {
                logger.LogInformation("Gateway: Gửi email tới nhóm Role {Role} - {Subject}", msg.Role, msg.Subject);
                try
                {
                    var (accounts, _) = await accountRepository.GetPagedAsync(
                        pageNumber: 1,
                        pageSize: 1000,
                        searchTerm: null,
                        email: null,
                        fullName: null,
                        role: roleEnum,
                        isLocked: false,
                        warehouseId: null,
                        createdFrom: null,
                        createdTo: null
                    );

                    foreach (var account in accounts)
                    {
                        if (!string.IsNullOrEmpty(account.Email))
                        {
                            try
                            {
                                await emailSender.SendAsync(account.Email, msg.Subject, msg.HtmlBody);
                            }
                            catch (Exception ex)
                            {
                                logger.LogError(ex, "Gateway: Lỗi khi gửi email tới {Email}", account.Email);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Gateway: Lỗi khi lấy danh sách tài khoản theo Role {Role}", msg.Role);
                }
            }
            else
            {
                logger.LogWarning("Gateway: Không tìm thấy Role hợp lệ: {Role}", msg.Role);
            }
        }
        else if (!string.IsNullOrEmpty(msg.To))
        {
            logger.LogInformation("Gateway: Gửi email tới {To} - {Subject}", msg.To, msg.Subject);
            try
            {
                await emailSender.SendAsync(msg.To, msg.Subject, msg.HtmlBody);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Gateway: Lỗi khi gửi email tới {To}", msg.To);
            }
        }
    }
}
