# KwikOFF - Product Comparison & Data Enrichment Platform

**KwikOFF** is a professional product data comparison and enrichment system that matches your inventory against the OpenFoodFacts database to provide comprehensive product information, ensure data quality, and support FDA FSMA 204 compliance.

## 🎯 What Does KwikOFF Do?

KwikOFF helps you:
- ✅ **Import** product inventories from CSV/Excel files
- ✅ **Compare** your products against 2.8+ million products in OpenFoodFacts
- ✅ **Enrich** your data with nutrition facts, ingredients, allergens, and images
- ✅ **Export** matched and enriched product data with 48+ data fields
- ✅ **Track** FSMA 204 compliance fields (lot numbers, harvest dates, traceability)

---

## 🚀 Quick Start

### Prerequisites
- **.NET 10.0 SDK** or later
- **PostgreSQL 14+** database
- **OpenAI API Key** (optional, for AI-powered name matching)

### Installation

```bash
# Clone the repository
git clone https://github.com/yourusername/KwikOFF.git
cd KwikOFF

# Setup database
createdb kwikoff_db

# Update connection string in appsettings.json
# "ConnectionStrings:DefaultConnection": "Host=localhost;Database=kwikoff_db;Username=youruser;Password=yourpass"

# Run database migrations
cd src/KwikOff.Web
dotnet ef database update

# Run the application
dotnet run
```

Visit `https://localhost:5001` in your browser.

---

## 📋 How It Works

### 1. **Import Your Products**

Navigate to **Import** → Upload CSV/Excel file

**Automatic Column Detection:**
- The system automatically detects columns for: Barcode/UPC, Product Name, Brand, Price, Quantity
- Supports FSMA 204 fields: Lot Number, Harvest Date, Origin, Grower, Expiration Date
- Manual mapping available if auto-detection needs adjustment

**Supported Formats:**
- CSV files (UTF-8 encoding recommended)
- Excel files (.xlsx, .xls)
- Barcodes: UPC-A, UPC-E, EAN-8, EAN-13, GTIN-14, ISBN

### 2. **Sync OpenFoodFacts Database** (First Time Setup)

Navigate to **Database** → Click **Start Sync**

**What Happens:**
- Downloads the latest OpenFoodFacts database (~2.8 million products)
- Imports product data with nutrition facts, ingredients, allergens, and images
- Runs in background (can take 30-60 minutes for full sync)
- Status updates shown in real-time

**Frequency:** Run weekly or monthly to keep data current.

### 3. **Compare Products**

Navigate to **Compare** → Select your import batch → Click **Start Comparison**

**Intelligent Matching:**
1. **Primary Search:**
   - Exact barcode matching (UPC/EAN normalization)
   - 100% barcode match = Guaranteed match (name variations are informational only)

2. **Secondary Search** (when primary fails or confidence < 100%):
   - **Partial Barcode Matching**: Handles truncated or padded barcodes
   - **AI-Powered Name Normalization**: Uses OpenAI to normalize product names
   - **Multi-Field Scoring**: Considers brand, size, quantity for confidence scoring

**Results:**
- ✅ **Matched**: Product found with high confidence
- ⚠️ **Discrepancy**: Product found but with data differences
- ❌ **Unmatched**: Product not found in OpenFoodFacts
- 🔍 **Secondary Search Indicators**: Shows when AI or alternative matching was used

### 4. **Review Comparison Results**

**For Each Product, See:**
- Your product name vs. OpenFoodFacts name
- Match confidence score (0-100%)
- Discrepancy details (name variations, data differences)
- Link to OpenFoodFacts product page (opens in new tab)
- Secondary search method used (if applicable)

**Key Feature - Name Variations:**
- Products with 100% barcode match but different names show as "Matched" with an info badge
- Expandable details explain the name variation is informational, not an error

### 5. **Export Enriched Data**

Navigate to **Export** → Select fields → Click **Export**

**Available Fields (48+):**

**Your Data:**
- Barcode, Product Name, Brand, Description, Category
- Price, Quantity, Unit of Measure
- Supplier, Internal SKU

**OpenFoodFacts Data:**
- Nutrition: Energy, Fat, Carbs, Protein, Sugar, Sodium, Fiber
- Product Info: Brands, Categories, Labels, Quantity
- Images: Product image, Ingredients image, Nutrition image
- Ingredients: Full ingredient list, Allergens
- Quality: Nutri-Score, NOVA Group, Ecoscore

**FSMA 204 Compliance:**
- Lot Number, Batch Number, Harvest Date
- Origin, Grower, Packing Date, Cooling Date
- Expiration Date

**Comparison Metadata:**
- Match Status, Confidence Score, Names Match (Yes/No)
- Discrepancy flags, Secondary search indicators

**Features:**
- Select All checkboxes for each category
- Exports up to 250MB+ files
- Chunked downloads prevent browser crashes

---

## 🏗️ Architecture

### Technology Stack
- **Framework**: .NET 10.0 (Blazor Server)
- **Database**: PostgreSQL with Entity Framework Core
- **UI**: Blazor Server (real-time, server-rendered)
- **Architecture**: Clean Architecture with CQRS (MediatR)
- **AI Integration**: OpenAI API (GPT-3.5-turbo)

