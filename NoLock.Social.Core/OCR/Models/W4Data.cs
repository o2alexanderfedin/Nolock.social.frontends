using System;
using System.Collections.Generic;

namespace NoLock.Social.Core.OCR.Models
{
    /// <summary>
    /// Represents extracted data from a W-4 tax withholding form.
    /// Supports both pre-2020 and post-2020 W-4 formats.
    /// </summary>
    public class W4Data
    {
        #region Employee Information

        /// <summary>
        /// Gets or sets the employee's first name.
        /// </summary>
        public string FirstName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the employee's middle name or initial.
        /// </summary>
        public string MiddleName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the employee's last name.
        /// </summary>
        public string LastName { get; set; } = string.Empty;

        /// <summary>
        /// Gets the employee's full name.
        /// </summary>
        public string FullName => $"{FirstName} {MiddleName} {LastName}".Trim();

        /// <summary>
        /// Gets or sets the employee's Social Security Number (SSN).
        /// Should be stored masked for security (XXX-XX-1234).
        /// </summary>
        public string SSN { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the employee's street address.
        /// </summary>
        public string StreetAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the employee's city.
        /// </summary>
        public string City { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the employee's state abbreviation.
        /// </summary>
        public string State { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the employee's ZIP code.
        /// </summary>
        public string ZipCode { get; set; } = string.Empty;

        #endregion

        #region Filing Status

        /// <summary>
        /// Gets or sets the filing status.
        /// Values: "Single", "Married filing jointly", "Married filing separately", "Head of household"
        /// </summary>
        public string FilingStatus { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the Single or Married filing separately box is checked.
        /// </summary>
        public bool IsSingleOrMarriedFilingSeparately { get; set; }

        /// <summary>
        /// Gets or sets whether the Married filing jointly box is checked.
        /// </summary>
        public bool IsMarriedFilingJointly { get; set; }

        /// <summary>
        /// Gets or sets whether the Head of household box is checked.
        /// </summary>
        public bool IsHeadOfHousehold { get; set; }

        #endregion

        #region Multiple Jobs or Spouse Works (Post-2020)

        /// <summary>
        /// Gets or sets whether the multiple jobs checkbox is selected (Step 2).
        /// Only applicable for post-2020 W-4 forms.
        /// </summary>
        public bool HasMultipleJobs { get; set; }

        /// <summary>
        /// Gets or sets whether to use the IRS online estimator option.
        /// </summary>
        public bool UseOnlineEstimator { get; set; }

        /// <summary>
        /// Gets or sets whether to use the Multiple Jobs Worksheet.
        /// </summary>
        public bool UseMultipleJobsWorksheet { get; set; }

        /// <summary>
        /// Gets or sets whether two jobs total checkbox is selected.
        /// </summary>
        public bool TwoJobsTotal { get; set; }

        #endregion

        #region Dependents (Post-2020)

        /// <summary>
        /// Gets or sets the number of qualifying children (under 17).
        /// Post-2020 forms calculate this as number × $2,000.
        /// </summary>
        public int QualifyingChildren { get; set; }

        /// <summary>
        /// Gets or sets the number of other dependents.
        /// Post-2020 forms calculate this as number × $500.
        /// </summary>
        public int OtherDependents { get; set; }

        /// <summary>
        /// Gets or sets the total claim amount for dependents (Step 3).
        /// </summary>
        public decimal DependentsClaimAmount { get; set; }

        #endregion

        #region Other Adjustments

        /// <summary>
        /// Gets or sets other income (not from jobs) amount (Step 4a).
        /// </summary>
        public decimal OtherIncome { get; set; }

        /// <summary>
        /// Gets or sets the deductions amount (Step 4b).
        /// </summary>
        public decimal Deductions { get; set; }

        /// <summary>
        /// Gets or sets extra withholding amount per pay period (Step 4c).
        /// </summary>
        public decimal ExtraWithholding { get; set; }

        #endregion

        #region Pre-2020 W-4 Fields

        /// <summary>
        /// Gets or sets the total number of allowances claimed.
        /// Only applicable for pre-2020 W-4 forms.
        /// </summary>
        public int? TotalAllowances { get; set; }

        /// <summary>
        /// Gets or sets whether the employee is exempt from withholding.
        /// </summary>
        public bool IsExempt { get; set; }

        #endregion

        #region Form Metadata

        /// <summary>
        /// Gets or sets the W-4 form version (e.g., "2020", "2019", "2018").
        /// </summary>
        public string FormVersion { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether this is a pre-2020 format W-4.
        /// </summary>
        public bool IsPreTwentyTwentyFormat => !string.IsNullOrEmpty(FormVersion) && 
            int.TryParse(FormVersion, out int year) && year < 2020;

        /// <summary>
        /// Gets or sets the employee's signature.
        /// </summary>
        public string EmployeeSignature { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the date the form was signed.
        /// </summary>
        public DateTime? DateSigned { get; set; }

        /// <summary>
        /// Gets or sets whether a signature was detected on the form.
        /// </summary>
        public bool SignatureDetected { get; set; }

        #endregion

        #region Employer Use Only

        /// <summary>
        /// Gets or sets the employer's name.
        /// </summary>
        public string EmployerName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the employer's address.
        /// </summary>
        public string EmployerAddress { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the employer's EIN (Employer Identification Number).
        /// </summary>
        public string EmployerEIN { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the first date of employment.
        /// </summary>
        public DateTime? FirstDateOfEmployment { get; set; }

        #endregion

        #region Validation

        /// <summary>
        /// Gets or sets validation errors encountered during processing.
        /// </summary>
        public List<string> ValidationErrors { get; set; } = new List<string>();

        /// <summary>
        /// Validates the W-4 data for completeness and correctness.
        /// </summary>
        /// <returns>True if the data is valid; otherwise, false.</returns>
        public bool Validate()
        {
            ValidationErrors.Clear();

            // Validate required fields
            if (string.IsNullOrWhiteSpace(FirstName) && string.IsNullOrWhiteSpace(LastName))
            {
                ValidationErrors.Add("Employee name is required.");
            }

            if (string.IsNullOrWhiteSpace(SSN))
            {
                ValidationErrors.Add("Social Security Number is required.");
            }
            else if (!IsValidSSNFormat(SSN))
            {
                ValidationErrors.Add("Invalid Social Security Number format.");
            }

            if (string.IsNullOrWhiteSpace(StreetAddress))
            {
                ValidationErrors.Add("Street address is required.");
            }

            if (string.IsNullOrWhiteSpace(City))
            {
                ValidationErrors.Add("City is required.");
            }

            if (string.IsNullOrWhiteSpace(State))
            {
                ValidationErrors.Add("State is required.");
            }

            if (string.IsNullOrWhiteSpace(ZipCode))
            {
                ValidationErrors.Add("ZIP code is required.");
            }

            // Validate filing status
            if (string.IsNullOrWhiteSpace(FilingStatus) && 
                !IsSingleOrMarriedFilingSeparately && 
                !IsMarriedFilingJointly && 
                !IsHeadOfHousehold)
            {
                ValidationErrors.Add("Filing status is required.");
            }

            // Validate signature
            if (!SignatureDetected && string.IsNullOrWhiteSpace(EmployeeSignature))
            {
                ValidationErrors.Add("Employee signature is required.");
            }

            if (!DateSigned.HasValue)
            {
                ValidationErrors.Add("Date signed is required.");
            }

            // Version-specific validation
            if (IsPreTwentyTwentyFormat)
            {
                if (!TotalAllowances.HasValue && !IsExempt)
                {
                    ValidationErrors.Add("Total allowances must be specified for pre-2020 W-4 forms.");
                }
            }
            else
            {
                // Post-2020 validation
                if (QualifyingChildren < 0)
                {
                    ValidationErrors.Add("Number of qualifying children cannot be negative.");
                }

                if (OtherDependents < 0)
                {
                    ValidationErrors.Add("Number of other dependents cannot be negative.");
                }

                if (DependentsClaimAmount < 0)
                {
                    ValidationErrors.Add("Dependents claim amount cannot be negative.");
                }

                if (OtherIncome < 0)
                {
                    ValidationErrors.Add("Other income amount cannot be negative.");
                }

                if (Deductions < 0)
                {
                    ValidationErrors.Add("Deductions amount cannot be negative.");
                }

                if (ExtraWithholding < 0)
                {
                    ValidationErrors.Add("Extra withholding amount cannot be negative.");
                }
            }

            return ValidationErrors.Count == 0;
        }

        /// <summary>
        /// Validates the SSN format (XXX-XX-XXXX or XXXXXXXXX).
        /// </summary>
        private bool IsValidSSNFormat(string ssn)
        {
            if (string.IsNullOrWhiteSpace(ssn))
                return false;

            // Remove any non-digit characters for validation
            var digitsOnly = System.Text.RegularExpressions.Regex.Replace(ssn, @"\D", "");

            // Check if it's 9 digits (can be masked or full)
            if (digitsOnly.Length < 4 || digitsOnly.Length > 9)
                return false;

            // If it's masked (XXX-XX-1234), just check last 4 are digits
            if (ssn.Contains("X") || ssn.Contains("*"))
            {
                var lastFour = System.Text.RegularExpressions.Regex.Match(ssn, @"\d{4}$");
                return lastFour.Success;
            }

            // For full SSN, ensure it's 9 digits
            return digitsOnly.Length == 9;
        }

        /// <summary>
        /// Gets the masked SSN for display (XXX-XX-1234).
        /// </summary>
        public string GetMaskedSSN()
        {
            if (string.IsNullOrWhiteSpace(SSN))
                return string.Empty;

            // If already masked, return as is
            if (SSN.Contains("X") || SSN.Contains("*"))
                return SSN;

            // Extract just digits
            var digitsOnly = System.Text.RegularExpressions.Regex.Replace(SSN, @"\D", "");

            if (digitsOnly.Length >= 4)
            {
                var lastFour = digitsOnly.Substring(digitsOnly.Length - 4);
                return $"XXX-XX-{lastFour}";
            }

            return "XXX-XX-XXXX";
        }

        #endregion
    }
}