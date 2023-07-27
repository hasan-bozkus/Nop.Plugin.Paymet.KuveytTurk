using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Newtonsoft.Json;
using Nop.Plugin.Payment.KuveytTurk.Models;
using Nop.Plugin.Payment.KuveytTurk.Services;
using Nop.Services.Catalog;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using OfficeOpenXml.Style;
using System.Threading.Tasks;
using Nop.Core;
using Nop.Core.Domain.Payments;

namespace Nop.Plugin.Payment.KuveytTurk.Contorllers
{
    public class PaymentKuveytTurkController : BasePaymentController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly INotificationService _notificationService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPermissionService _permissionService;
        private readonly ISettingService _settingService;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly KuveytTurkServices _kuveytTurkService;
        private readonly KuveytTurkPaymentSettings _kuveytTurkPaymentSettings;

        #endregion

        #region Ctor

        public PaymentKuveytTurkController(ILocalizationService localizationService,
            INotificationService notificationService,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IPermissionService permissionService,
            ISettingService settingService,
            IWebHelper webHelper,
            IWorkContext workContext,
            KuveytTurkServices kuveytTurkService,
            KuveytTurkPaymentSettings twoCheckoutPaymentSettings)
        {
            _localizationService = localizationService;
            _notificationService = notificationService;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _permissionService = permissionService;
            _settingService = settingService;
            _webHelper = webHelper;
            _workContext = workContext;
            _kuveytTurkService = kuveytTurkService;
            _kuveytTurkPaymentSettings = twoCheckoutPaymentSettings;
        }

        #endregion

        #region Methods
        /// <summary>
        /// Run this action if anything goes wronge
        /// </summary>
        /// <returns>Redirect to ShoppingCart</returns>
        public async Task<IActionResult> Fail()
        {
            //Get VPosTransactionResponseContract model from Request
            var model = _kuveytTurkService.GetVPosTransactionResponseContract(Request.Form["AuthenticationResponse"]);

            //Define err variable
            var err = _localizationService.GetResourceAsync(model.ResponseCode);

            //Send warning notification to user about error message
            _notificationService.WarningNotification($"{model.ResponseCode} - {err}({model.ResponseMessage})");

            //Repeate the order then delete it
            var order =await _orderService.GetOrderByGuidAsync(Guid.Parse(model.MerchantOrderId));
            if (order == null || order.Deleted || _workContext.GetCurrentCustomerAsync().Id != order.CustomerId)
                return Challenge();
           await  _orderProcessingService.ReOrderAsync(order);
            await _orderService.DeleteOrderAsync(order);

            //Delete temp files of this customer in OrderPayments directory
            _kuveytTurkService.ClearOrderPaymentsFiles(order.CustomerId);

            //Redirect to ShoppingCart
            return RedirectToRoute("ShoppingCart");
        }

