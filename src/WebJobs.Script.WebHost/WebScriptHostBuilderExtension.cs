﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Timers;
using Microsoft.Azure.WebJobs.Script.Diagnostics;
using Microsoft.Azure.WebJobs.Script.WebHost.DependencyInjection;
using Microsoft.Azure.WebJobs.Script.WebHost.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Microsoft.Azure.WebJobs.Script.WebHost
{
    public static class WebScriptHostBuilderExtension
    {
        public static IHostBuilder AddWebScriptHost(this IHostBuilder builder, IServiceProvider rootServiceProvider,
           IServiceScopeFactory rootScopeFactory, ScriptApplicationHostOptions webHostOptions, Action<IWebJobsBuilder> configureWebJobs = null)
        {
            ILoggerFactory configLoggerFactory = rootServiceProvider.GetService<ILoggerFactory>();

            builder.UseServiceProviderFactory(new JobHostScopedServiceProviderFactory(rootServiceProvider, rootScopeFactory))
                .AddScriptHost(webHostOptions, configLoggerFactory, webJobsBuilder =>
                {
                    webJobsBuilder
                        .AddAzureStorageCoreServices();

                    configureWebJobs?.Invoke(webJobsBuilder);

                    ConfigureRegisteredBuilders(webJobsBuilder, rootServiceProvider);
                })
                .ConfigureAppConfiguration(configurationBuilder =>
                {
                    ConfigureRegisteredBuilders(configurationBuilder, rootServiceProvider);
                })
                .ConfigureLogging(loggingBuilder =>
                {
                    loggingBuilder.Services.AddSingleton<ILoggerFactory, ScriptLoggerFactory>();

                    loggingBuilder.AddWebJobsSystem<SystemLoggerProvider>();

                    ConfigureRegisteredBuilders(loggingBuilder, rootServiceProvider);
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<HttpRequestQueue>();
                    services.AddSingleton<IHostLifetime, JobHostHostLifetime>();
                    services.TryAddSingleton<IWebJobsExceptionHandler, WebScriptHostExceptionHandler>();
                    services.AddSingleton<IScriptJobHostEnvironment, WebScriptJobHostEnvironment>();

                    services.AddSingleton<DefaultScriptWebHookProvider>();
                    services.TryAddSingleton<IScriptWebHookProvider>(p => p.GetService<DefaultScriptWebHookProvider>());
                    services.TryAddSingleton<IWebHookProvider>(p => p.GetService<DefaultScriptWebHookProvider>());

                    // Make sure the registered IHostIdProvider is used
                    IHostIdProvider provider = rootServiceProvider.GetService<IHostIdProvider>();
                    if (provider != null)
                    {
                        services.AddSingleton<IHostIdProvider>(provider);
                    }

                    // Logging and diagnostics
                    services.AddSingleton<IMetricsLogger, WebHostMetricsLogger>();
                    services.AddSingleton<IAsyncCollector<Host.Loggers.FunctionInstanceLogEntry>, FunctionInstanceLogger>();

                    // Hosted services
                    services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, HttpInitializationService>());
                    services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, FileMonitoringService>());
                });

            return builder;
        }

        private static void ConfigureRegisteredBuilders<TBuilder>(TBuilder builder, IServiceProvider services)
        {
            foreach (IConfigureBuilder<TBuilder> configureBuilder in services.GetServices<IConfigureBuilder<TBuilder>>())
            {
                configureBuilder.Configure(builder);
            }
        }
    }
}
