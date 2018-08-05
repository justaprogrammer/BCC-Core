﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Threading.Tasks;
using GitHubJwt;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSBLOC.Core.Interfaces;
using MSBLOC.Core.Services;
using MSBLOC.Web.Attributes;
using MSBLOC.Web.Interfaces;
using MSBLOC.Web.Models;
using MSBLOC.Web.Services;
using MSBLOC.Web.Util;
using Newtonsoft.Json.Linq;
using Swashbuckle.AspNetCore.Swagger;

namespace MSBLOC.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = "GitHub";
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/signin";
                options.LogoutPath = "/signout";
            })
            .AddGitHub(options =>
            {
                options.ClientId = Configuration["GitHub:OAuth:ClientId"];
                options.ClientSecret = Configuration["GitHub:OAuth:ClientSecret"];
                options.Scope.Add("user:email");

                options.ClaimActions.Clear();
                options.ClaimActions.MapJsonKey(ClaimTypes.NameIdentifier, "id");
                options.ClaimActions.MapJsonKey(ClaimTypes.Name, "name");
                options.ClaimActions.MapJsonKey("urn:github:login", "login");
                options.ClaimActions.MapJsonKey("urn:github:url", "html_url");
                options.ClaimActions.MapJsonKey("urn:github:avatar", "avatar_url");

                options.SaveTokens = true;
            });

            services.AddMvc().AddJsonOptions(options =>
            {
                options.SerializerSettings.Converters.Add(new OctokitStringEnumConverter());
            });

            services.Configure<GitHubAppOptions>(Configuration.GetSection("GitHub:App"));

            services.AddSingleton<IPrivateKeySource, OptionsPrivateKeySource>();
            services.AddSingleton<Func<string, Task<ICheckRunSubmitter>>>(s => async repoOwner =>
            {
                var gitHubAppId = s.GetService<IOptions<GitHubAppOptions>>().Value.Id;
                var privateKeySource = s.GetService<IPrivateKeySource>();
                var gitHubTokenGenerator = new TokenGenerator(gitHubAppId, privateKeySource, s.GetService<ILogger<TokenGenerator>>());

                var gitHubClientFactory = new GitHubClientFactory(gitHubTokenGenerator);
                var gitHubClient = await gitHubClientFactory.CreateAppClient(repoOwner);

                return new CheckRunSubmitter(gitHubClient.Check.Run, s.GetService<ILogger<CheckRunSubmitter>>());
            });

            services.AddScoped<ITempFileService, LocalTempFileService>();
            services.AddScoped<IBinaryLogProcessor, BinaryLogProcessor>();

            services.AddTransient<IMSBLOCService, MSBLOCService>();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("0.0.1", new Info { Title = "MSBLOC Web API", Version = "0.0.1" });
                c.OperationFilter<MultiPartFormBindingAttribute.MultiPartFormBindingFilter>();
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseBrowserLink();
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseSwagger(c =>
            {
                c.RouteTemplate = "docs/{documentName}/swagger.json";

                //Make routes lower case (Controller names for example)
                c.PreSerializeFilters.Add((document, request) =>
                {
                    document.Paths = document.Paths.ToDictionary(item => item.Key.ToLowerInvariant(), item => item.Value);
                });
            });

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/docs/0.0.1/swagger.json", "MSBLOC Web API");
                c.RoutePrefix = "docs";
            });

            app.UseStaticFiles();

            app.UseAuthentication();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
