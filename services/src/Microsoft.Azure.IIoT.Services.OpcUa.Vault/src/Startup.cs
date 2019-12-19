// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
//  Licensed under the MIT License (MIT). See License.txt in the repo root for license information.
// ------------------------------------------------------------

namespace Microsoft.Azure.IIoT.Services.OpcUa.Vault {
    using Microsoft.Azure.IIoT.Services.OpcUa.Vault.Runtime;
    using Microsoft.Azure.IIoT.Services.OpcUa.Vault.v2;
    using Microsoft.Azure.IIoT.Services;
    using Microsoft.Azure.IIoT.Services.Auth;
    using Microsoft.Azure.IIoT.Services.Auth.Clients;
    using Microsoft.Azure.IIoT.Services.Cors;
    using Microsoft.Azure.IIoT.OpcUa.Registry.Events.v2;
    using Microsoft.Azure.IIoT.OpcUa.Vault.Handler;
    using Microsoft.Azure.IIoT.OpcUa.Vault.Events;
    using Microsoft.Azure.IIoT.OpcUa.Vault.Services;
    using Microsoft.Azure.IIoT.OpcUa.Vault.Storage;
    using Microsoft.Azure.IIoT.OpcUa.Api.Registry.Clients;
    using Microsoft.Azure.IIoT.OpcUa.Api.Registry;
    using Microsoft.Azure.IIoT.Crypto.KeyVault.Clients;
    using Microsoft.Azure.IIoT.Crypto.Storage;
    using Microsoft.Azure.IIoT.Crypto.Default;
    using Microsoft.Azure.IIoT.Storage.CosmosDb.Services;
    using Microsoft.Azure.IIoT.Storage.Default;
    using Microsoft.Azure.IIoT.Messaging.ServiceBus.Services;
    using Microsoft.Azure.IIoT.Messaging.ServiceBus.Clients;
    using Microsoft.Azure.IIoT.Messaging.Default;
    using Microsoft.Azure.IIoT.Utils;
    using Microsoft.Azure.IIoT.Http.Auth;
    using Microsoft.Azure.IIoT.Http.Default;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Newtonsoft.Json;
    using Autofac;
    using Autofac.Extensions.DependencyInjection;
    using Serilog;
    using Swashbuckle.AspNetCore.Swagger;
    using System;
    using ILogger = Serilog.ILogger;

    /// <summary>
    /// Webservice startup
    /// </summary>
    public class Startup {

        /// <summary>
        /// Configuration - Initialized in constructor
        /// </summary>
        public Config Config { get; }

        /// <summary>
        /// Service info - Initialized in constructor
        /// </summary>
        public ServiceInfo ServiceInfo { get; } = new ServiceInfo();

        /// <summary>
        /// Current hosting environment - Initialized in constructor
        /// </summary>
        public IHostingEnvironment Environment { get; }

        /// <summary>
        /// Di container - Initialized in `ConfigureServices`
        /// </summary>
        public IContainer ApplicationContainer { get; private set; }

        /// <summary>
        /// Create startup
        /// </summary>
        /// <param name="env"></param>
        /// <param name="configuration"></param>
        public Startup(IHostingEnvironment env, IConfiguration configuration) :
            this(env, new Config(new ConfigurationBuilder()
                .AddConfiguration(configuration)
                .AddEnvironmentVariables()
                .AddEnvironmentVariables(EnvironmentVariableTarget.User)
                .AddFromDotEnvFile()
                .AddFromKeyVault()
                .Build())) {
        }

        /// <summary>
        /// Create startup
        /// </summary>
        /// <param name="env"></param>
        /// <param name="configuration"></param>
        public Startup(IHostingEnvironment env, Config configuration) {
            Environment = env;
            Config = configuration;
        }

        /// <summary>
        /// This is where you register dependencies, add services to the
        /// container. This method is called by the runtime, before the
        /// Configure method below.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public IServiceProvider ConfigureServices(IServiceCollection services) {

            services.AddLogging(o => o.AddConsole().AddDebug());

            // Setup (not enabling yet) CORS
            services.AddCors();
            services.AddHealthChecks();

            // Add authentication
            services.AddJwtBearerAuthentication(Config,
                Environment.IsDevelopment());

            // Add authorization
            services.AddAuthorization(options => {
                options.AddPolicies(Config.AuthRequired,
                    Config.UseRoles && !Environment.IsDevelopment());
            });

            // Add controllers as services so they'll be resolved.
            services.AddMvc()
                .AddApplicationPart(GetType().Assembly)
                .AddControllersAsServices()
                .AddJsonOptions(options => {
                    options.SerializerSettings.Formatting = Formatting.Indented;
                    options.SerializerSettings.Converters.Add(new ExceptionConverter(
                        Environment.IsDevelopment()));
                    options.SerializerSettings.MaxDepth = 10;
                });

