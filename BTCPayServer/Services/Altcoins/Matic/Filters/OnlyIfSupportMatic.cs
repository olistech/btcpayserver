#if ALTCOINS
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace BTCPayServer.Services.Altcoins.Matic.Filters
{
    public class OnlyIfSupportMaticAttribute : Attribute, IAsyncActionFilter
    {
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var options = context.HttpContext.RequestServices.GetService<BTCPayNetworkProvider>();
            if (!options.GetAll().OfType<MaticBTCPayNetwork>().Any())
            {
                context.Result = new NotFoundResult();
                return;
            }

            await next();
        }
    }
}
#endif