namespace BoardGameAiDashboard.Application.Common.Interfaces;

/// <summary>
/// Splits raw document text into optimised chunks for vector embedding.
/// Implementation lives in the Infrastructure layer.
/// </summary>
public interface IDocumentChunker
{
    /// <summary>
    /// Chunk the given content into segments suitable for embedding.
    /// </summary>
    /// <param name="content">Raw document text (e.g. a game rule page).</param>
    /// <param name="gameId">Game this content belongs to (metadata).</param>
    /// <param name="sectionTitle">Logical section heading (metadata).</param>
    /// <returns>List of chunks with metadata.</returns>
    IReadOnlyList<DocumentChunk> Chunk(
        string content, Guid gameId, string sectionTitle);
}

/// <summary>
/// A single chunk of text ready for embedding, with associated metadata.
/// </summary>
public sealed record DocumentChunk(
    string Content,
    string SectionTitle,
    Guid GameId);
