namespace PaymentTerminalService.Host
{
    /// <summary>
    /// Defines environment variable keys for the host.
    /// </summary>
    public static class EnvironmentVariables
    {
        /// <summary>
        /// Environment variable name used to control Swagger availability.
        /// Type: string. Value: "1" enables Swagger; any other value disables it.
        /// </summary>
        public const string SwaggerEnabled = "PAYMENT_TERMINAL_SERVICE_SWAGGER_ENABLED";
    }
}