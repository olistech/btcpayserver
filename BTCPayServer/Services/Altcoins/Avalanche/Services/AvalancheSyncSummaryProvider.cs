#if ALTCOINS
using BTCPayServer.Abstractions.Contracts;

namespace BTCPayServer.Services.Altcoins.Avalanche.Services
{
    public class AvalancheSyncSummaryProvider : ISyncSummaryProvider
    {
        private readonly AvalancheService _service;

        public AvalancheSyncSummaryProvider(AvalancheService avalancheService)
        {
            _service = avalancheService;
        }

        public bool AllAvailable()
        {
            return _service.IsAllAvailable();
        }

        public string Partial { get; } = "Avalanche/AvalancheSyncSummary";
    }
}
#endif
