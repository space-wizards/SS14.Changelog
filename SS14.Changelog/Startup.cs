using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SS14.Changelog.Configuration;
using SS14.Changelog.Services;

namespace SS14.Changelog
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
            services.Configure<ChangelogConfig>(Configuration.GetSection("Changelog"));
            services.AddControllers();
            
            services.AddSingleton<ChangelogService>();
            services.AddHostedService(p => p.GetRequiredService<ChangelogService>());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            
            app.UseRouting();

            app.UseAuthorization();

            app.UseSerilogRequestLogging();
            
            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}