### Project Structure
```
src/KwikOff.Web/
├── Components/          # Blazor UI components
│   ├── Pages/          # Main application pages
│   └── Layout/         # App layout and navigation
├── Domain/             # Business entities and rules
│   ├── Entities/       # ImportedProduct, OpenFoodFactsProduct, ComparisonResult
│   ├── Enums/          # MatchStatus, BarcodeType
│   └── ValueObjects/   # ColumnMapping, ImportFieldRequirements
├── Features/           # Use cases (CQRS pattern)
│   ├── Products/       # Import product commands
│   ├── Comparison/     # Compare products queries
│   ├── Export/         # Export to CSV
│   └── Database/       # OpenFoodFacts sync
├── Infrastructure/     # Data access and services
│   ├── Data/           # Database context and configurations
│   ├── Services/       # Business logic services
│   └── BackgroundServices/ # Sync background service
└── Migrations/         # Database schema history
```

### Key Services
- **BarcodeNormalizer**: Normalizes UPC/EAN/GTIN formats for consistent matching
- **ComparisonService**: Core product comparison logic with AI integration
- **OpenFoodFactsDataImporter**: Downloads and imports OFF database
- **CsvExporter**: Generates enriched product exports
- **OpenAIService**: AI-powered product name normalization
- **ImageUrlService**: Fetches product images from OpenFoodFacts API

---

## 🧪 Testing

```bash
# Run all tests (53 tests)
cd tests/KwikOff.Tests
dotnet test

# Run with detailed output
dotnet test --verbosity detailed

# Run with code coverage
dotnet test --collect:"XPlat Code Coverage"
```

**Test Coverage:**
- ✅ **BarcodeNormalizer**: 29 tests - UPC/EAN normalization, type detection
- ✅ **JsonDataSanitizer**: 24 tests - Crowdsourced data cleaning
- ✅ **All tests passing** - 53/53 (100%)

See [Test Coverage Report](./docs/TEST_COVERAGE.md) for details.

---

## ⚙️ Configuration

### appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=kwikoff_db;Username=youruser;Password=yourpass"
  },
  "AI": {
    "Provider": "OpenAI",
    "ApiKey": "your-openai-api-key-here",
    "Model": "gpt-3.5-turbo",
    "MaxTokens": 50,
    "CacheExpiration": "01:00:00"
  },
  "SecondarySearch": {
    "Enabled": true,
    "TriggerThreshold": 1.0,
    "MinConfidence": 0.65,
    "MaxCandidates": 10,
    "Strategies": ["PartialBarcode", "AINameNorm", "MultiField"]
  },
  "OpenFoodFacts": {
    "JsonlUrl": "https://static.openfoodfacts.org/data/openfoodfacts-products.jsonl.gz",
    "ApiUrl": "https://world.openfoodfacts.org",
    "BatchSize": 1000,
    "MaxRetries": 3
  }
}
```

### Environment Variables (Production)
```bash
export ConnectionStrings__DefaultConnection="your-connection-string"
export AI__ApiKey="your-openai-api-key"
```

---

## 📊 Performance

**Proven Scalability:**
- ✅ Handles **200,000+ product** imports
- ✅ Exports **250MB+ CSV files** without crashes
- ✅ Batch comparisons complete in **under 5 seconds**
- ✅ Background sync processes **2.8 million products** from OpenFoodFacts

**Optimizations:**
- Chunked file downloads for large exports
- Batch database operations with transactions
- In-memory caching for frequently accessed data
- Parallel processing for API calls

---

## 🔒 Security & Compliance

### Data Security
- Multi-tenant data isolation (tenant ID architecture)
- SQL injection prevention (parameterized queries)
- Input validation and data sanitization
- Secure external API communication (HTTPS only)

### FDA FSMA 204 Compliance Support
- Lot/Batch number tracking
- Harvest date and origin tracking
- Grower and supplier information
- Cooling, packing, and expiration dates

---

## 📚 Documentation

- **[Project Summary](./docs/PROJECT_SUMMARY.md)** - Executive overview for management
- **[Test Coverage](./docs/TEST_COVERAGE.md)** - Comprehensive test report
- **[Code Quality](./docs/CODE_QUALITY.md)** - Development standards

---

## 🛠️ Development

### Code Quality Standards
- **Files**: Max 1000 lines (recommended < 500)
- **Classes**: Max 500 lines (recommended < 300)

```bash
# Install git hooks (one-time)
./scripts/install-hooks.sh

# Run manual quality check
./scripts/check-code-length.sh
```

### Database Migrations

```bash
# Create new migration
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update

# Rollback migration
dotnet ef database update PreviousMigrationName
```

---

## 🤝 Contributing

1. Follow Clean Architecture principles
2. Write tests for new features
3. Keep files under 1000 lines
4. Use meaningful commit messages
5. Update documentation for major changes

---

## 📝 License

[Your License Here]

---

## 🙏 Credits

- **OpenFoodFacts**: Product database (CC BY-SA 3.0)
- **OpenAI**: AI-powered name normalization
- **.NET Community**: Framework and tools

---

## 📞 Support

For questions or issues:
- Create a GitHub issue
- Email: [your-email@example.com]

---

**Built with ❤️ using .NET 10.0 and Blazor**
