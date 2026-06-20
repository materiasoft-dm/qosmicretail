using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Configuration;
using MimeKit;

namespace Mercurius
{
    public class EmailSender : IEmailSender
    {
        private readonly string _from;
        private readonly string _user;
        private readonly string _password;
        private readonly string _host;
        private readonly int _port;
        private readonly bool _enableSsl;

        public EmailSender(IConfiguration configuration)
        {
            _from = configuration["Smtp:From"]!;
            _user = configuration["Smtp:User"]!;
            _password = configuration["Smtp:Password"]!;
            _host = configuration["Smtp:Host"] ?? "smtp.gmail.com";

            if (!int.TryParse(configuration["Smtp:Port"], out _port))
            {
                _port = 587;
            }

            if (!bool.TryParse(configuration["Smtp:EnableSsl"], out _enableSsl))
            {
                _enableSsl = true;
            }
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_from) || string.IsNullOrWhiteSpace(_user) || string.IsNullOrWhiteSpace(_password))
            {
                throw new InvalidOperationException("SMTP configuration is missing. Configure Smtp:From, Smtp:User and Smtp:Password in configuration or environment variables.");
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_from, _from));
            message.Subject = subject;
            message.To.Add(new MailboxAddress(email, email));

            var bodyBuilder = new BodyBuilder
            {
                HtmlBody = $"<html><body>{htmlMessage}</body></html>"
            };
            message.Body = bodyBuilder.ToMessageBody();

            using var smtpClient = new SmtpClient();
            
            var secureSocketOptions = _enableSsl 
                ? SecureSocketOptions.StartTls 
                : SecureSocketOptions.None;
            
            await smtpClient.ConnectAsync(_host, _port, secureSocketOptions, cancellationToken);
            
            if (!string.IsNullOrEmpty(_user) && !string.IsNullOrEmpty(_password))
            {
                await smtpClient.AuthenticateAsync(_user, _password, cancellationToken);
            }
            
            await smtpClient.SendAsync(message, cancellationToken);
            await smtpClient.DisconnectAsync(true, cancellationToken);
        }
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            await SendEmailAsync(email, subject, htmlMessage, CancellationToken.None);
        }
    }
}