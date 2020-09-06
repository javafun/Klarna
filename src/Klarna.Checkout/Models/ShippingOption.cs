﻿using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Klarna.Checkout.Models
{
    public class ShippingOption
    {
        /// <summary>
        /// Id.
        /// </summary>
        /// <remarks>Required</remarks>
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }
        /// <summary>
        /// Name.
        /// </summary>
        /// <remarks>Required</remarks>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        /// <summary>
        /// Description.
        /// </summary>
        [JsonProperty(PropertyName = "description")]
        public string Description { get; set; }
        /// <summary>
        /// Promotion name. To be used if this shipping option is promotional.
        /// </summary>
        [JsonProperty(PropertyName = "promo")]
        public string Promo { get; set; }
        /// <summary>
        /// Price including tax.
        /// </summary>
        /// <remarks>Required</remarks>
        [JsonProperty(PropertyName = "price")]
        public int Price { get; set; }
        /// <summary>
        /// Tax amount.
        /// </summary>
        /// <remarks>Required</remarks>
        [JsonProperty(PropertyName = "tax_amount")]
        public int TaxAmount { get; set; }
        /// <summary>
        /// Non-negative. In percent, two implicit decimals, I.e 2500 = 25%.
        /// </summary>
        /// <remarks>Required</remarks>
        [JsonProperty(PropertyName = "tax_rate")]
        public int TaxRate { get; set; }
        /// <summary>
        /// If true, this option will be preselected when checkout loads. Default: false
        /// </summary>
        [JsonProperty(PropertyName = "preselected")]
        public bool Preselected { get; set; }
        /// <summary>
        /// Shipping method. 
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = "shipping_method")]
        public ShippingMethod ShippingMethod { get; set; }
        
        /// <summary>
        /// The delivery details for this shipping option. 
        /// </summary>
        [JsonProperty(PropertyName = "delivery_details")]
        public DeliveryDetails DeliveryDetails { get; set; }
        
        /// <summary>
        /// TMS reference. Required to map completed orders to shipments reserved in TMS.
        /// </summary>
        [JsonProperty(PropertyName = "tms_reference")]
        public string TmsReference { get; set; }
    }
}
