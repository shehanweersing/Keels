using System;
using System.Text.RegularExpressions;
using PriceWise.Collector.Models;

namespace PriceWise.Collector.Validation;

public static class DataValidator
{
    private static readonly HashSet<string> AllowedUnits = new(StringComparer.OrdinalIgnoreCase)
    {
        "g", "kg", "ml", "l", "pcs", "packs", "m", "cm"
    };

    public static bool ValidateRawProduct(RawProductItem item, out string? failureReason)
    {
        if (string.IsNullOrWhiteSpace(item.SourceSupermarketSlug))
        {
            failureReason = "Supermarket slug cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.SourceProductId))
        {
            failureReason = "Source product ID cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.SourceProductName))
        {
            failureReason = "Source product name cannot be empty.";
            return false;
        }

        if (item.Price <= 0)
        {
            failureReason = $"Invalid price '{item.Price}'. Price must be strictly greater than 0.";
            return false;
        }

        if (item.Price > 1_000_000)
        {
            failureReason = $"Unreasonable price '{item.Price}' exceeds 1,000,000 LKR safeguard limit.";
            return false;
        }

        if (item.OriginalPrice.HasValue && item.OriginalPrice.Value < item.Price)
        {
            failureReason = $"Original price '{item.OriginalPrice.Value}' cannot be lower than current discounted price '{item.Price}'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(item.ProductUrl) || !Uri.TryCreate(item.ProductUrl, UriKind.Absolute, out _))
        {
            failureReason = $"Invalid product URL '{item.ProductUrl}'.";
            return false;
        }

        if (item.Quantity < 0)
        {
            failureReason = $"Quantity cannot be negative: {item.Quantity}.";
            return false;
        }

        failureReason = null;
        return true;
    }

    public static (decimal Quantity, string Unit) ParseQuantityAndUnit(string text, decimal fallbackQuantity = 1, string fallbackUnit = "pcs")
    {
        if (string.IsNullOrWhiteSpace(text))
            return (fallbackQuantity, fallbackUnit);

        // Common patterns: "500g", "1kg", "400 ml", "1.5L", "200Ml", "250 G"
        var match = Regex.Match(text, @"(\d+(?:\.\d+)?)\s*(kg|g|ml|l|ltr|litre|litres|pcs|packs|nos|tablets|gm|gms)\b", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var rawNum = match.Groups[1].Value;
            var rawUnit = match.Groups[2].Value.ToLowerInvariant();

            if (decimal.TryParse(rawNum, out var qty))
            {
                var unit = rawUnit switch
                {
                    "kg" => "kg",
                    "g" or "gm" or "gms" => "g",
                    "ml" => "ml",
                    "l" or "ltr" or "litre" or "litres" => "l",
                    "packs" => "packs",
                    _ => "pcs"
                };

                return (qty, unit);
            }
        }

        return (fallbackQuantity, fallbackUnit);
    }

    public static string NormalizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Remove HTML entities, excess whitespace, common marketing hype
        var clean = Regex.Replace(name, @"&amp;|&quot;|&nbsp;|<[^>]+>", " ");
        clean = Regex.Replace(clean, @"\s+", " ").Trim();
        return clean;
    }
}
