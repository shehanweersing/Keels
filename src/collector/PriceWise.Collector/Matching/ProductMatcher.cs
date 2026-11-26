using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PriceWise.Collector.Models;

namespace PriceWise.Collector.Matching;

public class ProductMatcher
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "in", "of", "for", "with", "special", "promo", "pack", "new"
    };

    public record MatchEvaluationResult
    {
        public Product CanonicalProduct { get; init; } = null!;
        public string MatchMethod { get; init; } = "new_product";
        public decimal Confidence { get; init; } = 1.0m;
        public string VerificationStatus { get; init; } = "verified";
        public bool IsNewProduct { get; init; }
    }

    private static readonly string[] KnownBrands =
    {
        "Elephant House", "Happy Hen", "Surf Excel", "Harpic", "Coca-Cola", "Coca Cola", "Sprite", "Fanta", "Pepsi",
        "Milkmaid", "Ambewela", "Maliban", "Munchee", "Dilmah", "Watawala", "Highland", "Kotmale", "Anchor",
        "Pelwatte", "Richlife", "Nestomalt", "Nescafe", "Nestle", "Milo", "Maggi", "Knorr", "Kist", "MD",
        "Prima", "Sunlight", "Dettol", "Lifebuoy", "Signal", "Closeup", "Sunsilk", "Dove", "Lux", "Pears",
        "Astra", "Flora", "Meadowlea", "Harischandra", "Samaposha", "Ritzbury", "Kandos", "Edinborough",
        "Marina", "Fortune", "Araliya", "Nipuna", "Cic", "Raigam", "Motha", "Keells", "Cargills", "Arpico",
        "Krest", "Samahan", "Iodex", "Lipton", "Zesta", "Necto", "EGB", "Sera", "Kothmale", "Kraft",
        "Sanitarium", "Bega", "Ceylon Biscuits", "Little Lion", "Magic", "4Rever", "Kandos"
    };

    private static readonly Dictionary<string, string> AbbreviationMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["choc"] = "chocolate",
        ["choco"] = "chocolate",
        ["van"] = "vanilla",
        ["straw"] = "strawberry",
        ["fc"] = "full cream",
        ["f/c"] = "full cream",
        ["pkt"] = "pack",
        ["packet"] = "pack",
        ["btl"] = "bottle",
        ["tb"] = "tetra",
        ["flav"] = "flavoured",
        ["flavored"] = "flavoured",
        ["cond"] = "condensed",
        ["pwd"] = "powder",
        ["powd"] = "powder"
    };

    public static string ExtractBrandFromName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Unknown";

        foreach (var brand in KnownBrands)
        {
            if (Regex.IsMatch(name, $@"(?:^|\b){Regex.Escape(brand)}(?:\b|$)", RegexOptions.IgnoreCase))
                return brand;
        }

        return "Unknown";
    }

    public MatchEvaluationResult MatchOrCreateProduct(RawProductItem rawItem, IReadOnlyList<Product> existingCatalog)
    {
        // 1. Exact Barcode Match (Tier 1)
        if (!string.IsNullOrWhiteSpace(rawItem.Barcode))
        {
            var barcodeMatch = existingCatalog.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(p.Barcode) &&
                string.Equals(p.Barcode.Trim(), rawItem.Barcode.Trim(), StringComparison.OrdinalIgnoreCase));

            if (barcodeMatch != null)
            {
                return new MatchEvaluationResult
                {
                    CanonicalProduct = barcodeMatch,
                    MatchMethod = "barcode_exact",
                    Confidence = 1.0m,
                    VerificationStatus = "verified",
                    IsNewProduct = false
                };
            }
        }

        // 2. Brand + Unit + Quantity + Title Similarity Match (Tier 2 & 3)
        var rawBrand = string.IsNullOrWhiteSpace(rawItem.Brand) ? "Unknown" : rawItem.Brand.Trim();
        var rawTokens = Tokenize(rawItem.SourceProductName, rawBrand);

        bool rawIsUnknownBrand = string.Equals(rawBrand, "Unknown", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(rawBrand, "Generic", StringComparison.OrdinalIgnoreCase);

        Product? bestCandidate = null;
        decimal highestScore = 0;
        string bestMethod = "new_product";

        // Never auto-match items whose brand or quantity is unverified
        if (!rawIsUnknownBrand && rawItem.Quantity > 0)
        {
            foreach (var existing in existingCatalog)
            {
                if (existing.Quantity <= 0)
                    continue;

                // CRITICAL RULE: Quantity and Unit MUST match. Different sizes/quantities are separate products.
                if (!AreQuantitiesAndUnitsEquivalent(rawItem.Quantity, rawItem.Unit, existing.Quantity, existing.Unit))
                {
                    continue;
                }

                bool existingIsUnknownBrand = string.Equals(existing.Brand, "Unknown", StringComparison.OrdinalIgnoreCase)
                                           || string.Equals(existing.Brand, "Generic", StringComparison.OrdinalIgnoreCase);

                if (existingIsUnknownBrand || !string.Equals(rawBrand, existing.Brand, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var existingTokens = Tokenize(existing.CanonicalName, existing.Brand);
                var similarity = ComputeJaccardSimilarity(rawTokens, existingTokens);

                if (similarity >= 0.95m)
                {
                    return new MatchEvaluationResult
                    {
                        CanonicalProduct = existing,
                        MatchMethod = "brand_name_unit_exact",
                        Confidence = 0.98m,
                        VerificationStatus = "verified",
                        IsNewProduct = false
                    };
                }
                else if (similarity >= 0.80m && similarity > highestScore)
                {
                    highestScore = similarity;
                    bestCandidate = existing;
                    bestMethod = "high_confidence_heuristic";
                }
            }
        }

        // High confidence match
        if (bestCandidate != null && highestScore >= 0.85m)
        {
            return new MatchEvaluationResult
            {
                CanonicalProduct = bestCandidate,
                MatchMethod = bestMethod,
                Confidence = Math.Round(highestScore, 2),
                VerificationStatus = "verified",
                IsNewProduct = false
            };
        }

        // Moderate similarity -> flag for review, DO NOT merge into verified comparisons automatically
        // When uncertain, create a separate product to preserve data integrity
        var newCanonical = new Product
        {
            Id = Guid.NewGuid(),
            CanonicalName = CleanCanonicalName(rawItem.SourceProductName),
            Brand = rawBrand,
            Barcode = rawItem.Barcode,
            Quantity = rawItem.Quantity,
            Unit = rawItem.Unit,
            Category = rawItem.Category,
            CreatedAt = DateTime.UtcNow
        };

        return new MatchEvaluationResult
        {
            CanonicalProduct = newCanonical,
            MatchMethod = "new_product",
            Confidence = 1.0m,
            VerificationStatus = "verified",
            IsNewProduct = true
        };
    }

    public static bool AreQuantitiesAndUnitsEquivalent(decimal q1, string u1, decimal q2, string u2)
    {
        var normU1 = NormalizeUnit(u1);
        var normU2 = NormalizeUnit(u2);

        // Normalize grams and kilograms
        if (normU1 == "kg" && normU2 == "g")
            return (q1 * 1000m) == q2;
        if (normU1 == "g" && normU2 == "kg")
            return q1 == (q2 * 1000m);

        // Normalize liters and milliliters
        if (normU1 == "l" && normU2 == "ml")
            return (q1 * 1000m) == q2;
        if (normU1 == "ml" && normU2 == "l")
            return q1 == (q2 * 1000m);

        return normU1 == normU2 && q1 == q2;
    }

    private static string NormalizeUnit(string unit)
    {
        return unit.ToLowerInvariant().Trim() switch
        {
            "kg" or "kilogram" or "kilograms" => "kg",
            "g" or "gm" or "gms" or "gram" or "grams" => "g",
            "l" or "ltr" or "liter" or "litre" or "litres" => "l",
            "ml" or "millilitre" or "milliliter" => "ml",
            "pack" or "packs" or "pk" => "packs",
            _ => "pcs"
        };
    }

    private static HashSet<string> Tokenize(string text, string brand)
    {
        // Strip out quantity patterns like "500g", "1kg", "1000ml", "1.5L" so they don't skew token similarity
        var stripped = Regex.Replace(text, @"\b\d+(?:\.\d+)?\s*(?:kg|g|gm|gms|ml|l|ltr|litre|litres|pcs|packs)\b", " ", RegexOptions.IgnoreCase);
        var clean = Regex.Replace(stripped, @"[^\w\s]", " ").ToLowerInvariant();
        var rawTokens = clean.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var t in rawTokens)
        {
            var mapped = AbbreviationMap.TryGetValue(t, out var expanded) ? expanded : t;
            foreach (var sub in mapped.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (sub.Length <= 1 || StopWords.Contains(sub) || string.Equals(sub, brand, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (decimal.TryParse(sub, out _))
                    continue;

                tokens.Add(sub);
            }
        }

        return tokens;
    }

    private static decimal ComputeJaccardSimilarity(HashSet<string> s1, HashSet<string> s2)
    {
        if (s1.Count == 0 && s2.Count == 0)
            return 1.0m;
        if (s1.Count == 0 || s2.Count == 0)
            return 0.0m;

        var intersection = s1.Intersect(s2, StringComparer.OrdinalIgnoreCase).Count();
        var union = s1.Union(s2, StringComparer.OrdinalIgnoreCase).Count();

        return (decimal)intersection / union;
    }

    private static string CleanCanonicalName(string name)
    {
        var clean = Regex.Replace(name, @"\s+", " ").Trim();
        return clean;
    }
}
