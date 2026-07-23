namespace BoardGameAiDashboard.Application.Common.Interfaces;

/// <summary>
/// Splits raw document text into optimised chunks for vector embedding.
/// Implementation lives in the Infrastructure layer.
/// </summary>
public interface IDocumentChunker
{
    /// <summary>
    /// Chunk a single section of content into segments suitable for embedding.
    /// </summary>
    /// <param name="content">Raw document text (e.g. a game rule page).</param>
    /// <param name="gameId">Game this content belongs to (metadata).</param>
    /// <param name="sectionTitle">Logical section heading (metadata).</param>
    /// <returns>List of chunks with metadata.</returns>
    IReadOnlyList<DocumentChunk> Chunk(
        string content, Guid gameId, string sectionTitle);

    /// <summary>
    /// Chunk multiple sections in batch. Overlap buffer resets between sections,
    /// guaranteeing semantic purity — no cross-section contamination.
    /// </summary>
    /// <param name="sections">Ordered list of named sections to chunk.</param>
    /// <param name="gameId">Game this content belongs to (metadata).</param>
    /// <returns>All chunks across all sections, preserving section order.</returns>
    IReadOnlyList<DocumentChunk> ChunkAll(
        IReadOnlyList<DocumentSection> sections, Guid gameId);
}

/// <summary>
/// Represents a single named section of a document, ready for chunking.
/// Used as batch input for <see cref="IDocumentChunker.ChunkAll"/>.
/// </summary>
public sealed record DocumentSection(string SectionTitle, string Content);

/// <summary>
/// A single chunk of text ready for embedding, with associated metadata.
/// </summary>
public sealed record DocumentChunk(
    string Content,
    string SectionTitle,
    Guid GameId);
