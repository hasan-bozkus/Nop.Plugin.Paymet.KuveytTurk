namespace Nop.Plugin.Payment.KuveytTurk
{
    internal class RefundRequestModel
    {
        public string MerchantId { get; set; }
        public string MerchantOrderId { get; set; }
        public decimal Amount { get; set; }
        public string CurrencyCode { get; set; }
        public object Hash { get; set; }
    }
}