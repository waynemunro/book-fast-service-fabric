using BookFast.SeedWork;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Threading.Tasks;
using System.Security.Claims;
using BookFast.Api.Authentication;
using BookFast.ReliableEvents;
using BookFast.ServiceBus;
using BookFast.Booking.Integration;

namespace BookFast.Booking.Composition
{
    internal class CompositionModule : ICompositionModule
    {
        public void AddServices(IServiceCollection services, IConfiguration configuration)
        {
            AddAuthentication(services, configuration);

            services.AddSecurityContext();
            services.AddAndConfigureMvc();

            services.Configure<TestOptions>(configuration.GetSection("Test"));

            services.AddCommandContext();
            services.AddReliableEventsDispatcher(configuration, new DefaultReliableEventMapper(typeof(Domain.Events.BookingCreatedEvent).Assembly));

            services.AddIntegrationEventPublisher(configuration);
            services.AddIntegrationEventReceiver(configuration, new IntegrationEventMapper());
        }

        private static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<B2CAuthenticationOptions>(configuration.GetSection("Authentication:AzureAd:B2C"));
            var serviceProvider = services.BuildServiceProvider();
            var authOptions = serviceProvider.GetService<IOptions<B2CAuthenticationOptions>>();

            services.AddAuthentication(Constants.CustomerAuthenticationScheme)
                .AddJwtBearer(Constants.CustomerAuthenticationScheme, options =>
                {
                    options.MetadataAddress = $"{authOptions.Value.Authority}/.well-known/openid-configuration?p={authOptions.Value.Policy}";
                    options.Audience = authOptions.Value.Audience;

                    options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
                    {
                        OnTokenValidated = ctx =>
                        {
                            var nameClaim = ctx.Principal.FindFirst("name");
                            if (nameClaim != null)
                            {
                                var claimsIdentity = (ClaimsIdentity)ctx.Principal.Identity;
                                claimsIdentity.AddClaim(new Claim(ClaimTypes.Name, nameClaim.Value));
                            }
                            return Task.FromResult(0);
                        }
                    };
                });
        }
    }
}
