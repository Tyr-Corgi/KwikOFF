#!/bin/bash
# Code Length Analysis Script
# Checks that all code files follow best practice length guidelines

set -e

# Configuration - Best Practice Limits
MAX_FILE_LENGTH=1000
WARN_FILE_LENGTH=500
MAX_CLASS_LENGTH=500
WARN_CLASS_LENGTH=300

# Exclusions
EXCLUDE_DIRS=(
    "Migrations"
    "bin"
    "obj"
    "wwwroot/lib"
    ".git"
    "node_modules"
)

# Colors
RED='\033[0;31m'
YELLOW='\033[1;33m'
GREEN='\033[0;32m'
NC='\033[0m' # No Color

# Build exclusion pattern
EXCLUDE_PATTERN=""
for dir in "${EXCLUDE_DIRS[@]}"; do
    EXCLUDE_PATTERN="${EXCLUDE_PATTERN} -not -path '*/${dir}/*'"
done

# Function to check file length
check_file_length() {
    local file=$1
    local lines=$(wc -l < "$file")
    
    if [ $lines -gt $MAX_FILE_LENGTH ]; then
        echo -e "${RED}❌ FAIL${NC}: $file ($lines lines > $MAX_FILE_LENGTH max)"
        return 1
    elif [ $lines -gt $WARN_FILE_LENGTH ]; then
        echo -e "${YELLOW}⚠️  WARN${NC}: $file ($lines lines > $WARN_FILE_LENGTH recommended)"
        return 0
    fi
    return 0
}

# Function to analyze C# classes
check_class_length() {
    local file=$1
    local in_class=0
    local class_name=""
    local class_start=0
    local line_num=0
    local has_error=0
    
    while IFS= read -r line; do
        ((line_num++))
        
        # Detect class declaration
        if [[ $line =~ ^[[:space:]]*(public|internal|private|protected)?[[:space:]]*(static|abstract|sealed)?[[:space:]]*class[[:space:]]+([A-Za-z0-9_]+) ]]; then
            if [ $in_class -eq 1 ]; then
                # End previous class
                local class_length=$((line_num - class_start))
                if [ $class_length -gt $MAX_CLASS_LENGTH ]; then
                    echo -e "${RED}❌ FAIL${NC}: Class '$class_name' in $file ($class_length lines > $MAX_CLASS_LENGTH max)"
                    has_error=1
                elif [ $class_length -gt $WARN_CLASS_LENGTH ]; then
                    echo -e "${YELLOW}⚠️  WARN${NC}: Class '$class_name' in $file ($class_length lines > $WARN_CLASS_LENGTH recommended)"
                fi
            fi
            class_name="${BASH_REMATCH[3]}"
            class_start=$line_num
            in_class=1
        fi
    done < "$file"
    
    # Check final class
    if [ $in_class -eq 1 ]; then
        local class_length=$((line_num - class_start))
        if [ $class_length -gt $MAX_CLASS_LENGTH ]; then
            echo -e "${RED}❌ FAIL${NC}: Class '$class_name' in $file ($class_length lines > $MAX_CLASS_LENGTH max)"
            has_error=1
        elif [ $class_length -gt $WARN_CLASS_LENGTH ]; then
            echo -e "${YELLOW}⚠️  WARN${NC}: Class '$class_name' in $file ($class_length lines > $WARN_CLASS_LENGTH recommended)"
        fi
    fi
    
    return $has_error
}

# Main analysis
echo "📊 Analyzing code files for best practice compliance..."
echo ""
echo "Limits:"
echo "  - Files: ${WARN_FILE_LENGTH} lines (warn), ${MAX_FILE_LENGTH} lines (fail)"
echo "  - Classes: ${WARN_CLASS_LENGTH} lines (warn), ${MAX_CLASS_LENGTH} lines (fail)"
echo ""

has_failures=0
file_count=0

# Find all C# and Razor files
while IFS= read -r -d '' file; do
    ((file_count++))
    
    # Check file length
    if ! check_file_length "$file"; then
        has_failures=1
    fi
    
    # Check class lengths for C# files
    if [[ $file == *.cs ]]; then
        if ! check_class_length "$file"; then
            has_failures=1
        fi
    fi
done < <(eval "find src tests -type f \( -name '*.cs' -o -name '*.razor' \) $EXCLUDE_PATTERN -print0" 2>/dev/null)

echo ""
echo "📈 Summary: Analyzed $file_count files"

if [ $has_failures -eq 1 ]; then
    echo ""
    echo -e "${RED}❌ Code quality check failed${NC}"
    echo "   Some files/classes exceed best practice length limits"
    echo ""
    echo "Recommended actions:"
    echo "  1. Refactor large files into smaller, focused components"
    echo "  2. Apply SOLID principles (Single Responsibility)"
    echo "  3. Extract helper classes or methods"
    echo "  4. Consider using partial classes for generated code"
    exit 1
else
    echo -e "${GREEN}✅ All files meet best practice guidelines${NC}"
    exit 0
fi