            services.AddApplicationInsightsTelemetry(Config.Configuration);

            services.AddSwagger(Config, new Info {
                Title = ServiceInfo.Name,
                Version = VersionInfo.PATH,
                Description = ServiceInfo.Description,
            });

            // Prepare DI container
            var builder = new ContainerBuilder();
            builder.Populate(services);
            ConfigureContainer(builder);
            ApplicationContainer = builder.Build();
            return new AutofacServiceProvider(ApplicationContainer);
        }


        /// <summary>
        /// This method is called by the runtime, after the ConfigureServices
        /// method above and used to add middleware
        /// </summary>
        /// <param name="app"></param>
        /// <param name="appLifetime"></param>
        public void Configure(IApplicationBuilder app, IApplicationLifetime appLifetime) {

            var log = ApplicationContainer.Resolve<ILogger>();

            if (Config.AuthRequired) {
                app.UseAuthentication();
            }
            if (Config.HttpsRedirectPort > 0) {
                // app.UseHsts();
                app.UseHttpsRedirection();
            }

            app.EnableCors();
            app.UseCorrelation();
            app.UseSwagger(new Info {
                Title = ServiceInfo.Name,
                Version = VersionInfo.PATH,
                Description = ServiceInfo.Description,
            });

            app.UseMvc();
            app.UseHealthChecks("/healthz");

            // If you want to dispose of resources that have been resolved in the
            // application container, register for the "ApplicationStopped" event.
            appLifetime.ApplicationStopped.Register(ApplicationContainer.Dispose);

            // Print some useful information at bootstrap time
            log.Information("{service} web service started with id {id}", ServiceInfo.Name,
                Uptime.ProcessId);
        }

        /// <summary>
        /// Autofac configuration.
        /// </summary>
        /// <param name="builder"></param>
        public virtual void ConfigureContainer(ContainerBuilder builder) {

            // Register service info and configuration interfaces
            builder.RegisterInstance(ServiceInfo)
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterInstance(Config)
                .AsImplementedInterfaces().SingleInstance();

            // Add diagnostics based on configuration
            builder.AddDiagnostics(Config);

            // CORS setup
            builder.RegisterType<CorsSetup>()
                .AsImplementedInterfaces().SingleInstance();

            // Register http client module
            builder.RegisterModule<HttpClientModule>();

            // ... with bearer auth
            builder.RegisterType<DistributedTokenCache>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<HttpBearerAuthentication>()
                .AsImplementedInterfaces().SingleInstance();

            // key vault client ...
            builder.RegisterType<UserOrServiceTokenProvider>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<KeyVaultServiceClient>()
                .AsImplementedInterfaces().SingleInstance();

            // Register event bus ...
            builder.RegisterType<EventBusHost>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ServiceBusClientFactory>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<ServiceBusEventBus>()
                .AsImplementedInterfaces().SingleInstance();

            // ... subscribe to application events ...
            builder.RegisterType<ApplicationEventBusSubscriber>()
                .AsImplementedInterfaces().SingleInstance();
            // ... and auto start
            builder.RegisterType<HostAutoStart>()
                .AutoActivate()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<RegistryEventHandler>()
                .AsImplementedInterfaces().SingleInstance();

            // Crypto services
            builder.RegisterType<CertificateDatabase>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<CertificateRevoker>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<CertificateFactory>()
                .AsImplementedInterfaces().SingleInstance();
            // TODO: Add keyvault
            builder.RegisterType<CertificateIssuer>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<KeyDatabase>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<KeyHandleSerializer>()
                .AsImplementedInterfaces().SingleInstance();

            // Register registry micro services adapters
            builder.RegisterType<RegistryServiceClient>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<RegistryAdapter>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<EntityInfoResolver>()
                .AsImplementedInterfaces().SingleInstance();

            // Vault services
            builder.RegisterType<RequestDatabase>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<GroupDatabase>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<CertificateRequestEventBroker>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<CertificateRequestManager>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<TrustGroupServices>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<CertificateAuthority>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<KeyPairRequestProcessor>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SigningRequestProcessor>()
                .AsImplementedInterfaces().SingleInstance();

            // Vault handler
            builder.RegisterType<AutoApproveHandler>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<KeyPairRequestHandler>()
                .AsImplementedInterfaces().SingleInstance();
            builder.RegisterType<SigningRequestHandler>()
                .AsImplementedInterfaces().SingleInstance();

            // ... with cosmos db collection as storage
            builder.RegisterType<ItemContainerFactory>()
                .AsImplementedInterfaces();
            builder.RegisterType<CosmosDbServiceClient>()
                .AsImplementedInterfaces();
        }
    }
}
