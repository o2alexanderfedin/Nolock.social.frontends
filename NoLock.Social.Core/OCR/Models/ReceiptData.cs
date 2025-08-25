namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Represents extracted data from a receipt document.
    /// </summary>
    public class ReceiptData
    {
        /// <summary>
        /// Gets or sets the store or merchant name.
        /// </summary>
        public string StoreName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the store address.
        /// </summary>
        public string StoreAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the store phone number.
        /// </summary>
        public string StorePhone { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the transaction date and time.
        /// </summary>
        public DateTime? TransactionDate { get; set; }

        /// <summary>
        /// Gets or sets the receipt number.
        /// </summary>
        public string ReceiptNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the cashier name or ID.
        /// </summary>
        public string Cashier { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of items purchased.
        /// </summary>
        public List<ReceiptItem> Items { get; set; } = new List<ReceiptItem>();

        /// <summary>
        /// Gets or sets the subtotal amount before tax.
        /// </summary>
        public decimal Subtotal { get; set; }

        /// <summary>
        /// Gets or sets the tax amount.
        /// </summary>
        public decimal TaxAmount { get; set; }

        /// <summary>
        /// Gets or sets the tax rate percentage.
        /// </summary>
        public decimal TaxRate { get; set; }

        /// <summary>
        /// Gets or sets the total amount including tax.
        /// </summary>
        public decimal Total { get; set; }

        /// <summary>
        /// Gets or sets the payment method used.
        /// </summary>
        public string PaymentMethod { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the last four digits of the payment card if applicable.
        /// </summary>
        public string CardLastFour { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets any additional notes or information.
        /// </summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the currency code (e.g., USD, EUR).
        /// </summary>
        public string Currency { get; set; } = "USD";
    }

    /// <summary>
    /// Represents an individual item on a receipt.
    /// </summary>
    public class ReceiptItem
    {
        /// <summary>
        /// Gets or sets the item description or name.
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the item SKU or product code.
        /// </summary>
        public string Sku { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the quantity purchased.
        /// </summary>
        public decimal Quantity { get; set; } = 1;

        /// <summary>
        /// Gets or sets the unit price.
        /// </summary>
        public decimal UnitPrice { get; set; }

        /// <summary>
        /// Gets or sets the total price for this line item.
        /// </summary>
        public decimal TotalPrice { get; set; }

        /// <summary>
        /// Gets or sets whether this item is taxable.
        /// </summary>
        public bool IsTaxable { get; set; } = true;

        /// <summary>
        /// Gets or sets any discount applied to this item.
        /// </summary>
        public decimal DiscountAmount { get; set; }
    }
}