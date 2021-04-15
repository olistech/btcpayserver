#if ALTCOINS
using System.Collections.Generic;
using System.Linq;
using System;


namespace BTCPayServer
{
    public static class AvalancheExtensions
    {
        
        public static IEnumerable<string> GetAllAvalancheSubChains(this BTCPayNetworkProvider networkProvider, BTCPayNetworkProvider unfiltered)
        {
            var ethBased = networkProvider.GetAll().OfType<AvalancheBTCPayNetwork>();
            var chainId = ethBased.Select(network => network.ChainId).Distinct();

            return unfiltered.GetAll().OfType<AvalancheBTCPayNetwork>()
                .Where(network => chainId.Contains(network.ChainId))
                .Select(network => network.CryptoCode.ToUpperInvariant());

        }
    }
}
#endif
