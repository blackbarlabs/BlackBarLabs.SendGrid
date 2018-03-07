using EastFive.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EastFive.SendGrid
{
    [Config]
    public static class AppSettings
    {
        [ConfigKey("API Key to access SendGrid", DeploymentOverrides.Suggested,
            DeploymentSecurityConcern = false,
            Location = "SendGrid admin portal",
            PrivateRepositoryOnly = true)]
        public const string ApiKey = "EastFive.SendGrid.ApiKey";
        public const string MuteEmailToAddress = "EastFive.SendGrid.MuteToAddress";
        public const string BccAllAddresses = "EastFive.SendGrid.BlindCopyAllAddresses";
    }
}
