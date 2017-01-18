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

        public async Task SendEmailMessageAsync(string toAddress, string fromAddress, string fromName, string subject, 
            string template,
            EmailSendSuccessDelegate onSuccess,
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

        public Task<TResult> SendEmailMessageAsync<TResult>(string toAddress, string toName, string fromAddress, string fromName, string templateName, IDictionary<string, string> substitutionsSingle, IDictionary<string, string[]> substitutionsMultiple, Func<string, TResult> onSuccess, Func<TResult> onServiceUnavailable, Func<string, TResult> onFailed)
        {
            throw new NotImplementedException();
        }
    }
}
