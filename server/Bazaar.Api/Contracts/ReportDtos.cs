namespace Bazaar.Api.Contracts;

public sealed record SalesBucketDto(string Date, int OrderCount, MoneyDto Revenue);

public sealed record SalesReportDto(
    IReadOnlyList<SalesBucketDto> Buckets,
    int TotalOrders,
    MoneyDto TotalRevenue);

public sealed record TopProductDto(string Sku, string Title, int QuantitySold, MoneyDto Revenue);

public sealed record LowStockDto(Guid VariantId, string Sku, string ProductTitle, int Available);

public sealed record DiscountUsageDto(string Code, string Type, int TimesUsed, int? UsageLimit);
