using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Mail;
using System.Net;
using BlackBarLabs.Extensions;
using SendGrid.Helpers.Mail;
using BlackBarLabs.Collections.Generic;
using BlackBarLabs.Web;
using BlackBarLabs.Linq;
using EastFive.Api.Services;

namespace BlackBarLabs.SendGrid
{
    public class TemplateMailer : EastFive.Api.Services.ISendMessageService
    {
        private readonly string apiKey;

        public TemplateMailer(string apiKey)
        {
            this.apiKey = apiKey;
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

            var emailMuteString = Microsoft.Azure.CloudConfigurationManager.GetSetting(Configuration.MuteEmailToAddress);
            var emailMute = !String.IsNullOrWhiteSpace(emailMuteString);
            var toAddressEmail = emailMute?
                new EmailAddress(emailMuteString, $"MUTED[{toAddress}:{toName}]")
                :
                new EmailAddress(toAddress, toName);
            message.AddTo(toAddressEmail);
            if (emailMute)
                message.SetClickTracking(false, false);

            var copyEmail = Microsoft.Azure.CloudConfigurationManager.GetSetting(Configuration.BccAllAddresses);
            var bccAddresses = (String.IsNullOrEmpty(copyEmail)? "" : copyEmail)
                        .Split(',')
                        .Where(s => !String.IsNullOrWhiteSpace(s))
                        .Select((bccAddress) => new EmailAddress(bccAddress))
                        .ToList();
            if (bccAddresses.Count > 0)
                message.AddBccs(bccAddresses);

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
