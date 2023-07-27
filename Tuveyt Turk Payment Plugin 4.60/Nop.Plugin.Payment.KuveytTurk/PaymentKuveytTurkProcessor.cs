using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Nop.Core.Domain.Directory;
using Nop.Core;
using Nop.Core.Infrastructure;
using Nop.Plugin.Payment.KuveytTurk.Models;
using Nop.Plugin.Payment.KuveytTurk.Services;
using Nop.Plugin.Payment.KuveytTurk.Validators;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Security;
using Nop.Web.Framework.Mvc.Filters;
using Ubiety.Dns.Core;
using System.Threading.Tasks;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payment.KuveytTurk.Components;

namespace Nop.Plugin.Payment.KuveytTurk
{
    public class PaymentKuveytTurkProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly ICurrencyService _currencyService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly IEncryptionService _encryptionService;
        private readonly ICustomerService _customerService;
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly INopFileProvider _nopFileProvider;
        private readonly KuveytTurkServices _kuveytTurkService;
        private readonly KuveytTurkPaymentSettings _kuveytTurkPaymentSettings;

        #endregion


        #region Ctor

        public PaymentKuveytTurkProcessor(CurrencySettings currencySettings,
            ICurrencyService currencyService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IPaymentService paymentService,
            ISettingService settingService,
            IWebHelper webHelper,
            IEncryptionService encryptionService,
            ICustomerService customerService,
            IWebHostEnvironment webHostEnvironment,
            INopFileProvider nopFileProvider,
            KuveytTurkServices kuveytTurkService,
            KuveytTurkPaymentSettings twoCheckoutPaymentSettings
            )
        {
            _currencySettings = currencySettings;
            _currencyService = currencyService;
            _httpContextAccessor = httpContextAccessor;
            _localizationService = localizationService;
            _paymentService = paymentService;
            _settingService = settingService;
            _webHelper = webHelper;
            _encryptionService = encryptionService;
            _customerService = customerService;
            _webHostEnvironment = webHostEnvironment;
            _nopFileProvider = nopFileProvider;
            //_kuveytTurkService = kuveytTurkService;
            //_kuveytTurkPaymentSettings = twoCheckoutPaymentSettings;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult
            {
                AllowStoringCreditCardNumber = true,
            };
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public async Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //Get payment details
            var creditCardName = _encryptionService.DecryptText(postProcessPaymentRequest.Order.CardName);
            var creditCardNumber = _encryptionService.DecryptText(postProcessPaymentRequest.Order.CardNumber);
            var creditCardExpirationYear = _encryptionService.DecryptText(postProcessPaymentRequest.Order.CardExpirationYear);
            var creditCardExpirationMonth = _encryptionService.DecryptText(postProcessPaymentRequest.Order.CardExpirationMonth);
            var creditCardCvv2 = _encryptionService.DecryptText(postProcessPaymentRequest.Order.CardCvv2);

            //Save details in an object
            var processPaymentRequest = new ProcessPaymentRequest
            {
                CreditCardName = creditCardName,
                CreditCardNumber = creditCardNumber,
                CreditCardExpireYear = Convert.ToInt32(creditCardExpirationYear),
                CreditCardExpireMonth = Convert.ToInt32(creditCardExpirationMonth),
                CreditCardCvv2 = creditCardCvv2,
                OrderGuid = postProcessPaymentRequest.Order.OrderGuid,
                OrderTotal = postProcessPaymentRequest.Order.OrderTotal,
            };

            //Convert data from ProcessPaymentRequest to Xml object
            var postData = _kuveytTurkService.GetDataAsXml(processPaymentRequest);
            //Send Xml object to url and get result
            var result = _kuveytTurkService.PostPaymentDataToUrl("https://boa.kuveytturk.com.tr/sanalposservice/Home/ThreeDModelPayGate", postData);

            //Create directory and save Html Code in it
            var file = _kuveytTurkService.PutHtmlCodeInFile(result);

            //Redirect to new file HTML page
            _httpContextAccessor.HttpContext.Response.Redirect($"{_webHelper.GetStoreLocation()}OrderPayments/{file}");
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return Task.FromResult(false);
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            return Task.FromResult(decimal.Zero);
            ;
            //return _paymentService.CalculateAdditionalFee(cart,
            //    _twoCheckoutPaymentSettings.AdditionalFee, _twoCheckoutPaymentSettings.AdditionalFeePercentage);
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public async Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public async Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            return new RefundPaymentResult { Errors = new[] { "Refund method not supported" } };
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            return Task.FromResult(new VoidPaymentResult { Errors = new[] { "Void method not supported" } });
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            return Task.FromResult(new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } });

        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return Task.FromResult(new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } });
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //do not allow reposting (it can take up to several hours until your order is reviewed
            return Task.FromResult(false);
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public async Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            var warnings = new List<string>();

            //validate
            var validator = new PaymnetInfoValidators(_localizationService);
            var model = new PaymentInfoModel
            {
                CardholderName = form["CardholderName"],
                CardNumber = form["CardNumber"],
                CardCode = form["CardCode"],
                ExpireMonth = form["ExpireMonth"],
                ExpireYear = form["ExpireYear"]
            };
            var validationResult = validator.Validate(model);
            if (!validationResult.IsValid)
                warnings.AddRange(validationResult.Errors.Select(error => error.ErrorMessage));

            return warnings;
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public async Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            return new ProcessPaymentRequest
            {
                CreditCardType = form["CreditCardType"],
                CreditCardName = form["CardholderName"],
                CreditCardNumber = form["CardNumber"],
                CreditCardExpireMonth = int.Parse(form["ExpireMonth"]),
                CreditCardExpireYear = int.Parse(form["ExpireYear"]),
                CreditCardCvv2 = form["CardCode"]
            };
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentKuveytTurk/Configure";
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public Type GetPublicViewComponentName()
        {
            return typeof(PaymentKuveytTurkViewComponent);
        }

        /// <summary>
        /// Install plugin
        /// </summary>
        public override async Task InstallAsync()
        {
            //settings
            _settingService.SaveSetting(new KuveytTurkPaymentSettings()
            {
                //UseSandbox = true,
                //UseMd5Hashing = true
            });

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}CustomerId", "Account No");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}CustomerId.Hint", "Enter account no.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}MerchantId", "Merchant No");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}MerchantId.Hint", "Enter merchant no.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}UserName", "User Name");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}UserName.Hint", "Enter User Name.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}Password", "Password");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}Password.Hint", "Enter Password.");

            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}PaymentMethodDescription", "You will be redirected to pay after complete the order.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}RedirectionTip", "You will be redirected to pay after complete the order.");

            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}PaymentDone", "Payment have been done!");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}KartiVerenBankayiAraLim", "Call the bank.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}GecersizUyeIsyeri", "Invalid Member Merchant.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}KartaElKoyunuz", "Not working card.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}IslemOnaylanmadi", "The transaction has not been approved.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}VipIslemIcinOnayVerildi", "Approved for VIP Operation.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}GecersizIslem", "No Transaction.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}GecersizIslemTutari", "No Transaction Amount.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}GecersizKartNumarasi", "Invalid Card Number.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}KartVerenBankaTanimsiz", "Card Issuer Bank Undefined.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}VadeSonuGecmisKartaElKoy", "Seize Card Overdue.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}SahtekarlikKartaelKoyunuz", "Falsify Your Card.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}KisitliKartKartaElKoyunuz", "Card limit exceeded.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}GuvenligiUyarinizKartaElKoyunuz", "The card was rejected for security reasons.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}KayipKartKartaElKoy", "Lost card.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}CalintiKartKartaElKoy", "Stolen card.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}BakiyesiKrediLimitiYetersiz", "Balance Credit Limit Insufficient.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}DovizHesabiBulunamasi", "No Exchange Account Found.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}VadeSonuGecmisKart", "Expiry Card.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}HataliKartSifresi", "Wrong Card Password.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}KartTanimliDegil", "Card Not Defined.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}IslemTipineIzinYok", "No Transaction Type.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}IslemTipiTerminaleKapali", "Operation Type Closed to Terminal.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}SahtekarlikSuphesi", "Suspicion of Fraud.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}ParaCekmeTutarLimitiAsild", "Withdrawal Amount Limit Exceeded.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}KisitlanmisKart", "Restricted Card.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}GuvenlikIhlali", "Security Violation.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}ParaÇekmeAdetLimitiAsildi", "Withdrawal Number Limit Exceeded.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}IslemiReddedinizGuvenligi", "Transaction Rejected Security.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}BuHesaptaHicbirIslemYapila", "No Transactions Made On This Account.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}TanimsizSube", "Undefined Branch.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}SifreDenemeSayisiAsildi", "Number of Enter Password Excided.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}SifrelerUyusmuyorKey", "Encryption Key is not Match.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}SifreScriptTalebiReddedildi", "Password Script Request Denied.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}SifreGuvenilirBulanmadi", "Security Password Not Found.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}ARQCKontroluBasarisiz", "ARQC Control Failed.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}SifreDegisikligi/YuklemeOnay", "Password Change/Download Confirmation.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}IslemSupheliTamamlandiKontrol", "Operation Suspicious Completed Check.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}EkKartIleBuIslemYapilmaz", "This Operation Cannot Be Done By Additional Card.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}GunSonuDevamEdiyor", "End of Day Calculating Continues.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}KartiVerenBankaHizmetdisi", "Bank Issuing Card Out of Service.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}KartVerenBankaTanimliDegil", "Unknown Bank Card.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}SistemArizali", "Problem in system.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}IPAdresiTanimliDegildir", "Your IP is not define.");
            await _localizationService.AddOrUpdateLocaleResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}OtherError", "Unknown Error.");

            await base.InstallAsync();
        }

        /// <summary>
        /// Uninstall plugin
        /// </summary>
        public override async Task UninstallAsync()
        {
            //settings
            await _settingService.DeleteSettingAsync<KuveytTurkPaymentSettings>();

            //locales
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}CustomerId");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}CustomerId.Hint");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}MerchantId");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}MerchantId.Hint");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}UserName");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}UserName.Hint");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}Password");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}Password.Hint");

            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}PaymentMethodDescription");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}RedirectionTip");

            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}PaymentDone");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}KartiVerenBankayiAraLim");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}GecersizUyeIsyeri");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}KartaElKoyunuz");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}IslemOnaylanmadi");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}VipIslemIcinOnayVerildi");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}GecersizIslem");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}GecersizIslemTutari");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}GecersizKartNumarasi");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}KartVerenBankaTanimsiz");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}VadeSonuGecmisKartaElKoy");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}SahtekarlikKartaelKoyunuz");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}KisitliKartKartaElKoyunuz");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}GuvenligiUyarinizKartaElKoyunuz");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}KayipKartKartaElKoy");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}CalintiKartKartaElKoy");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}BakiyesiKrediLimitiYetersiz");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}DovizHesabiBulunamasi");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}VadeSonuGecmisKart");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}HataliKartSifresi");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}KartTanimliDegil");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}IslemTipineIzinYok");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}IslemTipiTerminaleKapali");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}SahtekarlikSuphesi");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}ParaCekmeTutarLimitiAsild");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}KisitlanmisKart");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}GuvenlikIhlali");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}ParaÇekmeAdetLimitiAsildi");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}IslemiReddedinizGuvenligi");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}BuHesaptaHicbirIslemYapila");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}TanimsizSube");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}SifreDenemeSayisiAsildi");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}SifrelerUyusmuyorKey");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}SifreScriptTalebiReddedildi");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}SifreGuvenilirBulanmadi");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}ARQCKontroluBasarisiz");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}SifreDegisikligi/YuklemeOnay");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}IslemSupheliTamamlandiKontrol");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}EkKartIleBuIslemYapilmaz");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}GunSonuDevamEdiyor");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}KartiVerenBankaHizmetdisi");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}KartVerenBankaTanimliDegil");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}SistemArizali");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}IPAdresiTanimliDegildir");
            await _localizationService.DeleteLocaleResourcesAsync($"{KuveytTurkDefaults.LocalizationStringStart}OtherError");

            await base.UninstallAsync();
        }

        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture => true;

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund => true;

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund => true;

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid => true;

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.Manual;

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Standard;

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => false;

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            return await _localizationService.GetResourceAsync("Plugins.Payment.KuveytTurk.PaymentMethodDescription");
        }

        public Type GetPublicViewComponent()
        {
            throw new NotImplementedException();
        }


        #endregion
    }
}
