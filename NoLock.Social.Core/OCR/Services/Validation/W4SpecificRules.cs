using NoLock.Social.Core.OCR.Models;
using System.Text.RegularExpressions;

namespace NoLock.Social.Core.OCR.Services.Validation
{
    /// <summary>
    /// Document-specific validation rules for W4 tax form documents.
    /// </summary>
    public class W4SpecificRules : IDocumentSpecificRule
    {
        private static readonly Regex SsnPattern = new Regex(@"^\d{3}-?\d{2}-?\d{4}$", RegexOptions.Compiled);
        private static readonly HashSet<string> ValidFilingStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "single", "married", "marriedfilingseparately", "married filing separately", "headofhousehold", "head of household"
        };

        public string DocumentType => "w4";

        public bool CanHandle(string fieldName, string documentType)
        {
            return !string.IsNullOrEmpty(documentType) && documentType.Equals(DocumentType, StringComparison.OrdinalIgnoreCase);
        }

        public async Task ApplyRulesAsync(string fieldName, object value, FieldValidationResult result)
        {
            var lowerFieldName = fieldName?.ToLower() ?? string.Empty;
            
            // For null values, we generally don't validate as they may be optional fields
            // Exception: exempt status should default to false even for null values
            if (value == null && lowerFieldName != "exempt" && lowerFieldName != "exemptfrompayrolldeduction" && lowerFieldName != "exempt_from_withholding")
            {
                await Task.CompletedTask;
                return;
            }

            switch (lowerFieldName)
            {
                case "ssn":
                case "socialsecuritynumber":
                case "social_security_number":
                    ValidateSSN(value, result);
                    break;

                case "withholding":
                case "additionalwithholding":
                case "additional_withholding":
                    ValidateWithholding(value, result);
                    break;

                case "allowances":
                case "numberofallowances":
                case "number_of_allowances":
                    ValidateAllowances(value, result);
                    break;

                case "filingstatus":
                case "filing_status":
                case "maritalstatus":
                case "marital_status":
                    ValidateFilingStatus(value, result);
                    break;

                case "dependents":
                case "numberofdependents":
                case "number_of_dependents":
                    ValidateDependents(value, result);
                    break;

                case "multipleworksheet":
                case "multiple_jobs_worksheet":
                    ValidateMultipleJobsWorksheet(value, result);
                    break;

                case "othersincome":
                case "other_income":
                    ValidateOtherIncome(value, result);
                    break;

                case "deductions":
                case "itemizeddeductions":
                case "itemized_deductions":
                    ValidateDeductions(value, result);
                    break;

                case "extrawithholding":
                case "extra_withholding":
                    ValidateExtraWithholding(value, result);
                    break;

                case "exempt":
                case "exemptfrompayrolldeduction":
                case "exempt_from_withholding":
                    ValidateExemptStatus(value, result);
                    break;

                case "year":
                case "taxyear":
                case "tax_year":
                    ValidateTaxYear(value, result);
                    break;
            }

            await Task.CompletedTask;
        }

        public void ValidateSSN(object value, FieldValidationResult result)
        {
            var ssnString = value?.ToString()?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(ssnString))
            {
                result.Errors.Add("SSN is required for W4 forms.");
                result.IsValid = false;
                return;
            }

            // Check format
            if (!SsnPattern.IsMatch(ssnString))
            {
                result.Errors.Add("SSN must be in format XXX-XX-XXXX or XXXXXXXXX.");
                result.IsValid = false;
                return;
            }

            // Normalize SSN (remove hyphens)
            var normalizedSsn = ssnString.Replace("-", "");

            // Check for invalid SSN patterns
            var areaNumber = normalizedSsn.Substring(0, 3);
            var areaNum = int.Parse(areaNumber);
            if (areaNum == 0 || areaNum == 666)
            {
                result.Errors.Add("Invalid SSN area number.");
                result.IsValid = false;
            }
            else if (areaNum >= 900 && areaNum <= 999)
            {
                result.Warnings.Add("SSN area number in test range (900-999).");
            }

            if (normalizedSsn.Substring(3, 2) == "00")
            {
                result.Errors.Add("Invalid SSN group number.");
                result.IsValid = false;
            }

            if (normalizedSsn.EndsWith("0000"))
            {
                result.Errors.Add("Invalid SSN serial number.");
                result.IsValid = false;
            }

            // Check for test SSNs
            if (normalizedSsn == "123456789" || normalizedSsn == "111111111" || normalizedSsn == "999999999")
            {
                result.Warnings.Add("SSN appears to be a test value.");
            }

