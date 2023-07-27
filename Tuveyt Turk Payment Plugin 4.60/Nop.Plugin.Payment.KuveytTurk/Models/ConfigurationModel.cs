using System.ComponentModel.DataAnnotations;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using Octopus.Client.Model;

namespace Nop.Plugin.Payment.KuveytTurk.Models
{
    public record ConfigurationModel : BaseNopModel
    {
        [NopResourceDisplayName("Plugins.Payments.KuveytTurk.CustomerId")]
        public string CustomerId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.KuveytTurk.MerchantId")]
        public string MerchantId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.KuveytTurk.UserName")]
        public string UserName { get; set; }

        [NopResourceDisplayName("Plugins.Payments.KuveytTurk.Password")]
        [DataType(DataType.Password)]
        [Trim]
        public string Password { get; set; }
    }
}
