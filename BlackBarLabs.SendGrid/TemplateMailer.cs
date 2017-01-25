using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlackBarLabs.Web;
using SendGrid;
using System.Net.Mail;
using System.Net;
using Exceptions;
using BlackBarLabs.Extensions;

namespace BlackBarLabs.SendGrid
{
    public class TemplateMailer : EastFive.Api.Services.ISendMessageService
    {
        private readonly string username;
        private readonly string password;

        public TemplateMailer(string username, string password)
        {
            this.username = username;
            this.password = password;
        }

        public async Task<TResult> SendEmailMessageAsync<TResult>(
            string toAddress, string toName, 
            string fromAddress, string fromName,
            string templateName, 
            IDictionary<string, string> substitutionsSingle,
            IDictionary<string, string[]> substitutionsMultiple, 
            Func<string, TResult> onSuccess, 
            Func<TResult> onServiceUnavailable,
            Func<string, TResult> onFailed)
        {
            var subject = substitutionsSingle.ContainsKey("subject") ?
                substitutionsSingle["subject"]
                :
                "";

            var myMessage = new SendGridMessage();
            myMessage.AddTo(toAddress);
            myMessage.From = new MailAddress(fromAddress, fromName);
            myMessage.Subject = subject;
            myMessage.EnableTemplateEngine(templateName);
            myMessage.Text = "asdf";
            myMessage.Html = "<html></html>";

            if (default(IDictionary<string, string>) != substitutionsSingle)
            {
                foreach (var substitutionsKvp in substitutionsSingle)
                    myMessage.AddSubstitution(substitutionsKvp.Key,
                        substitutionsKvp.Value.ToEnumerable().ToList());
            }

            if (default(IDictionary<string, List<string>>) != substitutionsMultiple)
            {
                foreach (var substitutionsKvp in substitutionsMultiple)
                    myMessage.AddSubstitution(substitutionsKvp.Key, substitutionsKvp.Value.ToList());
            }

            // Create credentials, specifying your user name and password.
            var credentials = new NetworkCredential(username, password);

            // Create an Web transport for sending email.
            var transportWeb = new global::SendGrid.Web(credentials);

            // Send the email, which returns an awaitable task.
            return await SendMessageAsync(transportWeb, myMessage,
                () => onSuccess(toAddress),
                onFailed);
        }

        private async Task<TResult> SendMessageAsync<TResult>(global::SendGrid.Web transportWeb, SendGridMessage message,
            Func<TResult> onSuccess,
            Func<string, TResult> onFailure)
        {
            var emailMuteString = Microsoft.Azure.CloudConfigurationManager.GetSetting("BlackBarLabs.Web.SendMailService.Mute");
            var emailMute = String.Compare(emailMuteString, "true", true) == 0;
            var copyEmail = Microsoft.Azure.CloudConfigurationManager.GetSetting("BlackBarLabs.Web.SendMailService.CopyAllAddresses");
            try
            {
                if(!emailMute)
                    await transportWeb.DeliverAsync(message);

                try
                {
                    if (!string.IsNullOrEmpty(copyEmail))
                    {
                        var toAddresses = copyEmail.Split(',');
                        if (toAddresses.Length > 0)
                        {
                            message.To = new MailAddress[] { };
                            message.AddTo(toAddresses);
                            await transportWeb.DeliverAsync(message);
                        }
                    }
                } catch(Exception ex)
                {
                    // TODO: Log this
                    ex.GetType();
                }
                return onSuccess();
            }
            catch (InvalidApiRequestException ex)
            {
                var details = new StringBuilder();

                details.Append("ResponseStatusCode: " + ex.ResponseStatusCode + ".   ");
                for (int i = 0; i < ex.Errors.Count(); i++)
                {
                    details.Append(" -- Error #" + i.ToString() + " : " + ex.Errors[i]);
                }

                return onFailure(details.ToString());
            }
        }

        public async Task SendEmailMessageAsync(string toAddress, string fromAddress, string fromName, string subject, 
            string template,
            Func<string, Task> onSuccess,
            IDictionary<string, List<string>> substitutions,
            Action<string, IDictionary<string, string>> logIssue)
        {
            var myMessage = new SendGridMessage();
            myMessage.AddTo(toAddress);
            myMessage.From = new MailAddress(fromAddress, fromName);
            myMessage.Subject = subject;
            myMessage.EnableTemplateEngine(template);
            myMessage.Text = "asdf";
            myMessage.Html = "<html></html>";
            
            if (default(IDictionary<string, List<string>>) != substitutions)
            {
                foreach (var substitutionsKvp in substitutions)
                    myMessage.AddSubstitution(substitutionsKvp.Key, substitutionsKvp.Value);
            }

            // Create credentials, specifying your user name and password.
            var credentials = new NetworkCredential(username, password);

            // Create an Web transport for sending email.
            var transportWeb = new global::SendGrid.Web(credentials);

            // Send the email, which returns an awaitable task.
            try
            {
                await transportWeb.DeliverAsync(myMessage);
            }
            catch (InvalidApiRequestException ex)
            {
                var details = new StringBuilder();

                details.Append("ResponseStatusCode: " + ex.ResponseStatusCode + ".   ");
                for (int i = 0; i < ex.Errors.Count(); i++)
                {
                    details.Append(" -- Error #" + i.ToString() + " : " + ex.Errors[i]);
                }

                throw new ApplicationException(details.ToString(), ex);
            }
            onSuccess.Invoke(toAddress);
        }
    }
}
