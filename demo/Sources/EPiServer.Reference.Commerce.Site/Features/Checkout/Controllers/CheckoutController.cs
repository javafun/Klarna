﻿using EPiServer.Commerce.Order;
using EPiServer.Core;
using EPiServer.Reference.Commerce.Site.Features.Cart.Services;
using EPiServer.Reference.Commerce.Site.Features.Checkout.Pages;
using EPiServer.Reference.Commerce.Site.Features.Checkout.Services;
using EPiServer.Reference.Commerce.Site.Features.Checkout.ViewModelFactories;
using EPiServer.Reference.Commerce.Site.Features.Checkout.ViewModels;
using EPiServer.Reference.Commerce.Site.Features.Market.Services;
using EPiServer.Reference.Commerce.Site.Features.Recommendations.Services;
using EPiServer.Reference.Commerce.Site.Features.Shared.Services;
using EPiServer.Reference.Commerce.Site.Infrastructure.Attributes;
using EPiServer.Web;
using EPiServer.Web.Mvc;
using EPiServer.Web.Mvc.Html;
using EPiServer.Web.Routing;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;
using EPiServer.Reference.Commerce.Site.Features.Shared.Extensions;
using Klarna.Checkout;
using Klarna.Common;
using Klarna.Payments;
using Klarna.Payments.Models;
using Mediachase.Commerce.Markets;

namespace EPiServer.Reference.Commerce.Site.Features.Checkout.Controllers
{
    public class CheckoutController : PageController<CheckoutPage>
    {
        private readonly ICurrencyService _currencyService;
        private readonly ControllerExceptionHandler _controllerExceptionHandler;
        private readonly CheckoutViewModelFactory _checkoutViewModelFactory;
        private readonly OrderSummaryViewModelFactory _orderSummaryViewModelFactory;
        private readonly IOrderRepository _orderRepository;
        private readonly ICartService _cartService;
        private readonly IRecommendationService _recommendationService;
        private readonly OrderValidationService _orderValidationService;
        private ICart _cart;
        private readonly CheckoutService _checkoutService;
        private readonly IKlarnaPaymentsService _klarnaPaymentsService;
        private readonly IKlarnaCheckoutService _klarnaCheckoutService;
        private readonly IContentLoader _contentLoader;
        private readonly IMarketService _marketService;

        public CheckoutController(
            ICurrencyService currencyService,
            ControllerExceptionHandler controllerExceptionHandler,
            IOrderRepository orderRepository,
            CheckoutViewModelFactory checkoutViewModelFactory,
            ICartService cartService,
            OrderSummaryViewModelFactory orderSummaryViewModelFactory,
            IRecommendationService recommendationService,
            CheckoutService checkoutService, 
            OrderValidationService orderValidationService,
            IKlarnaPaymentsService klarnaPaymentsService,
            IKlarnaCheckoutService klarnaCheckoutService,
            IContentLoader contentLoader,
            IMarketService marketService)
        {
            _currencyService = currencyService;
            _controllerExceptionHandler = controllerExceptionHandler;
            _orderRepository = orderRepository;
            _checkoutViewModelFactory = checkoutViewModelFactory;
            _cartService = cartService;
            _orderSummaryViewModelFactory = orderSummaryViewModelFactory;
            _recommendationService = recommendationService;
            _checkoutService = checkoutService;
            _orderValidationService = orderValidationService;
            _klarnaPaymentsService = klarnaPaymentsService;
            _klarnaCheckoutService = klarnaCheckoutService;
            _contentLoader = contentLoader;
            _marketService = marketService;
        }

        [HttpGet]
        [OutputCache(Duration = 0, NoStore = true)]
        public async Task<ActionResult> Index(CheckoutPage currentPage)
        {
            if (CartIsNullOrEmpty())
            {
                return View("EmptyCart");
            }

            var viewModel = CreateCheckoutViewModel(currentPage);

            // To allow sharing of Personal Information for demo purposes
            Cart.Properties[Klarna.Payments.Constants.KlarnaAllowSharingOfPersonalInformationCartField] = true;
            Cart.Currency = _currencyService.GetCurrentCurrency();

            _checkoutService.UpdateShippingAddresses(Cart, viewModel);
            _checkoutService.UpdateShippingMethods(Cart, viewModel.Shipments);

            _cartService.ApplyDiscounts(Cart);
            _orderRepository.Save(Cart);

            await _recommendationService.TrackCheckoutAsync(HttpContext);

            _checkoutService.ProcessPaymentCancel(viewModel, TempData, ControllerContext);

            return View(viewModel.ViewName, viewModel);
        }

        [HttpGet]
        public ActionResult SingleShipment(CheckoutPage currentPage)
        {
            if (!CartIsNullOrEmpty())
            {
                _cartService.MergeShipments(Cart);
                _orderRepository.Save(Cart);
            }

            return RedirectToAction("Index", new { node = currentPage.ContentLink });
        }

        [HttpPost]
        [AllowDBWrite]
        public async Task<ActionResult> ChangeAddress(UpdateAddressViewModel addressViewModel)
        {
            ModelState.Clear();
            var viewModel = CreateCheckoutViewModel(addressViewModel.CurrentPage);
            _checkoutService.CheckoutAddressHandling.ChangeAddress(viewModel, addressViewModel);

            _checkoutService.UpdateShippingAddresses(Cart, viewModel);

            _orderRepository.Save(Cart);

            var addressViewName = addressViewModel.ShippingAddressIndex > -1 ? "SingleShippingAddress" : "BillingAddress";

            await _klarnaPaymentsService.CreateOrUpdateSession(Cart, new SessionSettings(SiteDefinition.Current.SiteUrl));

            return PartialView(addressViewName, viewModel);
        }