            result.Metadata["normalizedSSN"] = normalizedSsn;
        }

        public void ValidateWithholding(object value, FieldValidationResult result)
        {
            if (!decimal.TryParse(value?.ToString(), out var withholding))
            {
                result.Errors.Add("Withholding must be a valid number.");
                result.IsValid = false;
                return;
            }

            if (withholding < 0)
            {
                result.Errors.Add("Withholding amount cannot be negative.");
                result.IsValid = false;
            }
            else if (withholding > 10000)
            {
                result.Warnings.Add("Withholding amount appears unusually high. Please verify.");
            }

            result.Metadata["withholdingAmount"] = withholding;
        }

        public void ValidateAllowances(object value, FieldValidationResult result)
        {
            if (!int.TryParse(value?.ToString(), out var allowances))
            {
                result.Errors.Add("Allowances must be a valid integer.");
                result.IsValid = false;
                return;
            }

            if (allowances < 0)
            {
                result.Errors.Add("Number of allowances cannot be negative.");
                result.IsValid = false;
            }
            else if (allowances > 20)
            {
                result.Warnings.Add("Number of allowances appears unusually high. Please verify.");
            }

            result.Metadata["allowancesCount"] = allowances;
        }

        public void ValidateFilingStatus(object value, FieldValidationResult result)
        {
            var status = value?.ToString()?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(status))
            {
                result.Errors.Add("Filing status is required for W4 forms.");
                result.IsValid = false;
                return;
            }

            var normalizedStatus = status.Replace(" ", "").ToLower();
            if (!ValidFilingStatuses.Contains(normalizedStatus) && !ValidFilingStatuses.Contains(status))
            {
                result.Errors.Add($"Invalid filing status. Must be one of: Single, Married, Married Filing Separately, or Head of Household.");
                result.IsValid = false;
            }

            result.Metadata["filingStatus"] = status;
        }

        public void ValidateDependents(object value, FieldValidationResult result)
        {
            if (!int.TryParse(value?.ToString(), out var dependents))
            {
                result.Errors.Add("Number of dependents must be a valid integer.");
                result.IsValid = false;
                return;
            }

            if (dependents < 0)
            {
                result.Errors.Add("Number of dependents cannot be negative.");
                result.IsValid = false;
            }
            else if (dependents > 20)
            {
                result.Warnings.Add("Number of dependents appears unusually high. Please verify.");
            }

            result.Metadata["dependentsCount"] = dependents;
        }

        public void ValidateMultipleJobsWorksheet(object value, FieldValidationResult result)
        {
            if (value == null || !decimal.TryParse(value?.ToString(), out var amount))
            {
                return; // Optional field
            }

            if (amount < 0)
            {
                result.Errors.Add("Multiple jobs worksheet amount cannot be negative.");
                result.IsValid = false;
            }
            else if (amount > 50000)
            {
                result.Warnings.Add("Multiple jobs worksheet amount appears unusually high.");
            }

            result.Metadata["multipleJobsAmount"] = amount;
        }

        public void ValidateOtherIncome(object value, FieldValidationResult result)
        {
            if (value == null || !decimal.TryParse(value?.ToString(), out var income))
            {
                return; // Optional field
            }

            if (income < 0)
            {
                result.Warnings.Add("Other income is negative. This may affect withholding calculations.");
            }
            else if (income > 100000)
            {
                result.Warnings.Add("Other income appears unusually high. Please verify.");
            }

            result.Metadata["otherIncome"] = income;
        }

        public void ValidateDeductions(object value, FieldValidationResult result)
        {
            if (value == null || !decimal.TryParse(value?.ToString(), out var deductions))
            {
                return; // Optional field
            }

            if (deductions < 0)
            {
                result.Errors.Add("Deductions amount cannot be negative.");
                result.IsValid = false;
            }
            else if (deductions > 100000)
            {
                result.Warnings.Add("Deductions amount appears unusually high. Please verify.");
            }

            result.Metadata["deductionsAmount"] = deductions;
        }

        public void ValidateExtraWithholding(object value, FieldValidationResult result)
        {
            if (value == null || !decimal.TryParse(value?.ToString(), out var extra))
            {
                return; // Optional field
            }

            if (extra < 0)
            {
                result.Errors.Add("Extra withholding amount cannot be negative.");
                result.IsValid = false;
            }
            else if (extra > 5000)
            {
                result.Warnings.Add("Extra withholding amount appears unusually high. Please verify.");
            }

            result.Metadata["extraWithholding"] = extra;
        }

        public void ValidateExemptStatus(object value, FieldValidationResult result)
        {
            var strValue = value?.ToString()?.ToLower() ?? string.Empty;

            if (strValue == "true" || strValue == "yes" || strValue == "1" || strValue == "exempt")
            {
                result.Metadata["isExempt"] = true;
                result.Warnings.Add("Employee has claimed exempt status. No federal income tax will be withheld.");
            }
            else if (value is bool boolValue && boolValue)
            {
                result.Metadata["isExempt"] = true;
                result.Warnings.Add("Employee has claimed exempt status. No federal income tax will be withheld.");
            }
            else
            {
                result.Metadata["isExempt"] = false;
            }
        }

        public void ValidateTaxYear(object value, FieldValidationResult result)
        {
            if (value == null || !int.TryParse(value?.ToString(), out var year))
            {
                return; // Optional field
            }

            var currentYear = DateTime.Now.Year;
            if (year < currentYear - 5)
            {
                result.Warnings.Add($"W4 form is for tax year {year}, which is more than 5 years old.");
            }
            else if (year > currentYear + 1)
            {
                result.Errors.Add($"Invalid tax year {year}. Cannot be more than 1 year in the future.");
                result.IsValid = false;
            }

            result.Metadata["taxYear"] = year;
        }
    }
}