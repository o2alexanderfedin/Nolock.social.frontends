#!/usr/bin/env python3
"""
Cleanup script to remove implementation code from OCR architecture document.
Follows KISS, YAGNI, DRY, SOLID, TRIZ principles.
"""

import re
import sys

def clean_architecture_doc(filepath):
    """Remove implementation code and keep only architecture content."""
    
    with open(filepath, 'r') as f:
        lines = f.readlines()
    
    output = []
    in_code_block = False
    code_block_start = -1
    skip_block = False
    
    # Patterns that indicate implementation code (not architecture)
    impl_patterns = [
        r'^\s*private readonly',
        r'^\s*public async Task',
        r'^\s*public class.*Tests',
        r'^\s*\[Fact\]',
        r'^\s*\[Theory\]',
        r'^\s*Assert\.',
        r'^\s*var .* = new',
        r'^\s*_logger\.Log',
        r'^\s*await _.*\.',
        r'^\s*using var',
        r'^\s*try\s*{',
        r'^\s*catch\s*\(',
        r'^\s*throw new',
    ]
    
    # Classes that should be completely removed
    remove_classes = [
        'CASStorage', 'DocumentProcessingService', 'ReceiptMapper', 'W4Mapper',
        'MapperInitializer', 'OcrService', 'CachedOcrService', 'OcrServiceErrorHandler',
        'OfflineScanManager', 'OcrProcessingErrorHandler', 'CircuitBreakerOcrProxy',
        'OcrProcessingStateManager', 'CameraServiceTests', 'OCRServiceIntegrationTests',
        'Form1040Mapper', 'RetryPolicyConfiguration', 'ProcessingEntry', 'CASStateRecovery',
        'PerformanceMonitor', 'OCRResultCache'
    ]
    
    i = 0
    while i < len(lines):
        line = lines[i]
        
        # Check if this is a class we should remove
        for cls in remove_classes:
            if f'public class {cls}' in line:
                # Skip until we find the end of the class
                # Simple approach: skip until next heading or major section
                replacement = f'// {cls}: [Implementation details removed - see source code]\n'
                output.append(replacement)
                
                # Skip the implementation
                i += 1
                brace_count = 1 if '{' in line else 0
                while i < len(lines) and brace_count > 0:
                    if '{' in lines[i]:
                        brace_count += lines[i].count('{')
                    if '}' in lines[i]:
                        brace_count -= lines[i].count('}')
                    i += 1
                continue
        
        # Check for implementation patterns in code blocks
        if '```csharp' in line or '```cs' in line:
            in_code_block = True
            code_block_start = len(output)
            output.append(line)
            i += 1
            continue
        
        if in_code_block and '```' in line:
            in_code_block = False
            # Check if the code block contains implementation
            block_content = ''.join(output[code_block_start+1:])
            
            has_impl = False
            for pattern in impl_patterns:
                if re.search(pattern, block_content, re.MULTILINE):
                    has_impl = True
                    break
            
            # If it's implementation code, replace with description
            if has_impl and len(output) - code_block_start > 20:  # More than 20 lines is likely implementation
                # Remove the implementation and add a note
                output = output[:code_block_start]
                output.append('// [Implementation code removed - focus on architecture]\n')
            else:
                output.append(line)
            i += 1
            continue
        
        # For regular lines, just append
        output.append(line)
        i += 1
    
    return ''.join(output)

def main():
    filepath = '/Users/alexanderfedin/Projects/nolock.social/Nolock.social.frontend/docs/architecture/features/document-scanner/ocr-scanner-architecture.md'
    
    # Create backup
    with open(filepath, 'r') as f:
        backup = f.read()
    
    with open(filepath + '.backup', 'w') as f:
        f.write(backup)
    
    # Clean the document
    cleaned = clean_architecture_doc(filepath)
    
    # Write cleaned version
    with open(filepath + '.cleaned', 'w') as f:
        f.write(cleaned)
    
    # Count lines saved
    original_lines = len(backup.split('\n'))
    cleaned_lines = len(cleaned.split('\n'))
    
    print(f"Original: {original_lines} lines")
    print(f"Cleaned: {cleaned_lines} lines")
    print(f"Removed: {original_lines - cleaned_lines} lines ({100*(original_lines-cleaned_lines)/original_lines:.1f}%)")
    print(f"Backup saved to: {filepath}.backup")
    print(f"Cleaned version saved to: {filepath}.cleaned")

if __name__ == '__main__':
    main()