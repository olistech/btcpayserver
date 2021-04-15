#if ALTCOINS
using System.Net;
using System.Net.Http;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.HostedServices;
using BTCPayServer.Payments;
using BTCPayServer.Services.Altcoins.Avalanche.Payments;
using BTCPayServer.Services.Altcoins.Avalanche.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BTCPayServer.Services.Altcoins.Avalanche
{
    public static class AvalancheLikeExtensions
    {
        public  const string AvalancheInvoiceCheckHttpClient = "AvalancheCheck";
        public  const string AvalancheInvoiceCreateHttpClient = "AvalancheCreate";
        public static IServiceCollection AddAvalancheLike(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<AvalancheService>();
            serviceCollection.AddSingleton<IHostedService, AvalancheService>(provider => provider.GetService<AvalancheService>());
            serviceCollection.AddSingleton<AvalancheLikePaymentMethodHandler>();
            serviceCollection.AddSingleton<IPaymentMethodHandler>(provider => provider.GetService<AvalancheLikePaymentMethodHandler>());

            serviceCollection.AddSingleton<IUIExtension>(new UIExtension("Avalanche/StoreNavAvalancheExtension",  "store-nav"));
            serviceCollection.AddTransient<NoRedirectHttpClientHandler>();
            serviceCollection.AddSingleton<ISyncSummaryProvider, AvalancheSyncSummaryProvider>();
            serviceCollection.AddHttpClient(AvalancheInvoiceCreateHttpClient)
                .ConfigurePrimaryHttpMessageHandler<NoRedirectHttpClientHandler>();
            return serviceCollection;
        }
    }
    
    public class NoRedirectHttpClientHandler : HttpClientHandler
    {
        public NoRedirectHttpClientHandler()
        {
            this.AllowAutoRedirect = false;
        }
    }
}
#endif
