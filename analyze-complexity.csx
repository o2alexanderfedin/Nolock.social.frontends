#!/usr/bin/env dotnet-script
#r "nuget: Microsoft.CodeAnalysis.CSharp, 4.8.0"
#r "nuget: Microsoft.CodeAnalysis.CSharp.Workspaces, 4.8.0"
#r "nuget: Newtonsoft.Json, 13.0.3"

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json;

public class ComplexityAnalyzer : CSharpSyntaxWalker
{
    private readonly string _filePath;
    private readonly List<MethodComplexity> _methods = new List<MethodComplexity>();
    private int _currentComplexity;
    private string _currentMethod;
    private string _currentClass;

    public ComplexityAnalyzer(string filePath)
    {
        _filePath = filePath;
    }

    public List<MethodComplexity> Methods => _methods;

    public override void VisitClassDeclaration(ClassDeclarationSyntax node)
    {
        var previousClass = _currentClass;
        _currentClass = node.Identifier.Text;
        base.VisitClassDeclaration(node);
        _currentClass = previousClass;
    }

    public override void VisitMethodDeclaration(MethodDeclarationSyntax node)
    {
        _currentMethod = node.Identifier.Text;
        _currentComplexity = 1; // Base complexity
        
        base.VisitMethodDeclaration(node);
        
        _methods.Add(new MethodComplexity
        {
            FilePath = _filePath,
            ClassName = _currentClass ?? "Unknown",
            MethodName = _currentMethod,
            Complexity = _currentComplexity,
            LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1
        });
    }

    public override void VisitConstructorDeclaration(ConstructorDeclarationSyntax node)
    {
        _currentMethod = node.Identifier.Text + "(Constructor)";
        _currentComplexity = 1;
        
        base.VisitConstructorDeclaration(node);
        
        _methods.Add(new MethodComplexity
        {
            FilePath = _filePath,
            ClassName = _currentClass ?? "Unknown",
            MethodName = _currentMethod,
            Complexity = _currentComplexity,
            LineNumber = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1
        });
    }

    public override void VisitIfStatement(IfStatementSyntax node)
    {
        _currentComplexity++;
        base.VisitIfStatement(node);
    }

    public override void VisitWhileStatement(WhileStatementSyntax node)
    {
        _currentComplexity++;
        base.VisitWhileStatement(node);
    }

    public override void VisitForStatement(ForStatementSyntax node)
    {
        _currentComplexity++;
        base.VisitForStatement(node);
    }

    public override void VisitForEachStatement(ForEachStatementSyntax node)
    {
        _currentComplexity++;
        base.VisitForEachStatement(node);
    }

    public override void VisitDoStatement(DoStatementSyntax node)
    {
        _currentComplexity++;
        base.VisitDoStatement(node);
    }

    public override void VisitSwitchStatement(SwitchStatementSyntax node)
    {
        _currentComplexity += node.Sections.Count;
        base.VisitSwitchStatement(node);
    }

    public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
    {
        _currentComplexity++;
        base.VisitConditionalExpression(node);
    }

    public override void VisitCatchClause(CatchClauseSyntax node)
    {
        _currentComplexity++;
        base.VisitCatchClause(node);
    }

    public override void VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        if (node.IsKind(SyntaxKind.LogicalAndExpression) || 
            node.IsKind(SyntaxKind.LogicalOrExpression) ||
            node.IsKind(SyntaxKind.CoalesceExpression))
        {
            _currentComplexity++;
        }
        base.VisitBinaryExpression(node);
    }
}

public class MethodComplexity
{
    public string FilePath { get; set; }
    public string ClassName { get; set; }
    public string MethodName { get; set; }
    public int Complexity { get; set; }
    public int LineNumber { get; set; }
}

public class ComplexityReport
{
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public int TotalMethods { get; set; }
    public int HighComplexityMethods { get; set; }
    public int CriticalComplexityMethods { get; set; }
    public double AverageComplexity { get; set; }
    public int MaxComplexity { get; set; }
    public List<MethodComplexity> TopComplexMethods { get; set; }
    public List<MethodComplexity> CriticalMethods { get; set; }
    public Dictionary<string, int> ComplexityDistribution { get; set; }
}

// Main execution
var sourceDirectory = Path.Combine(Directory.GetCurrentDirectory(), "NoLock.Social.Core");
var allMethods = new List<MethodComplexity>();

