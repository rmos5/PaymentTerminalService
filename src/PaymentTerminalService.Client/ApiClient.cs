using System.Configuration;

namespace PaymentTerminalService.Client
{
    public partial class PaymentTerminalServiceClient
    {
        partial void Initialize()
        {
            var configuredUrl = ConfigurationManager.AppSettings["PaymentTerminalServiceUrl"];
            if (!string.IsNullOrEmpty(configuredUrl))
            {
                BaseUrl = configuredUrl;
            }
        }
    }
}