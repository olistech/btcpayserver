#if ALTCOINS
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BTCPayServer.Data;
using BTCPayServer.Events;
using BTCPayServer.HostedServices;
using BTCPayServer.Logging;
using BTCPayServer.Services.Altcoins.Avalanche.Payments;
using BTCPayServer.Services.Stores;
using BTCPayServer.Services.Altcoins.Avalanche.Configuration;
using BTCPayServer.Services.Altcoins.Avalanche.UI;
using BTCPayServer.Services.Invoices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NBitcoin;

namespace BTCPayServer.Services.Altcoins.Avalanche.Services
{
    public class AvalancheService : EventHostedServiceBase
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly EventAggregator _eventAggregator;
        private readonly StoreRepository _storeRepository;
        private readonly BTCPayNetworkProvider _btcPayNetworkProvider;
        private readonly SettingsRepository _settingsRepository;
        private readonly InvoiceRepository _invoiceRepository;
        private readonly IConfiguration _configuration;
        private readonly Dictionary<int, AvalancheWatcher> _chainHostedServices = new Dictionary<int, AvalancheWatcher>();

        private readonly Dictionary<int, CancellationTokenSource> _chainHostedServiceCancellationTokenSources =
            new Dictionary<int, CancellationTokenSource>();

        public AvalancheService(
            IHttpClientFactory httpClientFactory,
            EventAggregator eventAggregator,
            StoreRepository storeRepository,
            BTCPayNetworkProvider btcPayNetworkProvider,
            SettingsRepository settingsRepository,
            InvoiceRepository invoiceRepository,
            IConfiguration configuration) : base(
            eventAggregator)
        {
            _httpClientFactory = httpClientFactory;
            _eventAggregator = eventAggregator;
            _storeRepository = storeRepository;
            _btcPayNetworkProvider = btcPayNetworkProvider;
            _settingsRepository = settingsRepository;
            _invoiceRepository = invoiceRepository;
            _configuration = configuration;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            var chainIds = _btcPayNetworkProvider.GetAll().OfType<AvalancheBTCPayNetwork>()
                .Select(network => network.ChainId).Distinct().ToList();
            if (!chainIds.Any())
            {
                return;
            }

            await base.StartAsync(cancellationToken);
            _ = Task.Run(async () =>
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    _eventAggregator.Publish(new CheckWatchers());
                    await Task.Delay(IsAllAvailable() ? TimeSpan.FromDays(1) : TimeSpan.FromSeconds(5),
                        cancellationToken);
                }
            }, cancellationToken);
        }

        private static bool First = true;

        private async Task LoopThroughChainWatchers(CancellationToken cancellationToken)
        {
            var chainIds = _btcPayNetworkProvider.GetAll().OfType<AvalancheBTCPayNetwork>()
                .Select(network => network.ChainId).Distinct().ToList();
            foreach (var chainId in chainIds)
            {
                try
                {
                    var settings = await _settingsRepository.GetSettingAsync<AvalancheLikeConfiguration>(
                        AvalancheLikeConfiguration.SettingsKey(chainId));
                    if (settings is null || string.IsNullOrEmpty(settings.Web3ProviderUrl))
                    {
                        var val = _configuration.GetValue<string>($"chain{chainId}_web3", null);
                        var valUser = _configuration.GetValue<string>($"chain{chainId}_web3_user", null);
                        var valPass = _configuration.GetValue<string>($"chain{chainId}_web3_password", null);
                        if (val != null && First)
                        {
                            Logs.PayServer.LogInformation($"Setting eth chain {chainId} web3 to {val}");
                            settings ??= new AvalancheLikeConfiguration()
                            {
                                ChainId = chainId,
                                Web3ProviderUrl = val,
                                Web3ProviderPassword = valPass,
                                Web3ProviderUsername = valUser
                            };
                            await _settingsRepository.UpdateSetting(settings,
                                AvalancheLikeConfiguration.SettingsKey(chainId));
                        }
                    }

                    var currentlyRunning = _chainHostedServices.ContainsKey(chainId);
                    if (!currentlyRunning || (currentlyRunning))
                    {
                        await HandleChainWatcher(settings, cancellationToken);
                    }
                }
                catch (Exception)
                {
                    // ignored
                }
            }

            First = false;
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var chainHostedService in _chainHostedServices.Values)
            {
                _ = chainHostedService.StopAsync(cancellationToken);
            }

            return base.StopAsync(cancellationToken);
        }

        protected override void SubscribeToEvents()
        {
            base.SubscribeToEvents();

            Subscribe<ReserveAvalancheAddress>();
            Subscribe<SettingsChanged<AvalancheLikeConfiguration>>();
            Subscribe<CheckWatchers>();
        }

        protected override async Task ProcessEvent(object evt, CancellationToken cancellationToken)
        {
            if (evt is ReserveAvalancheAddress reserveAvalancheAddress)
            {
                await HandleReserveNextAddress(reserveAvalancheAddress);
            }

            if (evt is SettingsChanged<AvalancheLikeConfiguration> settingsChangedEthConfig)
            {
                await HandleChainWatcher(settingsChangedEthConfig.Settings, cancellationToken);
            }

            if (evt is CheckWatchers)
            {
                await LoopThroughChainWatchers(cancellationToken);
            }

            await base.ProcessEvent(evt, cancellationToken);
        }

        private async Task HandleChainWatcher(AvalancheLikeConfiguration avalancheLikeConfiguration,
            CancellationToken cancellationToken)
        {
            if (avalancheLikeConfiguration is null)
            {
                return;
            }

            if (_chainHostedServiceCancellationTokenSources.ContainsKey(avalancheLikeConfiguration.ChainId))
            {
                _chainHostedServiceCancellationTokenSources[avalancheLikeConfiguration.ChainId].Cancel();
                _chainHostedServiceCancellationTokenSources.Remove(avalancheLikeConfiguration.ChainId);
            }

            if (_chainHostedServices.ContainsKey(avalancheLikeConfiguration.ChainId))
            {
                await _chainHostedServices[avalancheLikeConfiguration.ChainId].StopAsync(cancellationToken);
                _chainHostedServices.Remove(avalancheLikeConfiguration.ChainId);
            }

            if (!string.IsNullOrWhiteSpace(avalancheLikeConfiguration.Web3ProviderUrl))
            {
                var cts = new CancellationTokenSource();
                _chainHostedServiceCancellationTokenSources.AddOrReplace(avalancheLikeConfiguration.ChainId, cts);
                _chainHostedServices.AddOrReplace(avalancheLikeConfiguration.ChainId,
                    new AvalancheWatcher(avalancheLikeConfiguration.ChainId, avalancheLikeConfiguration,
                        _btcPayNetworkProvider, _eventAggregator, _invoiceRepository));
                await _chainHostedServices[avalancheLikeConfiguration.ChainId].StartAsync(CancellationTokenSource
                    .CreateLinkedTokenSource(cancellationToken, cts.Token).Token);
            }
        }

        private async Task HandleReserveNextAddress(ReserveAvalancheAddress reserveAvalancheAddress)
        {
            var store = await _storeRepository.FindStore(reserveAvalancheAddress.StoreId);
            var avalancheSupportedPaymentMethod = store.GetSupportedPaymentMethods(_btcPayNetworkProvider)
                .OfType<AvalancheSupportedPaymentMethod>()
                .SingleOrDefault(method => method.PaymentId.CryptoCode == reserveAvalancheAddress.CryptoCode);
            if (avalancheSupportedPaymentMethod == null)
            {
                _eventAggregator.Publish(new ReserveAvalancheAddressResponse()
                {
                    OpId = reserveAvalancheAddress.OpId, Failed = true
                });
                return;
            }

            avalancheSupportedPaymentMethod.CurrentIndex++;
            var address = avalancheSupportedPaymentMethod.GetWalletDerivator()?
                .Invoke((int)avalancheSupportedPaymentMethod.CurrentIndex);

            if (string.IsNullOrEmpty(address))
            {
                _eventAggregator.Publish(new ReserveAvalancheAddressResponse()
                {
                    OpId = reserveAvalancheAddress.OpId, Failed = true
                });
                return;
            }
            store.SetSupportedPaymentMethod(avalancheSupportedPaymentMethod.PaymentId,
                avalancheSupportedPaymentMethod);
            await _storeRepository.UpdateStore(store);
            _eventAggregator.Publish(new ReserveAvalancheAddressResponse()
            {
                Address = address,
                Index = avalancheSupportedPaymentMethod.CurrentIndex,
                CryptoCode = avalancheSupportedPaymentMethod.CryptoCode,
                OpId = reserveAvalancheAddress.OpId,
                StoreId = reserveAvalancheAddress.StoreId,
                XPub = avalancheSupportedPaymentMethod.XPub
            });
        }

        public async Task<ReserveAvalancheAddressResponse> ReserveNextAddress(ReserveAvalancheAddress address)
        {
            address.OpId = string.IsNullOrEmpty(address.OpId) ? Guid.NewGuid().ToString() : address.OpId;
            var tcs = new TaskCompletionSource<ReserveAvalancheAddressResponse>();
            var subscription = _eventAggregator.Subscribe<ReserveAvalancheAddressResponse>(response =>
            {
                if (response.OpId == address.OpId)
                {
                    tcs.SetResult(response);
                }
            });
            _eventAggregator.Publish(address);

            if (tcs.Task.Wait(TimeSpan.FromSeconds(60)))
            {
                subscription?.Dispose();
                return await tcs.Task;
            }

            subscription?.Dispose();
            return null;
        }

        public class CheckWatchers
        {
            public override string ToString()
            {
                return "";
            }
        }

        public class ReserveAvalancheAddressResponse
        {
            public string StoreId { get; set; }
            public string CryptoCode { get; set; }
            public string Address { get; set; }
            public long Index { get; set; }
            public string OpId { get; set; }
            public string XPub { get; set; }
            public bool Failed { get; set; }

            public override string ToString()
            {
                return $"Reserved {CryptoCode} address {Address} for store {StoreId}";
            }
        }

        public class ReserveAvalancheAddress
        {
            public string StoreId { get; set; }
            public string CryptoCode { get; set; }
            public string OpId { get; set; }

            public override string ToString()
            {
                return $"Reserving {CryptoCode} address for store {StoreId}";
            }
        }

        public bool IsAllAvailable()
        {
            return _btcPayNetworkProvider.GetAll().OfType<AvalancheBTCPayNetwork>()
                .All(network => IsAvailable(network.CryptoCode, out _));
        }

        public bool IsAvailable(string networkCryptoCode, out string error)
        {
            error = null;
            var chainId = _btcPayNetworkProvider.GetNetwork<AvalancheBTCPayNetwork>(networkCryptoCode)?.ChainId;
            if (chainId != null && _chainHostedServices.TryGetValue(chainId.Value, out var watcher))
            {
                error = watcher.GlobalError;
                return string.IsNullOrEmpty(watcher.GlobalError);
            }
            return false;
        }
    }
}
#endif
