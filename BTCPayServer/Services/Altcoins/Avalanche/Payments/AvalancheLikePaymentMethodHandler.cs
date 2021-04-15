#if ALTCOINS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Logging;
using BTCPayServer.Models;
using BTCPayServer.Models.InvoicingModels;
using BTCPayServer.Payments;
using BTCPayServer.Rating;
using BTCPayServer.Services.Altcoins.Avalanche.Services;
using BTCPayServer.Services.Invoices;
using BTCPayServer.Services.Rates;
using NBitcoin;

namespace BTCPayServer.Services.Altcoins.Avalanche.Payments
{
    public class
        AvalancheLikePaymentMethodHandler : PaymentMethodHandlerBase<AvalancheSupportedPaymentMethod,
            AvalancheBTCPayNetwork>
    {
        private readonly BTCPayNetworkProvider _networkProvider;
        private readonly AvalancheService _avalancheService;

        public AvalancheLikePaymentMethodHandler(BTCPayNetworkProvider networkProvider, AvalancheService avalancheService)
        {
            _networkProvider = networkProvider;
            _avalancheService = avalancheService;
        }

        public override PaymentType PaymentType => AvalanchePaymentType.Instance;

        public override async Task<IPaymentMethodDetails> CreatePaymentMethodDetails(InvoiceLogs logs,
            AvalancheSupportedPaymentMethod supportedPaymentMethod, PaymentMethod paymentMethod,
            StoreData store, AvalancheBTCPayNetwork network, object preparePaymentObject)
        {
            if (!_avalancheService.IsAvailable(network.CryptoCode, out var error))
                throw new PaymentMethodUnavailableException(error??$"Not configured yet");
            var invoice = paymentMethod.ParentEntity;
            if (!(preparePaymentObject is Prepare ethPrepare)) throw new ArgumentException();
            var address = await ethPrepare.ReserveAddress(invoice.Id);
            if (address is null || address.Failed)
            {
                throw new PaymentMethodUnavailableException($"could not generate address");
            }
            
            return new AvalancheLikeOnChainPaymentMethodDetails()
            {
                DepositAddress = address.Address, Index = address.Index, XPub = address.XPub
            };
        }

        public override object PreparePayment(AvalancheSupportedPaymentMethod supportedPaymentMethod, StoreData store,
            BTCPayNetworkBase network)
        {
            return new Prepare()
            {
                ReserveAddress = s =>
                    _avalancheService.ReserveNextAddress(
                        new AvalancheService.ReserveAvalancheAddress()
                        {
                            StoreId = store.Id, CryptoCode = network.CryptoCode
                        })
            };
        }

        class Prepare
        {
            public Func<string, Task<AvalancheService.ReserveAvalancheAddressResponse>> ReserveAddress;
        }

        public override void PreparePaymentModel(PaymentModel model, InvoiceResponse invoiceResponse,
            StoreBlob storeBlob, IPaymentMethod paymentMethod)
        {
            var paymentMethodId = paymentMethod.GetId();
            var cryptoInfo = invoiceResponse.CryptoInfo.First(o => o.GetpaymentMethodId() == paymentMethodId);
            var network = _networkProvider.GetNetwork<AvalancheBTCPayNetwork>(model.CryptoCode);
            model.PaymentMethodName = GetPaymentMethodName(network);
            model.CryptoImage = GetCryptoImage(network);
            model.InvoiceBitcoinUrl = "";
            model.InvoiceBitcoinUrlQR = cryptoInfo.Address;
        }

        public override string GetCryptoImage(PaymentMethodId paymentMethodId)
        {
            var network = _networkProvider.GetNetwork<AvalancheBTCPayNetwork>(paymentMethodId.CryptoCode);
            return GetCryptoImage(network);
        }

        public override string GetPaymentMethodName(PaymentMethodId paymentMethodId)
        {
            var network = _networkProvider.GetNetwork<AvalancheBTCPayNetwork>(paymentMethodId.CryptoCode);
            return GetPaymentMethodName(network);
        }

        public override IEnumerable<PaymentMethodId> GetSupportedPaymentMethods()
        {
            return _networkProvider.GetAll().OfType<AvalancheBTCPayNetwork>()
                .Select(network => new PaymentMethodId(network.CryptoCode, PaymentType));
        }

        public override CheckoutUIPaymentMethodSettings GetCheckoutUISettings()
        {
            return new CheckoutUIPaymentMethodSettings()
            {
                ExtensionPartial = "Avalanche/AvalancheLikeMethodCheckout",
                CheckoutBodyVueComponentName = "AvalancheLikeMethodCheckout",
                CheckoutHeaderVueComponentName = "AvalancheLikeMethodCheckoutHeader",
                NoScriptPartialName = "Bitcoin_Lightning_LikeMethodCheckoutNoScript"
            };
        }

        private string GetCryptoImage(AvalancheBTCPayNetwork network)
        {
            return network.CryptoImagePath;
        }


        private string GetPaymentMethodName(AvalancheBTCPayNetwork network)
        {
            return $"{network.DisplayName}";
        }
    }
}
#endif
