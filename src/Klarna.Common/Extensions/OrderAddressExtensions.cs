﻿using System;
using EPiServer.Commerce.Order;
using Klarna.Common.Helpers;
using Klarna.Common.Models;
using Mediachase.Commerce.Customers;

namespace Klarna.Common.Extensions
{
    public static class OrderAddressExtensions
    {
        public static OrderManagementAddressInfo ToAddress(this IOrderAddress orderAddress)
        {
            var address = new OrderManagementAddressInfo();
            address.GivenName = orderAddress.FirstName;
            address.FamilyName = orderAddress.LastName;
            address.StreetAddress = orderAddress.Line1;
            address.StreetAddress2 = orderAddress.Line2;
            address.PostalCode = orderAddress.PostalCode;
            address.City = orderAddress.City;
            address.Country = CountryCodeHelper.GetTwoLetterCountryCode(orderAddress.CountryCode);
            if (orderAddress.CountryCode != null && address.Country.Equals("us", StringComparison.InvariantCultureIgnoreCase) && !string.IsNullOrEmpty(orderAddress.RegionName))
            {
                address.Region =
                    CountryCodeHelper.GetStateCode(CountryCodeHelper.GetTwoLetterCountryCode(orderAddress.CountryCode),
                        orderAddress.RegionName);
            }
            else
            {
                address.Region = orderAddress.RegionName;
            }

            address.Email = orderAddress.Email;
            address.Phone = orderAddress.DaytimePhoneNumber ?? orderAddress.EveningPhoneNumber;

            return address;
        }
        
        public static OrderManagementAddressInfo ToOrderAddress(this CustomerAddress customerAddress)
        {
            var address = new OrderManagementAddressInfo
            {
                GivenName = customerAddress.FirstName,
                FamilyName = customerAddress.LastName,
                StreetAddress = customerAddress.Line1,
                StreetAddress2 = customerAddress.Line2,
                PostalCode = customerAddress.PostalCode,
                City = customerAddress.City,
                Email = customerAddress.Email,
                Phone = customerAddress.DaytimePhoneNumber ?? customerAddress.EveningPhoneNumber
            };

            var countryCode = CountryCodeHelper.GetTwoLetterCountryCode(customerAddress.CountryCode);
            address.Country = countryCode;
            if (customerAddress.CountryCode != null && customerAddress.RegionName != null)
            {
                address.Region = CountryCodeHelper.GetStateCode(countryCode, customerAddress.RegionName);
            }

            return address;
        }
    }
}