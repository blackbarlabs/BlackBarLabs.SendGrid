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
using System.Configuration;
using Microsoft.WindowsAzure;
using EastFive.Api.Services;

namespace BlackBarLabs.SendGrid
{
    public class Mailer : ISendMessageService
    {
        private readonly string username;
        private readonly string password;
        public Mailer(string username, string password)
        {
            this.username = username;
            this.password = password;
        }

        public Task<TResult> SendEmailMessageAsync<TResult>(string toAddress, string toName, string fromAddress, string fromName, string templateName, IDictionary<string, string> substitutionsSingle, IDictionary<string, string[]> substitutionsMultiple, Func<string, TResult> onSuccess, Func<TResult> onServiceUnavailable, Func<string, TResult> onFailed)
        {
            throw new NotImplementedException();
        }

        public async Task SendEmailMessageAsync(string toAddress, string fromAddress, string fromName, string subject, string html, 
            Func<string, Task> onSuccess, 
            IDictionary<string, List<string>> substitutions,
            Action<string, IDictionary<string, string>> logIssue)
        {
            var emailMuteString = Microsoft.Azure.CloudConfigurationManager.GetSetting("BlackBarLabs.Web.SendMailService.Mute");
            var emailMute = String.Compare(emailMuteString, "true", true) == 0;
            var copyEmail = Microsoft.Azure.CloudConfigurationManager.GetSetting("BlackBarLabs.Web.SendMailService.CopyAllAddresses");

            if (!emailMute)
            {
                if (!EmailIsInMuteAddresses(toAddress))
                {
                    await
                        DispatchMessageAsync(toAddress, fromAddress, fromName, subject, html, onSuccess, substitutions,
                            logIssue);
                }
            }
            if (!string.IsNullOrEmpty(copyEmail))
            {
                var toAddresses = copyEmail.Split(',');
                foreach (var address in toAddresses)
                {
                    await DispatchMessageAsync(address, fromAddress, fromName, subject, html, onSuccess, substitutions, logIssue);
                }
                return;
            }
        }

        private static bool EmailIsInMuteAddresses(string toAddress)
        {
            var muteAddresses = Microsoft.Azure.CloudConfigurationManager.GetSetting("BlackBarLabs.Web.SendMailService.SpecificAddressesToMute");
            if (string.IsNullOrEmpty(muteAddresses))
                return false;
            var toAddressesSplit = muteAddresses.Split(',');
            return toAddressesSplit.Any(address => address == toAddress);
        }

        public async Task DispatchMessageAsync(string toAddress, string fromAddress, string fromName, string subject,
            string html,
            Func<string, Task> onSuccess,
            IDictionary<string, List<string>> substitutions,
            Action<string, IDictionary<string, string>> logIssue)
        {
            // Create the email object first, then add the properties.
            var myMessage = new SendGridMessage();
            myMessage.AddTo(toAddress);
            myMessage.From = new MailAddress(fromAddress, fromName);
            myMessage.Subject = subject;
            myMessage.Html = html;

            ValidateMessageSubstitutions(html, substitutions, logIssue);
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


        private static void ValidateMessageSubstitutions(string html, IDictionary<string, List<string>> substitutions,
            Action<string, IDictionary<string, string>> logIssue)
        {
            if (null == substitutions) return;

            var keysToRemove = new List<string>();
            foreach (var sub in substitutions)
            {
                var conditions = new Dictionary<string, string>
                {
                    { "html", html },
                    { "substituations",
                        String.Join("\r", substitutions.Select(kvp => string.Format("[{0}]:[{1}]", kvp.Key, kvp.Value)))  },
                    { "substitution-key", sub.Key },
                    { "substitution-value", String.Join("\r", sub.Value) }
                };

                if (!html.Contains(sub.Key))
                {
                    logIssue("Could not find substitution string " + sub.Key + " in email text.", conditions);
                    keysToRemove.Add(sub.Key);
                    continue;
                }
                if (null == sub.Value)
                {
                    logIssue("No list of substitutions given for substitution value " + sub.Key, conditions);
                    keysToRemove.Add(sub.Key);
                    continue;
                }
                if (sub.Value.Count == 0)
                {
                    logIssue("No value given for substitution value " + sub.Key, conditions);
                    keysToRemove.Add(sub.Key);
                    continue;
                }
                var values = sub.Value;
                foreach (var value in values)
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        logIssue("String is null for substitution value " + sub.Key, conditions);
                        keysToRemove.Add(sub.Key);
                        continue;
                    }
                }
            }
            foreach (var keyToRemove in keysToRemove)
                substitutions.Remove(keyToRemove);
        }
    }
}
