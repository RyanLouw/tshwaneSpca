namespace TshwaneSPCA.Data;

public sealed class SiteContent
{
    public required string Key { get; init; }
    public required string Value { get; set; }
    public long UpdatedAt { get; set; }
}

public enum AssetKind
{
    Newsletter,
    Gallery,
    Document
}

public sealed class PublicAsset
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public AssetKind Kind { get; init; }
    public required string ContentType { get; init; }
    public required string StorageName { get; init; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? AltText { get; set; }
    public DateOnly? EditionDate { get; set; }
    public long? PublishedAt { get; set; }
    public bool Archived { get; set; }
}
