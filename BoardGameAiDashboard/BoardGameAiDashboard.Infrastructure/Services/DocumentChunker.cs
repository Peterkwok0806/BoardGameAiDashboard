using BoardGameAiDashboard.Application.Common.Interfaces;

namespace BoardGameAiDashboard.Infrastructure.Services;

/// <summary>
/// Splits document text into overlapping chunks suitable for embedding.
/// Uses a simple sliding-window approach — no external NLP dependency.
/// </summary>
public sealed class DocumentChunker : IDocumentChunker
{
    /// <summary>Maximum characters per chunk.</summary>
    private const int MaxChunkSize = 500;

    /// <summary>Overlap between consecutive chunks (to preserve context).</summary>
    private const int OverlapSize = 100;

    public IReadOnlyList<DocumentChunk> Chunk(
        string content, Guid gameId, string sectionTitle)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Array.Empty<DocumentChunk>();

        var chunks = new List<DocumentChunk>();
        var cleanContent = content.Replace("\r\n", "\n").Replace("\r", "\n");

        if (cleanContent.Length <= MaxChunkSize)
        {
            chunks.Add(new DocumentChunk(cleanContent.Trim(), sectionTitle, gameId));
            return chunks;
        }

        // Split by paragraphs first, then combine into chunks
        var paragraphs = cleanContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        var currentChunk = new System.Text.StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            if (currentChunk.Length + paragraph.Length > MaxChunkSize && currentChunk.Length > 0)
            {
                chunks.Add(new DocumentChunk(currentChunk.ToString().Trim(), sectionTitle, gameId));

                // Keep last portion as overlap
                var remaining = currentChunk.ToString();
                if (remaining.Length > OverlapSize)
                {
                    currentChunk.Clear();
                    currentChunk.Append(remaining.AsSpan(remaining.Length - OverlapSize));
                    currentChunk.Append(" ");
                }
                else
                {
                    currentChunk.Clear();
                }
            }

            currentChunk.Append(paragraph).Append('\n');
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(new DocumentChunk(currentChunk.ToString().Trim(), sectionTitle, gameId));
        }

        return chunks.AsReadOnly();
    }
}
