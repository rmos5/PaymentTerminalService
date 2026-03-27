using Owin;
using PaymentTerminalService.Model;
using PaymentTerminalService.Web;
using SimpleInjector;
using SimpleInjector.Integration.WebApi;
using Swashbuckle.Application;
using System;
using System.Configuration;
using System.IO;
using System.Web.Http;

namespace PaymentTerminalService.Host
{
    /// <summary>
    /// OWIN startup class for PaymentTerminalService Web API.
    /// Configures routing, formatters, Swagger, and dependency injection.
    /// </summary>
    internal class Startup
    {
        /// <summary>
        /// Configures the OWIN pipeline and Web API for the PaymentTerminalService.
        /// Sets up routing, JSON formatting, Swagger UI, and dependency injection for services.
        /// </summary>
        /// <param name="app">The OWIN application builder.</param>
        public void Configuration(IAppBuilder app)
        {
            var config = new HttpConfiguration();

            config.Filters.Add(new ApiExceptionFilter());

            // Set up SimpleInjector container for dependency injection
            var container = new Container();

            var terminalSessionDirectoryBase = ConfigurationManager.AppSettings["TerminalSessionDirectoryBase"];
            terminalSessionDirectoryBase = string.IsNullOrWhiteSpace(terminalSessionDirectoryBase)
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TerminalSessions")
                : terminalSessionDirectoryBase.Trim();

            // Register IPaymentTerminalSelector implementation as a singleton
            container.Register<IPaymentTerminalSelector>(() =>
            {
                return new PaymentTerminalManagementService(terminalSessionDirectoryBase);
            }, Lifestyle.Singleton);

            container.RegisterWebApiControllers(config);
            config.DependencyResolver = new SimpleInjectorWebApiDependencyResolver(container);
            config.MapHttpAttributeRoutes();

            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            config.Formatters.Remove(config.Formatters.XmlFormatter);

            // Enable Swagger if environment variable is "1" or if DEBUG build
            bool enableSwagger = true;
#if !DEBUG
            enableSwagger = Environment.GetEnvironmentVariable(EnvironmentVariables.SwaggerEnabled) == "1";
#endif

            if (enableSwagger)
            {
                config.EnableSwagger(c =>
                {
                    c.SingleApiVersion("v1", "PaymentTerminalService");
                    c.UseFullTypeNameInSchemaIds();
                    c.DescribeAllEnumsAsStrings();

                    var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    c.IncludeXmlComments(Path.Combine(baseDir, "PaymentTerminalService.Web.xml"));
                    c.IncludeXmlComments(Path.Combine(baseDir, "PaymentTerminalService.Model.xml"));
                })
                .EnableSwaggerUi(c =>
                {
                    var thisAssembly = GetType().Assembly;
                    c.InjectJavaScript(thisAssembly, "PaymentTerminalService.Host.Swagger.custom.js");
                });

                app.Use(async (ctx, next) =>
                {
                    var path = ctx.Request.Path.Value ?? "";
                    if (path.Equals("/swagger", StringComparison.OrdinalIgnoreCase) ||
                        path.Equals("/swagger/", StringComparison.OrdinalIgnoreCase))
                    {
                        ctx.Response.StatusCode = 302;
                        ctx.Response.Headers.Set("Location", "/swagger/ui/index");
                        return;
                    }
                    await next();
                });
            }

            app.UseWebApi(config);
        }
    }
}