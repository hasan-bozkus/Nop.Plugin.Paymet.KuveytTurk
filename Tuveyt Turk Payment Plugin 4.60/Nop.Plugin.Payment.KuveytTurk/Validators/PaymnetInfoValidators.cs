using System;
using System.Threading.Tasks;
using FluentValidation;
using Nop.Plugin.Payment.KuveytTurk.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Payment.KuveytTurk.Validators
{
    public partial class PaymnetInfoValidators : BaseNopValidator<PaymentInfoModel>
    {
        private ILocalizationService _localizationService;

        public PaymnetInfoValidators(ILocalizationService localizationService)
        {
            _localizationService = localizationService;
        }

        public async Task PaymnetInfoValidatorsAsync(ILocalizationService localizationService)
        {
            //useful links:
            //http://fluentvalidation.codeplex.com/wikipage?title=Custom&referringTitle=Documentation&ANCHOR#CustomValidator
            //http://benjii.me/2010/11/credit-card-validator-attribute-for-asp-net-mvc-3/

            //RuleFor(x => x.CardNumber).NotEmpty().WithMessage(localizationService.GetResource("Payment.CardNumber.Required"));
            //RuleFor(x => x.CardCode).NotEmpty().WithMessage(localizationService.GetResource("Payment.CardCode.Required"));

            RuleFor(x => x.CardholderName).NotEmpty().WithMessage(await localizationService.GetResourceAsync("Payment.CardholderName.Required"));
            RuleFor(x => x.CardNumber).IsCreditCard().WithMessage(await localizationService.GetResourceAsync("Payment.CardNumber.Wrong"));
            RuleFor(x => x.CardCode).Matches(@"^[0-9]{3,4}$").WithMessage(await localizationService.GetResourceAsync("Payment.CardCode.Wrong"));
            RuleFor(x => x.ExpireMonth).NotEmpty().WithMessage(await localizationService.GetResourceAsync("Payment.ExpireMonth.Required"));
            RuleFor(x => x.ExpireYear).NotEmpty().WithMessage(await localizationService.GetResourceAsync("Payment.ExpireYear.Required"));
            RuleFor(x => x.ExpireMonth).Must((x, context) =>
            {
                //not specified yet
                if (string.IsNullOrEmpty(x.ExpireYear) || string.IsNullOrEmpty(x.ExpireMonth))
                    return true;

                //the cards remain valid until the last calendar day of that month
                //If, for example, an expiration date reads 06/15, this means it can be used until midnight on June 30, 2015
                var enteredDate = new DateTime(int.Parse(x.ExpireYear), int.Parse(x.ExpireMonth), 1).AddMonths(1);

                if (enteredDate < DateTime.Now)
                    return false;

                return true;
            }).WithMessage(await localizationService.GetResourceAsync("Payment.ExpirationDate.Expired"));
        }
    }
}
