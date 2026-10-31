using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Stateless.WorkflowEngine.WebConsole.Configuration;
using Stateless.WorkflowEngine.WebConsole.Storage.Stores;
using Stateless.WorkflowEngine.WebConsole.Features.Connections;
using Stateless.WorkflowEngine.WebConsole.Features.Users;
using Stateless.WorkflowEngine.WebConsole.Security;
using Stateless.WorkflowEngine.WebConsole.Services;
using System.IO.Abstractions;

namespace Stateless.WorkflowEngine.WebConsole
{
    public static class WebConsoleServiceCollectionExtensions
    {
        public const string UserStoreFileName = "users.json";
        public const string ConnectionTestPolicy = "ConnectionTest";

        public static IServiceCollection AddWebConsole(this IServiceCollection services, IAppSettings appSettings)
        {
            services.AddSingleton(appSettings);
            services.AddSingleton<IFileSystem, FileSystem>();

            services.AddDataProtection()
                .SetApplicationName("Stateless.WorkflowEngine.WebConsole")
                .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(appSettings.DataPath, "keys")));

            services.AddMemoryCache();
            services.AddHttpClient(WebConsoleVersionService.HttpClientName, c =>
            {
                c.DefaultRequestHeaders.UserAgent.ParseAdd("Stateless.WorkflowEngine.WebConsole");
                c.Timeout = TimeSpan.FromSeconds(30);
            });

            // security
            services.AddSingleton<IPasswordProvider, PasswordProvider>();
            services.AddSingleton<ILegacyEncryptionProvider, LegacyEncryptionProvider>();
            services.AddSingleton<IConnectionPasswordProtector, ConnectionPasswordProtector>();

            // data
            services.AddSingleton<IUserStore>(sp =>
            {
                var store = new UserStore(
                    Path.Combine(appSettings.DataPath, UserStoreFileName),
                    sp.GetRequiredService<IFileSystem>(),
                    sp.GetRequiredService<IPasswordProvider>(),
                    sp.GetRequiredService<IConnectionPasswordProtector>(),
                    sp.GetRequiredService<ILogger<UserStore>>());
                store.Load();
                return store;
            });

            // services
            services.AddSingleton<IWorkflowStoreFactory, WorkflowStoreFactory>();
            services.AddSingleton<IWorkflowInfoService, WorkflowInfoService>();
            services.AddSingleton<IWebConsoleVersionService, WebConsoleVersionService>();
            services.AddSingleton<IVersionCheckService, VersionCheckService>();

            // validators
            services.AddTransient<ConnectionValidator>();
            services.AddTransient<UserValidator>();

            AddWebConsoleAuth(services);

            return services;
        }

        private static void AddWebConsoleAuth(IServiceCollection services)
        {
            services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.Cookie.Name = "Stateless.WorkflowEngine.WebConsole";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Strict;
                    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                    options.ExpireTimeSpan = TimeSpan.FromDays(1);
                    options.SlidingExpiration = true;
                    options.Events = new CookieAuthenticationEvents
                    {
                        // this is an API - return status codes rather than redirecting
                        OnRedirectToLogin = ctx =>
                        {
                            ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        },
                        OnRedirectToAccessDenied = ctx =>
                        {
                            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        },
                        // re-read the user on every request so deleted users and changed claims take effect immediately
                        OnValidatePrincipal = async ctx =>
                        {
                            var userStore = ctx.HttpContext.RequestServices.GetRequiredService<IUserStore>();
                            Guid? userId = ctx.Principal == null ? null : UserPrincipalFactory.GetUserId(ctx.Principal);
                            var user = userId.HasValue ? userStore.GetUser(userId.Value) : null;
                            if (user == null)
                            {
                                ctx.RejectPrincipal();
                                await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                                return;
                            }
                            ctx.ReplacePrincipal(UserPrincipalFactory.Create(user));
                        }
                    };
                });

            services.AddAuthorization(options =>
            {
                foreach (string claim in Claims.AllClaims)
                {
                    options.AddPolicy(claim, p => p.RequireAuthenticatedUser().RequireClaim(Claims.PermissionClaimType, claim));
                }
                options.AddPolicy(ConnectionTestPolicy, p => p.RequireAuthenticatedUser().RequireClaim(Claims.PermissionClaimType, Claims.ConnectionAdd, Claims.ConnectionEdit));
            });
        }
    }
}
