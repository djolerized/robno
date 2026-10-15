namespace Textil.Planning.Domain;

/// <summary>A rule violation. Messages are user-facing (Serbian).</summary>
public class DomainException(IReadOnlyList<string> errors) : Exception(string.Join(" ", errors))
{
    public DomainException(string error) : this([error]) { }

    public IReadOnlyList<string> Errors { get; } = errors;
}

public class PermissionDeniedException() : DomainException("Nemate dozvolu za ovu akciju.");
