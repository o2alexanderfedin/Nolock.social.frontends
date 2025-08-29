#!/bin/bash

# Function to check if a class is likely a data model/DTO
check_if_data_model() {
    local file=$1
    local class_name=$2
    
    # Count properties and methods
    property_count=$(grep -c "public.*{.*get;.*}" "$file" 2>/dev/null || echo 0)
    method_count=$(grep -E "public.*\(.*\).*{$" "$file" 2>/dev/null | grep -v "get;" | grep -v "set;" | wc -l || echo 0)
    
    # If more properties than methods, likely a data model
    if [ "$property_count" -gt 0 ] && [ "$method_count" -le 2 ]; then
        echo "DATA_MODEL: $file - $class_name (Props: $property_count, Methods: $method_count)"
    fi
}

# Find all model/DTO directories and files
find NoLock.Social.Core -type f -name "*.cs" | while read file; do
    # Check if file is in Models, Configuration, or has specific naming patterns
    if echo "$file" | grep -qE "(Models/|Configuration/|.*Model\.cs|.*DTO\.cs|.*Data\.cs|.*Config.*\.cs|.*Options\.cs|.*Settings\.cs|.*Result\.cs|.*Response\.cs|.*Request\.cs|.*EventArgs\.cs)"; then
        classes=$(grep -E "public\s+(class|record|struct)" "$file" | sed 's/.*\(class\|record\|struct\)\s\+\([A-Za-z0-9_]*\).*/\2/')
        for class in $classes; do
            check_if_data_model "$file" "$class"
        done
    fi
done | sort
