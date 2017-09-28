using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AngularSpa
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
            services.AddMvc();
            services.AddNodeServices();
            services.AddSpaPrerenderer(); // TODO: Make this unnecessary by making UsePrerendering able to get a default instance if not explicitly registered
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller}/{action=Index}/{id?}");
            });

            // Handles any other request by serving the Angular application from the
            // specified public URL path
            app.UseSpa(publicPath: "/dist", setup: spa =>
            {
                /*
                // If you want to enable server-side prerendering for your app, then:
                // [1] Edit your application .csproj file and set the BuildServerSideRenderer
                //     property to 'true' so that the entrypoint file is built on publish
                // [2] Uncomment this code block
                spa.UsePrerendering("ClientApp/dist-server/main.bundle.js",
                    buildOnDemand: env.IsDevelopment() ? new AngularCliBuild("ssr") : null);
                */

                if (env.IsDevelopment())
                {
                    // In development, the Angular application does not have to be built
                    // already, as it will be served dynamically via the Angular CLI
                    spa.UseAngularCliMiddleware(sourcePath: "./ClientApp");
                }
            });
        }
    }
}
