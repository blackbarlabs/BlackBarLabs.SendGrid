using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SendGrid.Helpers.Mail;

using BlackBarLabs;
using BlackBarLabs.Extensions;
using BlackBarLabs.Collections.Generic;
using BlackBarLabs.Web;
using BlackBarLabs.Linq;
using EastFive.Web.Services;
using EastFive.Collections.Generic;

namespace EastFive.SendGrid
{
    public class SendGridMailer : ISendMessageService
    {
        private string apiKey;

        public SendGridMailer(string apiKey)
        {
            this.apiKey = apiKey;
        }

        public static TResult Load<TResult>(
            Func<SendGridMailer,TResult> onSuccess,
            Func<string,TResult> onFailure)
        {
            return EastFive.Web.Configuration.Settings.GetString(EastFive.SendGrid.AppSettings.ApiKey,
                key => onSuccess(new SendGridMailer(key)),
                onFailure);
        }

        public async Task<SendMessageTemplate[]> ListTemplatesAsync()
        {
            var client = new global::SendGrid.SendGridClient(apiKey);
            var responseTemplates = await client.RequestAsync(global::SendGrid.SendGridClient.Method.GET, urlPath: $"/templates");
            var templateInfo = await responseTemplates.Body.ReadAsStringAsync();
            var converter = new Newtonsoft.Json.Converters.ExpandoObjectConverter();
            dynamic obj = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Dynamic.ExpandoObject>(templateInfo, converter);
            return ((List<object>)obj.templates).Select((dynamic tmp) => new SendMessageTemplate()
            {
                externalTemplateId = tmp.id,
                name = tmp.name,
            }).ToArray();
        }

        public async Task<TResult> SendEmailMessageAsync<TResult>(
            string templateName,
            string toAddress, string toName,
            string fromAddress, string fromName,
            string subject,
            IDictionary<string, string> substitutionsSingle,
            IDictionary<string, IDictionary<string, string>[]> substitutionsMultiple,
            Func<string, TResult> onSuccess, 
            Func<TResult> onServiceUnavailable,
            Func<string, TResult> onFailure)
        {
            var message = new SendGridMessage();
            message.From = new EmailAddress(fromAddress, fromName);
            message.Subject = subject;
            message.TemplateId = templateName;

            var emailMute = false;
            var toAddressEmail = EastFive.Web.Configuration.Settings.GetString(AppSettings.MuteEmailToAddress,
                (emailMuteString) =>
                {
                    emailMute = true;
                    return new EmailAddress(emailMuteString, $"MUTED[{toAddress}:{toName}]");
                },
                (why) => new EmailAddress(toAddress, toName));

            message.AddTo(toAddressEmail);
            if (emailMute)
                message.SetClickTracking(false, false);

            var bccAddressesAdded = Web.Configuration.Settings.GetString(AppSettings.BccAllAddresses,
                copyEmail =>
                {
                    var bccAddresses = (copyEmail.IsNullOrWhiteSpace() ? "" : copyEmail)
                        .Split(',')
                        .Where(s => !String.IsNullOrWhiteSpace(s))
                        .Select((bccAddress) => new EmailAddress(bccAddress))
                        .ToList();
                    if (bccAddresses.Any())
                        message.AddBccs(bccAddresses);
                    return true;
                },
                (why) => false);

            message.AddSubstitutions(substitutionsSingle
                .Select(kvp => new KeyValuePair<string, string>($"--{kvp.Key}--", kvp.Value))
                .ToDictionary());
            var client = new global::SendGrid.SendGridClient(apiKey);

            if (substitutionsMultiple != default(IDictionary<string, IDictionary<string, string>[]>) &&
               substitutionsMultiple.Count > 0)
            {
                var responseTemplates = await client.RequestAsync(global::SendGrid.SendGridClient.Method.GET, urlPath: $"/templates/{templateName}");
                var templateInfo = await responseTemplates.Body.ReadAsStringAsync();
                var converter = new Newtonsoft.Json.Converters.ExpandoObjectConverter();
                dynamic obj = Newtonsoft.Json.JsonConvert.DeserializeObject<System.Dynamic.ExpandoObject>(templateInfo, converter);
                string html = obj.versions[0].html_content;
                var htmlDoc = new HtmlAgilityPack.HtmlDocument();
                htmlDoc.LoadHtml(html);
                if (htmlDoc.ParseErrors != null && htmlDoc.ParseErrors.Count() > 0)
                {
                    throw new Exception();
                }
                var substitutionsMultipleExpanded = substitutionsMultiple.SelectMany(
                    (substitutionMultiple) =>
                    {
                        var matchingNodes = htmlDoc.DocumentNode.SelectNodes($"//*[@data='--{substitutionMultiple.Key}--']");
                        if (matchingNodes != null && matchingNodes.Count > 0)
                        {
                            var substituations = matchingNodes
                                .Select(
                                    matchingNode =>
                                    {
                                        var subText = substitutionMultiple.Value
                                            .Select(
                                                (subValues) =>
                                                {
                                                    return subValues.Aggregate(
                                                        matchingNode.OuterHtml,
                                                        (subTextAggr, sub) =>
                                                        {
                                                            subTextAggr = subTextAggr.Replace($"--{sub.Key}--", sub.Value);
                                                            return subTextAggr;
                                                        });
                                                })
                                            .Join(" ");
                                        return new KeyValuePair<string, string>(matchingNode.OuterHtml, subText);
                                    })
                                .ToArray();
                            return substituations;
                        }
                        return new KeyValuePair<string, string>[] { };
                    }).ToDictionary();
                message.AddSubstitutions(substitutionsMultipleExpanded);
            }

            // Send the email, which returns an awaitable task.
            try
            {
                var response = await client.SendEmailAsync(message);
                var body = await response.Body.ReadAsStringAsync();
                if (response.StatusCode.IsSuccess())
                    return onSuccess(body);

                return onFailure(body);
            }
            catch (Exception ex)
            {
                //var details = new StringBuilder();

                //details.Append("ResponseStatusCode: " + ex.ResponseStatusCode + ".   ");
                //for (int i = 0; i < ex.Errors.Count(); i++)
                //{
                //    details.Append(" -- Error #" + i.ToString() + " : " + ex.Errors[i]);
                //}

                return onFailure(ex.ToString());
            }
        }
    }
}
