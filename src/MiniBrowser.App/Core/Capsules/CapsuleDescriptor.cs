namespace MiniBrowser.Core.Capsules;

public enum CapsuleKind
{
    Anonymous,
    Personal
}

public sealed record CapsuleDescriptor(
    string Id,
    string Name,
    CapsuleKind Kind,
    string? CachePath)
{
    public bool IsEphemeral => string.IsNullOrWhiteSpace(CachePath);
    public string StorageLabel => IsEphemeral ? "RAM-only" : "persistente isolada";
    public string DisplayLabel => Kind == CapsuleKind.Anonymous ? $"⚡ {Name}" : $"🔐 {Name}";

    public override string ToString() => DisplayLabel;
}
