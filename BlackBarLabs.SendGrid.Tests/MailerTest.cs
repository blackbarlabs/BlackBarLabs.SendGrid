using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using BlackBarLabs.SendGrid.Exceptions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlackBarLabs.SendGrid.Tests
{
    [TestClass]
    public class MailerTest
    {
        [Ignore]
        [TestMethod]
        public async Task SendEmail()
        {
            var username = System.Configuration.ConfigurationManager.AppSettings["SendGrid.UserName"];
            var password = System.Configuration.ConfigurationManager.AppSettings["SendGrid.Password"];
            var mailer = new Mailer(username, password);
            var html = @"<h1>Test Message</h1><p>This is a test</p>" ;
            await mailer.SendEmailMessageAsync("keithdholloway@gmail.com", "test@blackbarlabs.com", "Black Bar Labs Testing",
                "Test Email", html);
        }

        [Ignore]
        [TestMethod]
        public async Task SendEmailFromFile()
        {
            var username = System.Configuration.ConfigurationManager.AppSettings["SendGrid.UserName"];
            var password = System.Configuration.ConfigurationManager.AppSettings["SendGrid.Password"];
            var mailer = new Mailer(username, password);

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "BlackBarLabs.SendGrid.Tests.email.txt";
            var stream = assembly.GetManifestResourceStream(resourceName);
            var reader = new StreamReader(stream);
            var template = reader.ReadToEnd();

            var html = template;
            await mailer.SendEmailMessageAsync("keith@eastfive.com", "test@eastfive.com", "Testing",
                "Test Email", html);
        }

        [Ignore]
        [TestMethod]
        public async Task SendEmailWithSubstitutionParameters()
        {
            var username = System.Configuration.ConfigurationManager.AppSettings["SendGrid.UserName"];
            var password = System.Configuration.ConfigurationManager.AppSettings["SendGrid.Password"];
            var mailer = new Mailer(username, password);
            var html = @"<h1>Test Message</h1><p>This is a test</p><br/><p>-Name-</p><br/><p>-Number-</p><br/><p>-State-</p><br/>";

            var substitutions = new Dictionary<string, List<string>>();
            substitutions.Add("-Name-", new List<string>() {"John"});
            substitutions.Add("-Number-", new List<string>() { "123123" });
            substitutions.Add("-State-", new List<string>() { "GA" });
            
            await mailer.SendEmailMessageAsync("keithdholloway@gmail.com", "test@blackbarlabs.com", "Black Bar Labs Testing",
                "Test Email", html, substitutions);
        }

        [TestMethod]
        [ExpectedException(typeof(EmailSubstitutionParameterException))]
        public async Task SendEmailWithTooManySubstitutionParameters()
        {
            var username = System.Configuration.ConfigurationManager.AppSettings["SendGrid.UserName"];
            var password = System.Configuration.ConfigurationManager.AppSettings["SendGrid.Password"];
            var mailer = new Mailer(username, password);
            var html = @"<h1>Test Message</h1><p>This is a test</p><br/><p>-Name-</p><br/><p>-Number-</p><br/><p>-State-</p><br/>";

            var substitutions = new Dictionary<string, List<string>>();
            substitutions.Add("-Name-", new List<string>() { "John" });
            substitutions.Add("-Number-", new List<string>() { "123123" });
            substitutions.Add("-State-", new List<string>() { "GA" });
            substitutions.Add("-Missing-", new List<string>() { "Missing" });

            await mailer.SendEmailMessageAsync("keithdholloway@gmail.com", "test@blackbarlabs.com", "Black Bar Labs Testing",
                "Test Email", html, substitutions);
        }
    }
}
