﻿using System.Threading.Tasks;
using EPiServer.Commerce.Order;
using Klarna.Checkout.Models;
using Klarna.Rest.Models;

namespace Klarna.Checkout
{
    public interface IKlarnaCheckoutService
    {
        CheckoutOrderData CreateOrUpdateOrder(ICart cart);
        CheckoutOrderData CreateOrder(ICart cart);
        CheckoutOrderData UpdateOrder(string orderId, ICart cart);

        CheckoutOrderData GetOrder(string orderId);

        ICart GetCartByKlarnaOrderId(int orderGroupId, string orderId);

        void UpdateShippingMethod(ICart cart, PatchedCheckoutOrderData checkoutOrderData);

        void UpdateAddress(ICart cart, PatchedCheckoutOrderData checkoutOrderData);
    }
}