// Process all C# files
var csFiles = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
    .Where(f => !f.Contains("/obj/") && 
                !f.Contains("/bin/") && 
                !f.Contains("/Models/") &&
                !f.Contains("/Configuration/") &&
                !f.Contains("/Generated/") &&
                !f.Contains("EventArgs.cs") &&
                !f.Contains("Data.cs") &&
                !f.Contains("Config.cs") &&
                !f.Contains("Options.cs") &&
                !f.Contains("Settings.cs") &&
                !f.Contains("Request.cs") &&
                !f.Contains("Response.cs") &&
                !f.Contains("DTO.cs"));

foreach (var file in csFiles)
{
    var code = File.ReadAllText(file);
    var tree = CSharpSyntaxTree.ParseText(code);
    var root = tree.GetRoot();
    
    var analyzer = new ComplexityAnalyzer(file.Replace(Directory.GetCurrentDirectory() + "/", ""));
    analyzer.Visit(root);
    allMethods.AddRange(analyzer.Methods);
}

// Generate report
var report = new ComplexityReport
{
    TotalMethods = allMethods.Count,
    HighComplexityMethods = allMethods.Count(m => m.Complexity > 10),
    CriticalComplexityMethods = allMethods.Count(m => m.Complexity > 20),
    AverageComplexity = allMethods.Any() ? allMethods.Average(m => m.Complexity) : 0,
    MaxComplexity = allMethods.Any() ? allMethods.Max(m => m.Complexity) : 0,
    TopComplexMethods = allMethods.OrderByDescending(m => m.Complexity).Take(20).ToList(),
    CriticalMethods = allMethods.Where(m => m.Complexity > 20).OrderByDescending(m => m.Complexity).ToList(),
    ComplexityDistribution = new Dictionary<string, int>
    {
        ["1-5 (Simple)"] = allMethods.Count(m => m.Complexity <= 5),
        ["6-10 (Moderate)"] = allMethods.Count(m => m.Complexity > 5 && m.Complexity <= 10),
        ["11-20 (Complex)"] = allMethods.Count(m => m.Complexity > 10 && m.Complexity <= 20),
        [">20 (Critical)"] = allMethods.Count(m => m.Complexity > 20)
    }
};

// Create complexity directory
Directory.CreateDirectory(".complexity");

// Save JSON report
File.WriteAllText(".complexity/report.json", JsonConvert.SerializeObject(report, Formatting.Indented));

// Save CSV for detailed analysis
using (var writer = new StreamWriter(".complexity/methods.csv"))
{
    writer.WriteLine("FilePath,ClassName,MethodName,Complexity,LineNumber,Risk");
    foreach (var method in allMethods.OrderByDescending(m => m.Complexity))
    {
        var risk = method.Complexity > 20 ? "CRITICAL" : 
                   method.Complexity > 10 ? "HIGH" : 
                   method.Complexity > 5 ? "MEDIUM" : "LOW";
        writer.WriteLine($"\"{method.FilePath}\",\"{method.ClassName}\",\"{method.MethodName}\",{method.Complexity},{method.LineNumber},\"{risk}\"");
    }
}

// Print summary
Console.WriteLine("Cyclomatic Complexity Analysis Complete");
Console.WriteLine("========================================");
Console.WriteLine($"Total Methods Analyzed: {report.TotalMethods}");
Console.WriteLine($"Average Complexity: {report.AverageComplexity:F2}");
Console.WriteLine($"Maximum Complexity: {report.MaxComplexity}");
Console.WriteLine($"Methods with High Complexity (>10): {report.HighComplexityMethods}");
Console.WriteLine($"Methods with Critical Complexity (>20): {report.CriticalComplexityMethods}");
Console.WriteLine();
Console.WriteLine("Complexity Distribution:");
foreach (var kvp in report.ComplexityDistribution)
{
    Console.WriteLine($"  {kvp.Key}: {kvp.Value} methods");
}

if (report.CriticalMethods.Any())
{
    Console.WriteLine();
    Console.WriteLine("CRITICAL: Methods with complexity > 20:");
    foreach (var method in report.CriticalMethods)
    {
        Console.WriteLine($"  - {method.ClassName}.{method.MethodName}: {method.Complexity} (Line {method.LineNumber})");
        Console.WriteLine($"    File: {method.FilePath}");
    }
}

Console.WriteLine();
Console.WriteLine("Reports saved to:");
Console.WriteLine("  - .complexity/report.json");
Console.WriteLine("  - .complexity/methods.csv");