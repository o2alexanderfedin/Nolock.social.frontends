namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Represents extracted data from a check document.
    /// </summary>
    public class CheckData
    {
        /// <summary>
        /// Gets or sets the bank name.
        /// </summary>
        public string BankName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the bank routing number (9 digits).
        /// </summary>
        public string RoutingNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the account number.
        /// </summary>
        public string AccountNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the check number.
        /// </summary>
        public string CheckNumber { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the numeric amount on the check.
        /// </summary>
        public decimal? AmountNumeric { get; set; }

        /// <summary>
        /// Gets or sets the written amount on the check.
        /// </summary>
        public string AmountWritten { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the parsed value from the written amount.
        /// </summary>
        public decimal? AmountWrittenParsed { get; set; }

        /// <summary>
        /// Gets or sets the payee name (who the check is made out to).
        /// </summary>
        public string Payee { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the check date.
        /// </summary>
        public DateTime? Date { get; set; }

        /// <summary>
        /// Gets or sets the memo or description field.
        /// </summary>
        public string Memo { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the payer name (account holder).
        /// </summary>
        public string PayerName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the payer address.
        /// </summary>
        public string PayerAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the raw MICR line data.
        /// </summary>
        public string MicrLine { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether a signature was detected.
        /// </summary>
        public bool SignatureDetected { get; set; }

        /// <summary>
        /// Gets or sets the signature confidence score (0-1).
        /// </summary>
        public double SignatureConfidence { get; set; }

        /// <summary>
        /// Gets or sets whether the routing number is valid.
        /// </summary>
        public bool IsRoutingNumberValid { get; set; }

        /// <summary>
        /// Gets or sets whether the written and numeric amounts match.
        /// </summary>
        public bool AmountsMatch { get; set; }

        /// <summary>
        /// Gets or sets validation errors found during processing.
        /// </summary>
        public List<string> ValidationErrors { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets additional notes or warnings.
        /// </summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Validates the check data for consistency and completeness.
        /// </summary>
        /// <returns>True if the check data is valid; otherwise, false.</returns>
        public bool Validate()
        {
            ValidationErrors.Clear();

            // Check required fields
            if (string.IsNullOrWhiteSpace(RoutingNumber))
            {
                ValidationErrors.Add("Routing number is missing");
            }
            else if (RoutingNumber.Length != 9)
            {
                ValidationErrors.Add("Routing number must be 9 digits");
            }

            if (string.IsNullOrWhiteSpace(AccountNumber))
            {
                ValidationErrors.Add("Account number is missing");
            }

            if (string.IsNullOrWhiteSpace(CheckNumber))
            {
                ValidationErrors.Add("Check number is missing");
            }

            if (!AmountNumeric.HasValue || AmountNumeric.Value <= 0)
            {
                ValidationErrors.Add("Numeric amount is missing or invalid");
            }

            if (string.IsNullOrWhiteSpace(Payee))
            {
                ValidationErrors.Add("Payee is missing");
            }

            if (!Date.HasValue)
            {
                ValidationErrors.Add("Date is missing");
            }

            // Check amount consistency
            if (AmountNumeric.HasValue && AmountWrittenParsed.HasValue)
            {
                AmountsMatch = Math.Abs(AmountNumeric.Value - AmountWrittenParsed.Value) < 0.01m;
                if (!AmountsMatch)
                {
                    ValidationErrors.Add($"Amount mismatch: Numeric=${AmountNumeric.Value:F2}, Written=${AmountWrittenParsed.Value:F2}");
                }
            }

            return ValidationErrors.Count == 0;
        }
    }
}