        /// <summary>
        /// Run this action if every thing goes write
        /// </summary>
        /// <returns></returns>
        public async Task<IActionResult> SendApprove()
        {
            //Get VPosTransactionResponseContract model from Request
            var model =  _kuveytTurkService.GetVPosTransactionResponseContract(Request.Form["AuthenticationResponse"]);

            //Get order details
            var order =await _orderService.GetOrderByGuidAsync(new Guid(model.MerchantOrderId));

            //Save payment details in variables
            var merchantOrderId = model.MerchantOrderId;
            var amount = model.VPosMessage.Amount;
            var mD = model.MD;
            var customerId = _kuveytTurkPaymentSettings.CustomerId; //Müsteri Numarasi
            var merchantId = _kuveytTurkPaymentSettings.MerchantId; //Magaza Kodu
            var userName = _kuveytTurkPaymentSettings.UserName; //api rollü kullanici adı

            //Hash some data in one string result
            SHA1 sha = new SHA1CryptoServiceProvider();
            var hashedPassword = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(_kuveytTurkPaymentSettings.Password)));
            var hashstr = merchantId + merchantOrderId + amount + userName + hashedPassword;
            var hashbytes = System.Text.Encoding.GetEncoding("ISO-8859-9").GetBytes(hashstr);
            var inputbytes = sha.ComputeHash(hashbytes);
            var hashData = Convert.ToBase64String(inputbytes);

            //Generate XML code to send it to ProvisionGate
            var postData =
                "<KuveytTurkVPosMessage xmlns:xsi='http://www.w3.org/2001/XMLSchema-instance' xmlns:xsd='http://www.w3.org/2001/XMLSchema'>" +
                "<APIVersion>1.0.0</APIVersion>" +
                "<HashData>" + hashData + "</HashData>" +
                "<MerchantId>" + merchantId + "</MerchantId>" +
                "<CustomerId>" + customerId + "</CustomerId>" +
                "<UserName>" + userName + "</UserName>" +
                "<CurrencyCode>0949</CurrencyCode>" +
                "<TransactionType>Sale</TransactionType>" +
                "<InstallmentCount>0</InstallmentCount>" +
                "<Amount>" + amount + "</Amount>" +
                "<MerchantOrderId>" + merchantOrderId + "</MerchantOrderId>" +
                "<TransactionSecurity>3</TransactionSecurity>" +
                "<KuveytTurkVPosAdditionalData>" +
                "<AdditionalData>" +
                "<Key>MD</Key>" +
                "<Data>" + mD + "</Data>" +
                "</AdditionalData>" +
                "</KuveytTurkVPosAdditionalData>" +
                "</KuveytTurkVPosMessage>";

            var responseString = _kuveytTurkService.PostPaymentDataToUrl("https://boa.kuveytturk.com.tr/sanalposservice/Home/ThreeDModelProvisionGate", postData);

            var modelRes = _kuveytTurkService.GetVPosTransactionResponseContract(responseString);

            if (modelRes.ResponseCode == "00")
            {
                 order.PaymentStatus = PaymentStatus.Paid;
                 await _orderService.UpdateOrderAsync(order);
               _notificationService.SuccessNotification(await _localizationService.GetResourceAsync($"{KuveytTurkDefaults.LocalizationStringStart}PaymentDone"));

                return Redirect($"{_webHelper.GetStoreLocation()}");
            }

            var err = _kuveytTurkService.GetErrorMessage(modelRes.ResponseCode);
            _notificationService.WarningNotification($"{modelRes.ResponseCode} - {err}({model.ResponseMessage})");

            //Repeate the order then delete it
           
            if (order == null || order.Deleted || _workContext.GetCurrentCustomerAsync().Id != order.CustomerId)
                return Challenge();
           await _orderProcessingService.ReOrderAsync(order);
           await _orderService.DeleteOrderAsync(order);

            return Redirect("/");
        }

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var model = new ConfigurationModel
            {
                CustomerId = _kuveytTurkPaymentSettings.CustomerId,
                MerchantId = _kuveytTurkPaymentSettings.MerchantId,
                UserName = _kuveytTurkPaymentSettings.UserName,
                Password = _kuveytTurkPaymentSettings.Password,
            };
            //C:\Users\HASAN\Desktop\nopCommerce_4.60.2_Source\src\Presentation\Nop.Web\Plugins\Payment.KuveytTurk\Views\Configure.cshtml
            return View("~/Plugins/Payment.KuveytTurk/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        //[AdminAntiForgery]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return await Configure();

            //save settings
            _kuveytTurkPaymentSettings.CustomerId = model.CustomerId;
            _kuveytTurkPaymentSettings.MerchantId = model.MerchantId;
            _kuveytTurkPaymentSettings.UserName = model.UserName;
            _kuveytTurkPaymentSettings.Password = model.Password;
            await _settingService.SaveSettingAsync(_kuveytTurkPaymentSettings);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return await Configure();
        }

        #endregion
    }
}
