using Microsoft.Owin;
using Owin;

[assembly: OwinStartup(typeof(ChobiIotMAService.Startup))]

namespace ChobiIotMAService
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureMobileApp(app);
        }
    }
}