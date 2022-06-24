using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MRochon.Extensions
{
    public interface IEmailService
    {
        Task<bool> SendAsync(string emailAddress, string displayName, string htmlBody);
    }

    public class EmailOptions
    {
        public string? Secret { get; set; }
        public string? FromEmail { get; set; }
        public string? FromName { get; set; }
        public string? Subject { get; set; }
    }

    public class SendGridService : IEmailService
    {
        private readonly ILogger<SendGridService> _logger;
        private readonly IOptions<EmailOptions> _options;
        private SendGridClient _mailer;
        public SendGridService(ILogger<SendGridService> logger,
            IOptions<EmailOptions> options)
        {
            _logger = logger;
            _options = options;
            _mailer = new SendGridClient(options.Value.Secret);
        }

        public async Task<bool> SendAsync(string emailAddress, string displayName, string htmlBody)
        {
            var from = new EmailAddress(_options.Value.FromEmail, _options.Value.FromName);
            var to = new EmailAddress(emailAddress, displayName);
            var msg = MailHelper.CreateSingleEmail(from, to, _options.Value.Subject, String.Empty, htmlBody);
            var response = await _mailer.SendEmailAsync(msg).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Email sent to {emailAddress}");
                return true;
            }
            else
            {
                _logger.LogError($"Email send to {emailAddress} failed with: {await response.Body.ReadAsStringAsync()}");
                return false;
            }
        }
    }
}
