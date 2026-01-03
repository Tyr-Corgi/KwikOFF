using System.Text.RegularExpressions;
using KwikOff.Web.Domain.Interfaces;

namespace KwikOff.Web.Infrastructure.Services.FieldDetectors;

/// <summary>
/// Detects Traceability Lot Code (TLC) columns - FSMA 204.
/// </summary>
public class TraceabilityLotCodeFieldDetector : FieldDetectorBase
{
    public override string FieldName => "TraceabilityLotCode";
    public override string DisplayName => "Traceability Lot Code (TLC)";
    public override bool IsFsma204 => true;

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "tlc", "traceabilitylotcode", "traceability_lot_code", "lotcode",
        "lot_code", "lotnumber", "lot_number", "batch", "batchnumber",
        "batch_number", "lot"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "traceability", "lotcode", "lotnumber", "batch"
    };
}

/// <summary>
/// Detects origin location columns - FSMA 204.
/// </summary>
public class OriginLocationFieldDetector : FieldDetectorBase
{
    public override string FieldName => "OriginLocation";
    public override string DisplayName => "Origin Location";
    public override bool IsFsma204 => true;

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "origin", "originlocation", "origin_location", "source",
        "sourcelocation", "source_location", "farm", "growerlocation",
        "grower_location", "originating_location"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "origin", "source", "farm", "grower"
    };
}

/// <summary>
/// Detects current location columns - FSMA 204.
/// </summary>
public class CurrentLocationFieldDetector : FieldDetectorBase
{
    public override string FieldName => "CurrentLocation";
    public override string DisplayName => "Current Location";
    public override bool IsFsma204 => true;

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "currentlocation", "current_location", "warehouse", "facility",
        "storelocation", "store_location", "location"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "current", "warehouse", "facility"
    };
}

/// <summary>
/// Detects destination location columns - FSMA 204.
/// </summary>
public class DestinationLocationFieldDetector : FieldDetectorBase
{
    public override string FieldName => "DestinationLocation";
    public override string DisplayName => "Destination Location";
    public override bool IsFsma204 => true;

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "destination", "destinationlocation", "destination_location",
        "shipto", "ship_to", "delivery_location", "receivinglocation"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "destination", "shipto", "delivery"
    };
}

/// <summary>
/// Base class for date field detectors.
/// </summary>
public abstract class DateFieldDetectorBase : FieldDetectorBase
{
    protected override double AnalyzeDataPattern(IReadOnlyList<string> sampleValues)
    {
        // Check for date patterns
        var datePatterns = new[]
        {
            @"^\d{1,2}/\d{1,2}/\d{2,4}$",   // MM/DD/YYYY
            @"^\d{4}-\d{2}-\d{2}$",          // YYYY-MM-DD
            @"^\d{1,2}-\d{1,2}-\d{2,4}$",   // MM-DD-YYYY
            @"^\d{8}$"                       // YYYYMMDD
        };

        var matchRatio = GetMatchingRatio(sampleValues, v =>
            datePatterns.Any(p => Regex.IsMatch(v.Trim(), p)) ||
            DateTime.TryParse(v, out _));

        if (matchRatio >= 0.80) return 0.85;
        if (matchRatio >= 0.50) return 0.65;
        return 0;
    }
}

/// <summary>
/// Detects harvest date columns - FSMA 204.
/// </summary>
public class HarvestDateFieldDetector : DateFieldDetectorBase
{
    public override string FieldName => "HarvestDate";
    public override string DisplayName => "Harvest Date";
    public override bool IsFsma204 => true;

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "harvestdate", "harvest_date", "harvested", "harvest",
        "dateofharvest", "date_of_harvest", "pickdate", "pick_date"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "harvest", "pick"
    };
}

/// <summary>
/// Detects pack date columns - FSMA 204.
/// </summary>
public class PackDateFieldDetector : DateFieldDetectorBase
{
    public override string FieldName => "PackDate";
    public override string DisplayName => "Pack Date";
    public override bool IsFsma204 => true;

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "packdate", "pack_date", "packed", "packagingdate",
        "packaging_date", "dateofpacking", "packaged_date"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "pack", "packaging"
    };
}

/// <summary>
/// Detects ship date columns - FSMA 204.
/// </summary>
public class ShipDateFieldDetector : DateFieldDetectorBase
{
    public override string FieldName => "ShipDate";
    public override string DisplayName => "Ship Date";
    public override bool IsFsma204 => true;

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "shipdate", "ship_date", "shipped", "shippingdate",
        "shipping_date", "dateofshipment", "dispatch_date"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "ship", "dispatch"
    };
}

/// <summary>
/// Detects receive date columns - FSMA 204.
/// </summary>
public class ReceiveDateFieldDetector : DateFieldDetectorBase
{
    public override string FieldName => "ReceiveDate";
    public override string DisplayName => "Receive Date";
    public override bool IsFsma204 => true;

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "receivedate", "receive_date", "received", "receivingdate",
        "receiving_date", "dateofreceiving", "arrival_date"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "receive", "receiving", "arrival"
    };
}

/// <summary>
/// Detects expiration date columns - FSMA 204.
/// </summary>
public class ExpirationDateFieldDetector : DateFieldDetectorBase
{
    public override string FieldName => "ExpirationDate";
    public override string DisplayName => "Expiration Date";
    public override bool IsFsma204 => true;

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "expirationdate", "expiration_date", "expires", "expiry",
        "expirydate", "expiry_date", "bestby", "best_by",
        "bestbefore", "best_before", "useby", "use_by", "sellby", "sell_by"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "expir", "bestby", "bestbefore", "useby", "sellby"
    };
}

/// <summary>
/// Detects reference document type columns - FSMA 204.
/// </summary>
public class ReferenceDocumentTypeFieldDetector : FieldDetectorBase
{
    public override string FieldName => "ReferenceDocumentType";
    public override string DisplayName => "Reference Document Type";
    public override bool IsFsma204 => true;

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "refdoctype", "ref_doc_type", "documenttype", "document_type",
        "referencedocumenttype", "reference_document_type", "doctype"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "documenttype", "doctype"
    };
}

/// <summary>
/// Detects reference document number columns - FSMA 204.
/// </summary>
public class ReferenceDocumentNumberFieldDetector : FieldDetectorBase
{
    public override string FieldName => "ReferenceDocumentNumber";
    public override string DisplayName => "Reference Document Number";
    public override bool IsFsma204 => true;

    protected override IReadOnlyList<string> ExactMatches => new[]
    {
        "refdocnumber", "ref_doc_number", "documentnumber", "document_number",
        "referencedocumentnumber", "reference_document_number", "docnumber",
        "ponumber", "po_number", "invoicenumber", "invoice_number"
    };

    protected override IReadOnlyList<string> PartialMatches => new[]
    {
        "documentnumber", "docnumber", "ponumber", "invoicenumber"
    };
}
