using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nop.Plugin.Payment.KuveytTurk
{
    /// <summary>
    /// Represents plugin constants
    /// </summary>
    public class KuveytTurkDefaults
    {
        /// <summary>
        /// Gets a name of the view component to display payment info in public store
        /// </summary>
        public const string PAYMENT_INFO_VIEW_COMPONENT_NAME = "PaymentKuveytTurk";

        /// <summary>
        /// Gets payment method system name
        /// </summary>
        public static string SystemName => "Payment.KuveytTurk";

        /// <summary>
        /// Gets IPN handler route name
        /// </summary>
        public static string Payment => "Plugin.Payment.KuveytTurk.Payment";
        public static string Fail => "Plugin.Payment.KuveytTurk.Fail";
        public static string Approval => "Plugin.Payment.KuveytTurk.Approval";
        public static string SendApprove => "Plugin.Payment.KuveytTurk.SendApprove";

        public static string OrderPaymentsDirectory => "OrderPayments";

        public static string LocalizationStringStart => "Plugins.Payment.KuveytTurk.";
    }
}
