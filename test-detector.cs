using System;
using System.Collections.Generic;
using System.Linq;

// Test ContainsKeyword
static bool ContainsKeyword(string text, string keyword)
{
    // All text and keywords should already be lowercase at this point
    // For multi-word keywords or special patterns, just check contains
    if (keyword.Contains(' ') || keyword.Contains('-'))
    {
        return text.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }
    
    // For other single words, just use contains
    return text.Contains(keyword, StringComparison.OrdinalIgnoreCase);
}

// Test data
var text = "invoice invoice invoice invoice invoice";
var lowerText = text.ToLowerInvariant();
var keywords = new List<string> 
{ 
    "invoice", "bill to", "ship to", "invoice number", "due date",
    "terms", "net 30", "purchase order", "quantity", "unit price",
    "line total", "balance due", "remit to", "amount due", "payment terms"
};

Console.WriteLine($"Text: {lowerText}");
Console.WriteLine($"Looking for keywords...");

foreach (var keyword in keywords)
{
    if (ContainsKeyword(lowerText, keyword))
    {
        Console.WriteLine($"  Found: '{keyword}'");
    }
}

// Direct test
Console.WriteLine($"\nDirect contains test:");
Console.WriteLine($"  lowerText.Contains('invoice'): {lowerText.Contains("invoice")}");
Console.WriteLine($"  'invoice'.Contains(' '): {"invoice".Contains(' ')}");
Console.WriteLine($"  'invoice'.Contains('-'): {"invoice".Contains('-')}");