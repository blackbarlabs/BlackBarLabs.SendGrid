using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using BlackBarLabs.SendGrid.Exceptions;
using BlackBarLabs.Web;
using Exceptions;
using SendGrid;

namespace BlackBarLabs.SendGrid
{
    public class Mailer : Web.ISendMailService
    {
        private readonly string username;
        private readonly string password;
        private readonly string toAddressTestOverride;
        public Mailer(string username, string password, string toAddressTestOverride = null)
        {
            this.username = username;
            this.password = password;
            this.toAddressTestOverride = toAddressTestOverride;
        }
        public async Task SendEmailMessageAsync(string toAddress, string fromAddress, string fromName, string subject, string html, 
            EmailSendSuccessDelegate onSuccess, 
            IDictionary<string, List<string>> substitutions = null)
        {
            if (!string.IsNullOrEmpty(toAddressTestOverride))
            {
                var toAddresses = toAddressTestOverride.Split(',');
                foreach (var address in toAddresses)
                {
                    await DispatchMessageAsync(address, fromAddress, fromName, subject, html, onSuccess, substitutions);
                }
                return;
            }

            await DispatchMessageAsync(toAddress, fromAddress, fromName, subject, html, onSuccess, substitutions);
        }

        public async Task DispatchMessageAsync(string toAddress, string fromAddress, string fromName, string subject,
            string html, EmailSendSuccessDelegate onSuccess,
            IDictionary<string, List<string>> substitutions = null)
        {
            // Create the email object first, then add the properties.
            var myMessage = new SendGridMessage();
            myMessage.AddTo(toAddress);
            myMessage.From = new MailAddress(fromAddress, fromName);
            myMessage.Subject = subject;
            myMessage.Html = html;

            ValidateMessageSubstitutions(html, substitutions);
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


        private static void ValidateMessageSubstitutions(string html, IDictionary<string, List<string>> substitutions)
        {
            if (null == substitutions) return;
            foreach (var sub in substitutions)
            {
                if (!html.Contains(sub.Key))
                    throw new EmailSubstitutionParameterException("Could not find substitution string " + sub.Key + " in email text.");
                if (null == sub.Value)
                    throw new EmailSubstitutionParameterException("No list of substitutions given for substitution value " + sub.Key);
                if (sub.Value.Count == 0)
                    throw new EmailSubstitutionParameterException("No value given for substitution value " + sub.Key);
                var values = sub.Value;
                foreach (var value in values)
                {
                    if (string.IsNullOrEmpty(value))
                        throw new EmailSubstitutionParameterException("String is null for substitution value " + sub.Key);
                }
            }
        }
    }
}
