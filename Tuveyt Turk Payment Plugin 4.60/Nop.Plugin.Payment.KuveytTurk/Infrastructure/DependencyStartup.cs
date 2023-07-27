using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Autofac;
using LinqToDB.Tools;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Nop.Core.Configuration;
using Nop.Core.Infrastructure;
using Nop.Data;
using Nop.Plugin.Payment.KuveytTurk.Services;
using Orleans;

namespace Nop.Plugin.Payment.KuveytTurk.Infrastructure
{
    public class DependencyStartup : INopStartup
    {
        public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            services.AddScoped<KuveytTurkServices>();
        }

        public void Configure(IApplicationBuilder application)
        {
            //do nothing
        }

        public int Order => 1;
    }

}