        [AcceptVerbs(HttpVerbs.Get | HttpVerbs.Post)]
        public ActionResult OrderSummary()
        {
            var viewModel = _orderSummaryViewModelFactory.CreateOrderSummaryViewModel(Cart);

            var success = AsyncHelper.RunSync(() => _klarnaPaymentsService.CreateOrUpdateSession(Cart, new SessionSettings(SiteDefinition.Current.SiteUrl)));

            return PartialView(viewModel);
        }

        [HttpPost]
        [AllowDBWrite]
        public ActionResult AddCouponCode(CheckoutPage currentPage, string couponCode)
        {
            if (_cartService.AddCouponCode(Cart, couponCode))
            {
                _orderRepository.Save(Cart);
            }
            var viewModel = CreateCheckoutViewModel(currentPage);
            return View(viewModel.ViewName, viewModel);
        }

        [HttpPost]
        [AllowDBWrite]
        public ActionResult RemoveCouponCode(CheckoutPage currentPage, string couponCode)
        {
            _cartService.RemoveCouponCode(Cart, couponCode);
            _orderRepository.Save(Cart);
            var viewModel = CreateCheckoutViewModel(currentPage);
            return View(viewModel.ViewName, viewModel);
        }
        
        [HttpPost]
        [AllowDBWrite]
        public ActionResult Purchase(CheckoutViewModel viewModel, IPaymentMethod paymentMethod)
        {
            if (CartIsNullOrEmpty())
            {
                return Redirect(Url.ContentUrl(ContentReference.StartPage));
            }

            viewModel.Payment = paymentMethod;

            viewModel.IsAuthenticated = User.Identity.IsAuthenticated;

            _checkoutService.CheckoutAddressHandling.UpdateUserAddresses(viewModel);

            if (!_checkoutService.ValidateOrder(ModelState, viewModel, _orderValidationService.ValidateOrder(Cart)))
            {
                return View(viewModel);
            }
            
            if (!paymentMethod.ValidateData())
            {
                return View(viewModel);
            }

            _checkoutService.UpdateShippingAddresses(Cart, viewModel);
            
            _checkoutService.CreateAndAddPaymentToCart(Cart, viewModel);

            var purchaseOrder = _checkoutService.PlaceOrder(Cart, ModelState, viewModel);
            if (!string.IsNullOrEmpty(viewModel.RedirectUrl))
            {
                return Redirect(viewModel.RedirectUrl);
            }

            if (purchaseOrder == null)
            {
                return View(viewModel);
            }

            var result = _klarnaPaymentsService.Complete(purchaseOrder);
            if (result.IsRedirect)
            {
                return Redirect(result.RedirectUrl);
            }

            var confirmationSentSuccessfully = _checkoutService.SendConfirmation(viewModel, purchaseOrder);

            return Redirect(_checkoutService.BuildRedirectionUrl(viewModel, purchaseOrder, confirmationSentSuccessfully));
        }

        [HttpGet]
        public async Task<ActionResult> KlarnaCheckoutConfirmation(int orderGroupId, string klarna_order_id)
        {
            var cart = _klarnaCheckoutService.GetCartByKlarnaOrderId(orderGroupId, klarna_order_id);
            if (cart != null)
            {
                var market = _marketService.GetMarket(cart.MarketId);
                var order = await _klarnaCheckoutService.GetOrder(klarna_order_id, market).ConfigureAwait(false);
                if (order.Status == "checkout_complete")
                {
                    var purchaseOrder = _checkoutService.CreatePurchaseOrderForKlarna(klarna_order_id, order, cart);
                    if (purchaseOrder == null)
                    {
                        ModelState.AddModelError("", "Error occurred while creating a purchase order");

                        return RedirectToAction("Index");
                    }

                    var currentPage = _contentLoader.Get<CheckoutPage>(_contentLoader.GetStartPage().CheckoutPage);

                    var address = new Shared.Models.AddressModel
                    {
                        Email = purchaseOrder.GetFirstForm().Payments.FirstOrDefault()?.BillingAddress.Email
                    };

                _checkoutService.SendConfirmation(new CheckoutViewModel
                    {
                        CurrentPage = currentPage,
                        BillingAddress = address
                }, purchaseOrder);

                    return Redirect(_checkoutService.BuildRedirectionUrl(purchaseOrder));
                }
                else
                {
                    return RedirectToAction("Index");
                }
            }
            return HttpNotFound();
        }

        public ActionResult OnPurchaseException(ExceptionContext filterContext)
        {
            var currentPage = filterContext.RequestContext.GetRoutedData<CheckoutPage>();
            if (currentPage == null)
            {
                return new EmptyResult();
            }

            var viewModel = CreateCheckoutViewModel(currentPage);
            ModelState.AddModelError("Purchase", filterContext.Exception.Message);

            return View(viewModel.ViewName, viewModel);
        }

        protected override void OnException(ExceptionContext filterContext)
        {
            _controllerExceptionHandler.HandleRequestValidationException(filterContext, "purchase", OnPurchaseException);
        }

        private ViewResult View(CheckoutViewModel checkoutViewModel)
        {
            return View(checkoutViewModel.ViewName, CreateCheckoutViewModel(checkoutViewModel.CurrentPage, checkoutViewModel.Payment));
        }

        private CheckoutViewModel CreateCheckoutViewModel(CheckoutPage currentPage, IPaymentMethod paymentMethod = null)
        {
            return _checkoutViewModelFactory.CreateCheckoutViewModel(Cart, currentPage, paymentMethod);
        }

        private ICart Cart => _cart ?? (_cart = _cartService.LoadCart(_cartService.DefaultCartName));

        private bool CartIsNullOrEmpty()
        {
            return Cart == null || !Cart.GetAllLineItems().Any();
        }
    }
}
