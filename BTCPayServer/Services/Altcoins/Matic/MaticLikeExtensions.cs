#if ALTCOINS
using System.Net;
using System.Net.Http;
using BTCPayServer.Abstractions.Contracts;
using BTCPayServer.Abstractions.Services;
using BTCPayServer.HostedServices;
using BTCPayServer.Payments;
using BTCPayServer.Services.Altcoins.Matic.Payments;
using BTCPayServer.Services.Altcoins.Matic.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BTCPayServer.Services.Altcoins.Matic
{
    public static class MaticLikeExtensions
    {
        public  const string MaticInvoiceCheckHttpClient = "MaticCheck";
        public  const string MaticInvoiceCreateHttpClient = "MaticCreate";
        public static IServiceCollection AddMaticLike(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddSingleton<MaticService>();
            serviceCollection.AddSingleton<IHostedService, MaticService>(provider => provider.GetService<MaticService>());
            serviceCollection.AddSingleton<MaticLikePaymentMethodHandler>();
            serviceCollection.AddSingleton<IPaymentMethodHandler>(provider => provider.GetService<MaticLikePaymentMethodHandler>());

            serviceCollection.AddSingleton<IUIExtension>(new UIExtension("Matic/StoreNavMaticExtension",  "store-nav"));
            serviceCollection.AddTransient<NoRedirectHttpClientHandler>();
            serviceCollection.AddSingleton<ISyncSummaryProvider, MaticSyncSummaryProvider>();
            serviceCollection.AddHttpClient(MaticInvoiceCreateHttpClient)
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
