using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NoLock.Social.Core.OCR.Interfaces;
using NoLock.Social.Core.OCR.Models;

namespace NoLock.Social.Core.OCR.Processors
{
    /// <summary>
    /// Processes W-4 tax withholding form documents to extract structured data.
    /// Supports both pre-2020 and post-2020 W-4 formats.
    /// Implements the IDocumentProcessor interface for the plugin architecture.
    /// </summary>
    public class W4Processor : IDocumentProcessor
    {
        private readonly ILogger<W4Processor> _logger;

        /// <summary>
        /// Regular expressions for W-4 form pattern matching.
        /// </summary>
        private static class Patterns
        {
            // Form identification patterns
            public static readonly Regex FormTitle = new Regex(
                @"Form\s+W-?4|Employee.*Withholding.*Certificate|Withholding.*Allowance.*Certificate",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex FormYear = new Regex(
                @"(?:Form\s+W-?4\s*\()?(\d{4})\)?|Rev\.\s*(\d{4})",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Employee information patterns
            public static readonly Regex Name = new Regex(
                @"(?:First\s+name|Name)[:\s]*([A-Za-z\-']+)\s+(?:Middle|MI)?[:\s]*([A-Za-z\-']*)?\s*(?:Last\s+name)?[:\s]*([A-Za-z\-']+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex SSN = new Regex(
                @"(?:SSN|Social\s+Security\s+Number)[:\s]*(\d{3}[-\s]?\d{2}[-\s]?\d{4})",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex Address = new Regex(
                @"(?:Address|Street)[:\s]*(.+?)(?=\n|$)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex CityStateZip = new Regex(
                @"(?:City)[:\s]*([A-Za-z\s]+),?\s*(?:State)?[:\s]*([A-Z]{2})\s*(?:ZIP|Zip\s+code)?[:\s]*(\d{5}(?:-\d{4})?)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Filing status patterns
            public static readonly Regex Single = new Regex(
                @"\[?\s*[X✓]\s*\]?\s*Single\s+or\s+Married\s+filing\s+separately",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex MarriedJointly = new Regex(
                @"\[?\s*[X✓]\s*\]?\s*Married\s+filing\s+jointly",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex HeadOfHousehold = new Regex(
                @"\[?\s*[X✓]\s*\]?\s*Head\s+of\s+household",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Multiple jobs pattern (post-2020)
            public static readonly Regex MultipleJobs = new Regex(
                @"Step\s+2.*Multiple\s+[Jj]obs|Complete\s+this\s+step\s+if\s+you.*hold\s+more\s+than\s+one\s+job",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Dependents patterns (post-2020)
            public static readonly Regex QualifyingChildren = new Regex(
                @"(?:Qualifying\s+children|Children\s+under\s+age\s+17)[:\s]*(\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex OtherDependents = new Regex(
                @"(?:Other\s+dependents)[:\s]*(\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Other adjustments patterns (post-2020)
            public static readonly Regex OtherIncome = new Regex(
                @"(?:4a|Other\s+income)[:\s]*\$?\s*([\d,]+\.?\d*)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex Deductions = new Regex(
                @"(?:4b|Deductions)[:\s]*\$?\s*([\d,]+\.?\d*)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex ExtraWithholding = new Regex(
                @"(?:4c|Extra\s+withholding)[:\s]*\$?\s*([\d,]+\.?\d*)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Pre-2020 patterns
            public static readonly Regex TotalAllowances = new Regex(
                @"(?:Total\s+number\s+of\s+allowances|Line\s+[HG])[:\s]*(\d+)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex Exempt = new Regex(
                @"\[?\s*[X✓]\s*\]?\s*(?:I\s+claim\s+)?Exempt",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Signature patterns
            public static readonly Regex SignatureDate = new Regex(
                @"(?:Date|Dated)[:\s]*(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex SignatureLine = new Regex(
                @"(?:Employee.*signature|Signature)[:\s]*(.+?)(?=\n|Date|$)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Employer section patterns
            public static readonly Regex EmployerName = new Regex(
                @"(?:Employer.*name|Company)[:\s]*(.+?)(?=\n|$)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            public static readonly Regex EmployerEIN = new Regex(
                @"(?:EIN|Employer.*identification.*number)[:\s]*(\d{2}-?\d{7})",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        /// <summary>
        /// Initializes a new instance of the W4Processor class.
        /// </summary>
        /// <param name="logger">Logger for diagnostic output.</param>
        public W4Processor(ILogger<W4Processor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        public string DocumentType => "W4";

        /// <inheritdoc />
        public async Task<object> ProcessAsync(string rawOcrData, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                throw new ArgumentException("Raw OCR data cannot be null or empty.", nameof(rawOcrData));
            }

            _logger.LogInformation("Processing W-4 document");

            var processedW4 = new ProcessedW4
            {
                RawOcrText = rawOcrData,
                ProcessedAt = DateTime.UtcNow
            };

            try
            {
                // Extract W-4 data from raw OCR text
                processedW4.W4Data = await ExtractW4DataAsync(rawOcrData, cancellationToken);

                // Calculate confidence score based on extracted fields
                processedW4.ConfidenceScore = CalculateConfidenceScore(processedW4.W4Data);

                // Validate the processed data
                processedW4.Validate();

                _logger.LogInformation("W-4 processed successfully with confidence score: {ConfidenceScore}",
                    processedW4.ConfidenceScore);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing W-4 document");
                processedW4.ValidationErrors.Add($"Processing error: {ex.Message}");
            }

            return processedW4;
        }

        /// <inheritdoc />
        public bool CanProcess(string rawOcrData)
        {
            if (string.IsNullOrWhiteSpace(rawOcrData))
            {
                return false;
            }

            // Check for W-4 specific patterns
            var hasFormTitle = Patterns.FormTitle.IsMatch(rawOcrData);
            var hasW4Text = rawOcrData.IndexOf("W-4", StringComparison.OrdinalIgnoreCase) >= 0 ||
                           rawOcrData.IndexOf("W4", StringComparison.OrdinalIgnoreCase) >= 0;

            // Check for W-4 specific keywords
            var w4Keywords = new[]
            {
                "withholding", "allowance", "filing status", "dependents",
                "employee's withholding", "single", "married", "head of household",
                "social security number", "multiple jobs", "claim exempt"
            };

            var keywordCount = w4Keywords.Count(keyword => 
                rawOcrData.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0);

            // Consider it a W-4 if we have the form title or W-4 text and at least 3 keywords
            return (hasFormTitle || hasW4Text) && keywordCount >= 3;
        }

        /// <summary>
        /// Extracts W-4 data from raw OCR text.
        /// </summary>
        private async Task<W4Data> ExtractW4DataAsync(string rawText, CancellationToken cancellationToken)
        {
            return await Task.Run(() =>
            {
                var w4Data = new W4Data();

                // Determine form version first
                DetermineFormVersion(rawText, w4Data);

                // Extract employee information
                ExtractEmployeeInformation(rawText, w4Data);

                // Extract SSN  
                ExtractAndValidateSSN(rawText, w4Data);

                // Extract filing status
                ExtractFilingStatus(rawText, w4Data);

                // Extract withholding information
                ExtractWithholdingInformation(rawText, w4Data);

                // Extract signature and date
                ExtractSignatureAndDate(rawText, w4Data);
                
                // Extract employer information (optional)
                ExtractEmployerInformation(rawText, w4Data);

                return w4Data;
            }, cancellationToken);
        }

        /// <summary>
        /// Determines the W-4 form version from the raw text.
        /// </summary>
        private void DetermineFormVersion(string rawText, W4Data w4Data)
        {
            var yearMatch = Patterns.FormYear.Match(rawText);
            if (yearMatch.Success)
            {
                // Try first capture group
                if (!string.IsNullOrWhiteSpace(yearMatch.Groups[1].Value))
                {
                    w4Data.FormVersion = yearMatch.Groups[1].Value;
                }
                // Try second capture group (for Rev. format)
                else if (!string.IsNullOrWhiteSpace(yearMatch.Groups[2].Value))
                {
                    w4Data.FormVersion = yearMatch.Groups[2].Value;
                }
            }

            // If no year found, try to determine by content
            if (string.IsNullOrEmpty(w4Data.FormVersion))
            {
                // Check for post-2020 specific sections
                if (rawText.IndexOf("Step 1", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    rawText.IndexOf("Step 2", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    w4Data.FormVersion = "2020"; // Assume current format
                }
                // Check for pre-2020 allowances
                else if (Patterns.TotalAllowances.IsMatch(rawText))
                {
                    w4Data.FormVersion = "2019"; // Assume last pre-2020 version
                }
            }

            _logger.LogDebug("Detected W-4 form version: {FormVersion}, Is pre-2020: {IsPreTwentyTwenty}",
                w4Data.FormVersion, w4Data.IsPreTwentyTwentyFormat);
        }

        /// <summary>
        /// Extracts employee information from the raw text.
        /// </summary>
        private void ExtractEmployeeInformation(string rawText, W4Data w4Data)
        {
            // Extract name
            var nameMatch = Patterns.Name.Match(rawText);
            if (nameMatch.Success)
            {
                w4Data.FirstName = nameMatch.Groups[1].Value.Trim();
                w4Data.MiddleName = nameMatch.Groups[2].Value.Trim();
                w4Data.LastName = nameMatch.Groups[3].Value.Trim();
            }
            else
            {
                // Try alternative patterns for name extraction
                ExtractNameAlternative(rawText, w4Data);
            }

            // Extract address
            var addressMatch = Patterns.Address.Match(rawText);
            if (addressMatch.Success)
            {
                w4Data.StreetAddress = addressMatch.Groups[1].Value.Trim();
            }

            // Extract city, state, zip
            var cityStateZipMatch = Patterns.CityStateZip.Match(rawText);
            if (cityStateZipMatch.Success)
            {
                w4Data.City = cityStateZipMatch.Groups[1].Value.Trim();
                w4Data.State = cityStateZipMatch.Groups[2].Value.Trim();
                w4Data.ZipCode = cityStateZipMatch.Groups[3].Value.Trim();
            }
            else
            {
                // Try alternative patterns for location extraction
                ExtractLocationAlternative(rawText, w4Data);
            }

            _logger.LogDebug("Extracted employee information - Name: {FullName}, Address: {Address}",
                w4Data.FullName, w4Data.StreetAddress);
        }

        /// <summary>
        /// Extracts name using alternative patterns.
        /// </summary>
        private void ExtractNameAlternative(string rawText, W4Data w4Data)
        {
            // Try line-based extraction for forms with structured layout
            var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                
                // Look for "Name" or "Employee name" label
                if (line.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0 &&
                    line.IndexOf("Employer", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    // Check same line first
                    var colonIndex = line.IndexOf(':');
                    if (colonIndex > 0 && colonIndex < line.Length - 1)
                    {
                        var namePart = line.Substring(colonIndex + 1).Trim();
                        ParseFullName(namePart, w4Data);
                    }
                    // Check next line
                    else if (i + 1 < lines.Length)
                    {
                        ParseFullName(lines[i + 1].Trim(), w4Data);
                    }
                    break;
                }
            }
        }

        /// <summary>
        /// Parses a full name string into first, middle, and last names.
        /// </summary>
        private void ParseFullName(string fullName, W4Data w4Data)
        {
            if (string.IsNullOrWhiteSpace(fullName))
                return;

            var nameParts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            if (nameParts.Length >= 1)
            {
                w4Data.FirstName = nameParts[0];
            }
            
            if (nameParts.Length == 2)
            {
                w4Data.LastName = nameParts[1];
            }
            else if (nameParts.Length >= 3)
            {
                w4Data.MiddleName = string.Join(" ", nameParts.Skip(1).Take(nameParts.Length - 2));
                w4Data.LastName = nameParts[nameParts.Length - 1];
            }
        }

        /// <summary>
        /// Extracts location using alternative patterns.
        /// </summary>
        private void ExtractLocationAlternative(string rawText, W4Data w4Data)
        {
            // Try to find state abbreviation pattern
            var statePattern = new Regex(@"\b([A-Z]{2})\s+(\d{5}(?:-\d{4})?)\b");
            var stateMatch = statePattern.Match(rawText);
            
            if (stateMatch.Success)
            {
                w4Data.State = stateMatch.Groups[1].Value;
                w4Data.ZipCode = stateMatch.Groups[2].Value;
                
                // Try to find city before the state
                var beforeState = rawText.Substring(0, stateMatch.Index);
                var cityPattern = new Regex(@"([A-Za-z\s]+?)\s*,?\s*$");
                var cityMatch = cityPattern.Match(beforeState);
                
                if (cityMatch.Success)
                {
                    w4Data.City = cityMatch.Groups[1].Value.Trim();
                }
            }
        }

        /// <summary>
        /// Extracts and validates SSN from the raw text.
        /// </summary>
        private void ExtractAndValidateSSN(string rawText, W4Data w4Data)
        {
            var ssnMatch = Patterns.SSN.Match(rawText);
            if (ssnMatch.Success)
            {
                var rawSSN = ssnMatch.Groups[1].Value;
                // Clean up SSN (remove spaces/dashes for validation)
                var cleanSSN = Regex.Replace(rawSSN, @"[\s\-]", "");
                
                if (IsValidSSN(cleanSSN))
                {
                    // Mask SSN for security - only store last 4 digits
                    w4Data.SSN = MaskSSN(cleanSSN);
                    _logger.LogDebug("Successfully extracted and masked SSN");
                }
                else
                {
                    _logger.LogWarning("Invalid SSN format detected: {SSN}", MaskSSN(cleanSSN));
                    w4Data.ValidationErrors.Add("Invalid SSN format detected");
                }
            }
            else
            {
                // Try alternative SSN patterns
                ExtractSSNAlternative(rawText, w4Data);
            }
        }

        /// <summary>
        /// Extracts SSN using alternative patterns.
        /// </summary>
        private void ExtractSSNAlternative(string rawText, W4Data w4Data)
        {
            // Try to find any 9-digit sequence that could be an SSN
            var nineDigitPattern = new Regex(@"\b(\d{3})[\s\-]?(\d{2})[\s\-]?(\d{4})\b");
            var matches = nineDigitPattern.Matches(rawText);
            
            foreach (Match match in matches)
            {
                var potentialSSN = $"{match.Groups[1].Value}{match.Groups[2].Value}{match.Groups[3].Value}";
                
                // Check if this looks like an SSN (not all zeros, not sequential)
                if (IsValidSSN(potentialSSN))
                {
                    // Check context around the match to confirm it's likely an SSN
                    var startIndex = Math.Max(0, match.Index - 50);
                    var length = Math.Min(100, rawText.Length - startIndex);
                    var context = rawText.Substring(startIndex, length).ToLowerInvariant();
                    
                    if (context.Contains("ssn") || context.Contains("social") || 
                        context.Contains("security") || context.Contains("tin"))
                    {
                        w4Data.SSN = MaskSSN(potentialSSN);
                        _logger.LogDebug("Found SSN using alternative pattern");
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Validates that an SSN follows proper format rules.
        /// </summary>
        private bool IsValidSSN(string ssn)
        {
            if (string.IsNullOrWhiteSpace(ssn))
                return false;
                
            // Must be 9 digits
            if (ssn.Length != 9 || !ssn.All(char.IsDigit))
                return false;
                
            // SSN validation rules:
            // - First 3 digits (area number) cannot be 000, 666, or 900-999
            // - Middle 2 digits (group number) cannot be 00
            // - Last 4 digits (serial number) cannot be 0000
            
            var area = int.Parse(ssn.Substring(0, 3));
            var group = int.Parse(ssn.Substring(3, 2));
            var serial = int.Parse(ssn.Substring(5, 4));
            
            if (area == 0 || area == 666 || area >= 900)
                return false;
                
            if (group == 0)
                return false;
                
            if (serial == 0)
                return false;
                
            return true;
        }

        /// <summary>
        /// Masks an SSN for security, showing only the last 4 digits.
        /// </summary>
        private string MaskSSN(string ssn)
        {
            if (string.IsNullOrWhiteSpace(ssn))
                return "XXX-XX-XXXX";
                
            var clean = Regex.Replace(ssn, @"\D", "");
            
            if (clean.Length >= 4)
            {
                var lastFour = clean.Substring(clean.Length - 4);
                return $"XXX-XX-{lastFour}";
            }
            
            return "XXX-XX-XXXX";
        }

        /// <summary>
        /// Extracts filing status from the raw text.
        /// </summary>
        private void ExtractFilingStatus(string rawText, W4Data w4Data)
        {
            // Check for Single or Married filing separately
            if (Patterns.Single.IsMatch(rawText))
            {
                w4Data.IsSingleOrMarriedFilingSeparately = true;
                w4Data.FilingStatus = "Single or Married filing separately";
                _logger.LogDebug("Detected filing status: Single or Married filing separately");
            }
            
            // Check for Married filing jointly
            if (Patterns.MarriedJointly.IsMatch(rawText))
            {
                w4Data.IsMarriedFilingJointly = true;
                w4Data.FilingStatus = "Married filing jointly";
                _logger.LogDebug("Detected filing status: Married filing jointly");
            }
            
            // Check for Head of household
            if (Patterns.HeadOfHousehold.IsMatch(rawText))
            {
                w4Data.IsHeadOfHousehold = true;
                w4Data.FilingStatus = "Head of household";
                _logger.LogDebug("Detected filing status: Head of household");
            }
            
            // If no checkbox patterns found, try alternative extraction
            if (string.IsNullOrEmpty(w4Data.FilingStatus))
            {
                ExtractFilingStatusAlternative(rawText, w4Data);
            }
            
            // Check for multiple jobs indicator (post-2020)
            if (!w4Data.IsPreTwentyTwentyFormat)
            {
                ExtractMultipleJobsInfo(rawText, w4Data);
            }
        }

        /// <summary>
        /// Extracts filing status using alternative patterns.
        /// </summary>
        private void ExtractFilingStatusAlternative(string rawText, W4Data w4Data)
        {
            var lines = rawText.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var line in lines)
            {
                var lowerLine = line.ToLowerInvariant();
                
                // Look for filing status keywords without checkbox patterns
                if (lowerLine.Contains("single") && !lowerLine.Contains("married"))
                {
                    w4Data.FilingStatus = "Single";
                    w4Data.IsSingleOrMarriedFilingSeparately = true;
                    _logger.LogDebug("Found filing status via text: Single");
                    break;
                }
                else if (lowerLine.Contains("married") && lowerLine.Contains("jointly"))
                {
                    w4Data.FilingStatus = "Married filing jointly";
                    w4Data.IsMarriedFilingJointly = true;
                    _logger.LogDebug("Found filing status via text: Married filing jointly");
                    break;
                }
                else if (lowerLine.Contains("married") && lowerLine.Contains("separately"))
                {
                    w4Data.FilingStatus = "Married filing separately";
                    w4Data.IsSingleOrMarriedFilingSeparately = true;
                    _logger.LogDebug("Found filing status via text: Married filing separately");
                    break;
                }
                else if (lowerLine.Contains("head") && lowerLine.Contains("household"))
                {
                    w4Data.FilingStatus = "Head of household";
                    w4Data.IsHeadOfHousehold = true;
                    _logger.LogDebug("Found filing status via text: Head of household");
                    break;
                }
            }
        }

        /// <summary>
        /// Extracts multiple jobs information for post-2020 forms.
        /// </summary>
        private void ExtractMultipleJobsInfo(string rawText, W4Data w4Data)
        {
            // Check if Step 2 section exists (multiple jobs)
            if (Patterns.MultipleJobs.IsMatch(rawText))
            {
                // Look for checkbox patterns in Step 2
                var step2Pattern = new Regex(
                    @"Step\s+2[:\s]*(.{0,500}?)(?=Step\s+3|$)",
                    RegexOptions.IgnoreCase | RegexOptions.Singleline);
                
                var step2Match = step2Pattern.Match(rawText);
                if (step2Match.Success)
                {
                    var step2Content = step2Match.Groups[1].Value;
                    
                    // Check for Option (a) - online estimator
                    if (step2Content.IndexOf("estimator", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        w4Data.UseOnlineEstimator = true;
                    }
                    
                    // Check for Option (b) - worksheet
                    if (step2Content.IndexOf("worksheet", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        w4Data.UseMultipleJobsWorksheet = true;
                    }
                    
                    // Check for Option (c) - two jobs total checkbox
                    var twoJobsPattern = new Regex(@"\[?\s*[X✓]\s*\]?\s*(?:two\s+jobs|checkbox)", 
                        RegexOptions.IgnoreCase);
                    if (twoJobsPattern.IsMatch(step2Content))
                    {
                        w4Data.TwoJobsTotal = true;
                        w4Data.HasMultipleJobs = true;
                    }
                }
            }
        }

        /// <summary>
        /// Extracts withholding information based on form version.
        /// </summary>
        private void ExtractWithholdingInformation(string rawText, W4Data w4Data)
        {
            if (w4Data.IsPreTwentyTwentyFormat)
            {
                // Extract pre-2020 allowances
                ExtractPreTwentyTwentyAllowances(rawText, w4Data);
            }
            else
            {
                // Extract post-2020 withholding information
                ExtractPostTwentyTwentyWithholding(rawText, w4Data);
            }
            
            // Check for exempt status (applies to both versions)
            if (Patterns.Exempt.IsMatch(rawText))
            {
                w4Data.IsExempt = true;
                _logger.LogDebug("Employee claims exempt from withholding");
            }
        }

        /// <summary>
        /// Extracts pre-2020 allowances information.
        /// </summary>
        private void ExtractPreTwentyTwentyAllowances(string rawText, W4Data w4Data)
        {
            // Extract total allowances
            var allowancesMatch = Patterns.TotalAllowances.Match(rawText);
            if (allowancesMatch.Success)
            {
                if (int.TryParse(allowancesMatch.Groups[1].Value, out int allowances))
                {
                    w4Data.TotalAllowances = allowances;
                    _logger.LogDebug("Extracted total allowances: {Allowances}", allowances);
                }
            }
            
            // Extract additional withholding amount
            var additionalPattern = new Regex(
                @"Additional\s+amount.*withhold[:\s]*\$?\s*([\d,]+\.?\d*)",
                RegexOptions.IgnoreCase);
            
            var additionalMatch = additionalPattern.Match(rawText);
            if (additionalMatch.Success)
            {
                if (decimal.TryParse(additionalMatch.Groups[1].Value.Replace(",", ""), out decimal additional))
                {
                    w4Data.ExtraWithholding = additional;
                    _logger.LogDebug("Extracted additional withholding: {Amount}", additional);
                }
            }
        }

        /// <summary>
        /// Extracts post-2020 withholding information.
        /// </summary>
        private void ExtractPostTwentyTwentyWithholding(string rawText, W4Data w4Data)
        {
            // Extract Step 3: Claim Dependents
            ExtractDependentsInfo(rawText, w4Data);
            
            // Extract Step 4: Other Adjustments
            ExtractOtherAdjustments(rawText, w4Data);
        }

        /// <summary>
        /// Extracts dependents information (Step 3 for post-2020 forms).
        /// </summary>
        private void ExtractDependentsInfo(string rawText, W4Data w4Data)
        {
            // Look for Step 3 section
            var step3Pattern = new Regex(
                @"Step\s+3[:\s]*(.{0,1000}?)(?=Step\s+4|Step\s+5|$)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            var step3Match = step3Pattern.Match(rawText);
            if (step3Match.Success)
            {
                var step3Content = step3Match.Groups[1].Value;
                
                // Extract qualifying children
                var childrenMatch = Patterns.QualifyingChildren.Match(step3Content);
                if (childrenMatch.Success)
                {
                    if (int.TryParse(childrenMatch.Groups[1].Value, out int children))
                    {
                        w4Data.QualifyingChildren = children;
                        _logger.LogDebug("Extracted qualifying children: {Count}", children);
                    }
                }
                
                // Extract other dependents
                var otherMatch = Patterns.OtherDependents.Match(step3Content);
                if (otherMatch.Success)
                {
                    if (int.TryParse(otherMatch.Groups[1].Value, out int other))
                    {
                        w4Data.OtherDependents = other;
                        _logger.LogDebug("Extracted other dependents: {Count}", other);
                    }
                }
                
                // Calculate or extract total claim amount
                var claimPattern = new Regex(
                    @"(?:total|claim\s+amount)[:\s]*\$?\s*([\d,]+\.?\d*)",
                    RegexOptions.IgnoreCase);
                
                var claimMatch = claimPattern.Match(step3Content);
                if (claimMatch.Success)
                {
                    if (decimal.TryParse(claimMatch.Groups[1].Value.Replace(",", ""), out decimal claim))
                    {
                        w4Data.DependentsClaimAmount = claim;
                    }
                }
                else if (w4Data.QualifyingChildren > 0 || w4Data.OtherDependents > 0)
                {
                    // Calculate based on standard amounts
                    w4Data.DependentsClaimAmount = 
                        (w4Data.QualifyingChildren * 2000m) + (w4Data.OtherDependents * 500m);
                }
            }
        }

        /// <summary>
        /// Extracts other adjustments (Step 4 for post-2020 forms).
        /// </summary>
        private void ExtractOtherAdjustments(string rawText, W4Data w4Data)
        {
            // Look for Step 4 section
            var step4Pattern = new Regex(
                @"Step\s+4[:\s]*(.{0,1000}?)(?=Step\s+5|Employee.*signature|$)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            var step4Match = step4Pattern.Match(rawText);
            string step4Content = step4Match.Success ? step4Match.Groups[1].Value : rawText;
            
            // Extract other income (4a)
            var otherIncomeMatch = Patterns.OtherIncome.Match(step4Content);
            if (otherIncomeMatch.Success)
            {
                if (decimal.TryParse(otherIncomeMatch.Groups[1].Value.Replace(",", ""), out decimal income))
                {
                    w4Data.OtherIncome = income;
                    _logger.LogDebug("Extracted other income: {Amount}", income);
                }
            }
            
            // Extract deductions (4b)
            var deductionsMatch = Patterns.Deductions.Match(step4Content);
            if (deductionsMatch.Success)
            {
                if (decimal.TryParse(deductionsMatch.Groups[1].Value.Replace(",", ""), out decimal deductions))
                {
                    w4Data.Deductions = deductions;
                    _logger.LogDebug("Extracted deductions: {Amount}", deductions);
                }
            }
            
            // Extract extra withholding (4c)
            var extraMatch = Patterns.ExtraWithholding.Match(step4Content);
            if (extraMatch.Success)
            {
                if (decimal.TryParse(extraMatch.Groups[1].Value.Replace(",", ""), out decimal extra))
                {
                    w4Data.ExtraWithholding = extra;
                    _logger.LogDebug("Extracted extra withholding: {Amount}", extra);
                }
            }
        }

        /// <summary>
        /// Extracts signature and date information.
        /// </summary>
        private void ExtractSignatureAndDate(string rawText, W4Data w4Data)
        {
            // Extract signature date
            var dateMatch = Patterns.SignatureDate.Match(rawText);
            if (dateMatch.Success)
            {
                if (DateTime.TryParse(dateMatch.Groups[1].Value, out DateTime date))
                {
                    w4Data.DateSigned = date;
                    _logger.LogDebug("Extracted signature date: {Date}", date);
                }
            }
            
            // Extract signature
            var signatureMatch = Patterns.SignatureLine.Match(rawText);
            if (signatureMatch.Success)
            {
                var signature = signatureMatch.Groups[1].Value.Trim();
                
                // Check if it's not just a line or placeholder text
                if (!string.IsNullOrWhiteSpace(signature) && 
                    !signature.Contains("_____") &&
                    !signature.Equals("signature", StringComparison.OrdinalIgnoreCase))
                {
                    w4Data.EmployeeSignature = signature;
                    w4Data.SignatureDetected = true;
                    _logger.LogDebug("Signature detected: {Signature}", signature.Substring(0, Math.Min(20, signature.Length)));
                }
            }
            
            // Alternative signature detection
            if (!w4Data.SignatureDetected)
            {
                DetectSignatureAlternative(rawText, w4Data);
            }
        }

        /// <summary>
        /// Detects signature using alternative methods.
        /// </summary>
        private void DetectSignatureAlternative(string rawText, W4Data w4Data)
        {
            // Look for Step 5 or signature section
            var signatureSectionPattern = new Regex(
                @"(?:Step\s+5|Employee.*signature|Sign\s+here)[:\s]*(.{0,200}?)(?=\n|Date|$)",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);
            
            var sectionMatch = signatureSectionPattern.Match(rawText);
            if (sectionMatch.Success)
            {
                var signatureArea = sectionMatch.Groups[1].Value.Trim();
                
                // Check for handwritten text indicators
                if (signatureArea.Length > 3 && 
                    !signatureArea.Contains("_____") &&
                    !signatureArea.Contains("Please sign"))
                {
                    // Look for cursive or script-like patterns (simplified check)
                    if (ContainsSignatureLikeText(signatureArea))
                    {
                        w4Data.SignatureDetected = true;
                        w4Data.EmployeeSignature = "[Signature Present]";
                        _logger.LogDebug("Alternative signature detection successful");
                    }
                }
            }
        }

        /// <summary>
        /// Checks if text contains signature-like patterns.
        /// </summary>
        private bool ContainsSignatureLikeText(string text)
        {
            // Simple heuristics for signature detection
            // In real OCR, signatures often appear as connected letters or special characters
            
            // Check for minimum length
            if (text.Length < 5)
                return false;
            
            // Check if it's not all uppercase (signatures are usually mixed case or cursive)
            if (text == text.ToUpperInvariant())
                return false;
            
            // Check if it contains some letters (not just symbols or numbers)
            var letterCount = text.Count(char.IsLetter);
            if (letterCount < 3)
                return false;
            
            // Check if it's not a common word or instruction
            var commonWords = new[] { "signature", "sign", "here", "employee", "date", "print", "name" };
            var lowerText = text.ToLowerInvariant();
            
            return !commonWords.Any(word => lowerText.Contains(word));
        }

        /// <summary>
        /// Extracts employer information (for employer use only section).
        /// </summary>
        private void ExtractEmployerInformation(string rawText, W4Data w4Data)
        {
            // Extract employer name
            var employerNameMatch = Patterns.EmployerName.Match(rawText);
            if (employerNameMatch.Success)
            {
                w4Data.EmployerName = employerNameMatch.Groups[1].Value.Trim();
                _logger.LogDebug("Extracted employer name: {Name}", w4Data.EmployerName);
            }
            
            // Extract employer EIN
            var einMatch = Patterns.EmployerEIN.Match(rawText);
            if (einMatch.Success)
            {
                w4Data.EmployerEIN = einMatch.Groups[1].Value.Trim();
                _logger.LogDebug("Extracted employer EIN: {EIN}", w4Data.EmployerEIN);
            }
            
            // Extract first date of employment if present
            var employmentDatePattern = new Regex(
                @"(?:First\s+date\s+of\s+employment|Employment\s+date)[:\s]*(\d{1,2}[/-]\d{1,2}[/-]\d{2,4})",
                RegexOptions.IgnoreCase);
            
            var employmentMatch = employmentDatePattern.Match(rawText);
            if (employmentMatch.Success)
            {
                if (DateTime.TryParse(employmentMatch.Groups[1].Value, out DateTime employmentDate))
                {
                    w4Data.FirstDateOfEmployment = employmentDate;
                    _logger.LogDebug("Extracted employment date: {Date}", employmentDate);
                }
            }
        }

        /// <summary>
        /// Calculates a confidence score based on the completeness of extracted data.
        /// </summary>
        private double CalculateConfidenceScore(W4Data w4Data)
        {
            var score = 0.0;
            var maxScore = 10.0;

            // Check for essential fields
            if (!string.IsNullOrWhiteSpace(w4Data.FirstName) || !string.IsNullOrWhiteSpace(w4Data.LastName))
                score += 2.0;
            if (!string.IsNullOrWhiteSpace(w4Data.SSN))
                score += 2.0;
            if (!string.IsNullOrWhiteSpace(w4Data.StreetAddress))
                score += 1.0;
            if (!string.IsNullOrWhiteSpace(w4Data.City) && !string.IsNullOrWhiteSpace(w4Data.State))
                score += 1.0;
            if (!string.IsNullOrWhiteSpace(w4Data.FilingStatus))
                score += 1.5;
            if (w4Data.SignatureDetected || !string.IsNullOrWhiteSpace(w4Data.EmployeeSignature))
                score += 1.5;
            if (w4Data.DateSigned.HasValue)
                score += 1.0;

            // Version-specific scoring
            if (w4Data.IsPreTwentyTwentyFormat)
            {
                if (w4Data.TotalAllowances.HasValue || w4Data.IsExempt)
                    score += 1.0;
            }
            else
            {
                if (w4Data.QualifyingChildren > 0 || w4Data.OtherDependents > 0 ||
                    w4Data.OtherIncome > 0 || w4Data.Deductions > 0 || w4Data.ExtraWithholding > 0)
                    score += 1.0;
            }

            return Math.Min(score / maxScore, 1.0);
        }
    }
}