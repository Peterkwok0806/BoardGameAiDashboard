using System.Text.RegularExpressions;
using BoardGameAiDashboard.Application.Common.Interfaces;

namespace BoardGameAiDashboard.Infrastructure.Services;

/// <summary>
/// Splits document text into overlapping chunks suitable for embedding.
/// Features:
/// - Dynamic chunk size based on content type (tables vs prose)
/// - Table boundary detection to avoid splitting mid-table
/// - Subtitle/heading recognition within sections
/// - Duplicate title prefix removal
/// </summary>
public sealed partial class DocumentChunker : IDocumentChunker
{
    /// <summary>Default maximum characters per chunk for prose.</summary>
    private const int DefaultMaxChunkSize = 500;

    /// <summary>Maximum characters per chunk for tables.</summary>
    private const int TableMaxChunkSize = 800;

    /// <summary>Overlap between consecutive chunks within the same section.</summary>
    private const int OverlapSize = 100;

    /// <summary>Minimum chunk count in a section before adding subtitle.</summary>
    private const int MinChunksForSubtitle = 3;

    /// <summary>Maximum length for section title (matches DB column size).</summary>
    private const int MaxSectionTitleLength = 200;

    /// <summary>Regex to detect table rows (tab-separated or aligned columns).</summary>
    [GeneratedRegex(@"^.+\t.+$|^\s{2,}\S", RegexOptions.Multiline)]
    private static partial Regex TableRowPattern();

    /// <summary>Regex to detect subtitle/heading lines within content.</summary>
    /// Only matches short, clear subtitle patterns (max ~60 chars) to avoid extracting long text.
    /// Pattern: [short], **short**, *short*, Title: or Step/Phase/Note N
    [GeneratedRegex(@"^(?:\[[^\]]{1,60}\]|(?:\*\*?[^*]{1,60}\*\*?)|(?:[A-Z][a-z]+(?:\s+[A-Za-z]+){0,3}:)|(?:(?:Step|Phase|Note|Warning|Tip)\s+\d+))", RegexOptions.Multiline)]
    private static partial Regex SubtitlePattern();

    /// <inheritdoc />
    public IReadOnlyList<DocumentChunk> Chunk(
        string content, Guid gameId, string sectionTitle)
    {
        return ChunkAll(new[] { new DocumentSection(sectionTitle, content) }, gameId);
    }

    /// <inheritdoc />
    public IReadOnlyList<DocumentChunk> ChunkAll(
        IReadOnlyList<DocumentSection> sections, Guid gameId)
    {
        if (sections is null || sections.Count == 0)
            return Array.Empty<DocumentChunk>();

        var allChunks = new List<DocumentChunk>();

        foreach (var section in sections)
        {
            var sectionChunks = ChunkSingleSection(
                section.Content, gameId, section.SectionTitle);

            // Add subtitle to chunks if section has many chunks
            if (sectionChunks.Count >= MinChunksForSubtitle)
            {
                for (int i = 0; i < sectionChunks.Count; i++)
                {
                    var chunk = sectionChunks[i];
                    var subtitle = i > 0 ? $" (Part {i + 1})" : "";
                    // Truncate combined title to fit DB column (200 chars)
                    var combinedTitle = chunk.SectionTitle + subtitle;
                    var finalTitle = combinedTitle.Length > MaxSectionTitleLength
                        ? combinedTitle[..MaxSectionTitleLength]
                        : combinedTitle;
                    allChunks.Add(new DocumentChunk(chunk.Content, finalTitle, chunk.GameId));
                }
            }
            else
            {
                allChunks.AddRange(sectionChunks);
            }
        }

        return allChunks.AsReadOnly();
    }

    /// <summary>
    /// Splits a single section's content into overlapping chunks.
    /// - Uses dynamic chunk size (larger for tables)
    /// - Respects table boundaries
    /// - Removes duplicate title prefixes
    /// </summary>
    private IReadOnlyList<DocumentChunk> ChunkSingleSection(
        string content, Guid gameId, string sectionTitle)
    {
        if (string.IsNullOrWhiteSpace(content))
            return Array.Empty<DocumentChunk>();

        var chunks = new List<DocumentChunk>();
        var cleanContent = content.Replace("\r\n", "\n").Replace("\r", "\n");

        // Determine content type and chunk size
        var hasTables = ContainsTables(cleanContent);
        var maxChunkSize = hasTables ? TableMaxChunkSize : DefaultMaxChunkSize;

        if (cleanContent.Length <= maxChunkSize)
        {
            var cleanedChunk = RemoveDuplicateTitlePrefix(cleanContent, sectionTitle);
            chunks.Add(new DocumentChunk(cleanedChunk.Trim(), sectionTitle, gameId));
            return chunks;
        }

        // Split by paragraphs and table rows
        var segments = SplitIntoSegments(cleanContent);
        var currentChunk = new System.Text.StringBuilder();
        var currentTitle = sectionTitle;

        foreach (var segment in segments)
        {
            // Check if adding this segment would exceed chunk size
            var wouldExceed = currentChunk.Length + segment.Length > maxChunkSize;

            // If this segment is a new subtitle, update the chunk title
            var subtitle = TryExtractSubtitle(segment);
            if (subtitle != null)
            {
                // Truncate combined title to fit DB column (200 chars)
                var combinedTitle = $"{sectionTitle} - {subtitle}";
                currentTitle = combinedTitle.Length > MaxSectionTitleLength
                    ? combinedTitle[..MaxSectionTitleLength]
                    : combinedTitle;
            }

            if (wouldExceed && currentChunk.Length > 0)
            {
                // Create chunk and handle overlap
                var chunkContent = RemoveDuplicateTitlePrefix(currentChunk.ToString(), sectionTitle);
                chunks.Add(new DocumentChunk(chunkContent.Trim(), currentTitle, gameId));

                // Keep last portion as overlap
                var remaining = currentChunk.ToString();
                if (remaining.Length > OverlapSize)
                {
                    currentChunk.Clear();
                    currentChunk.Append(remaining.AsSpan(remaining.Length - OverlapSize));
                    currentChunk.Append(' ');
                }
                else
                {
                    currentChunk.Clear();
                }
            }

            currentChunk.Append(segment).Append('\n');
        }

        // Add final chunk
        if (currentChunk.Length > 0)
        {
            var chunkContent = RemoveDuplicateTitlePrefix(currentChunk.ToString(), sectionTitle);
            chunks.Add(new DocumentChunk(chunkContent.Trim(), currentTitle, gameId));
        }

        return chunks.AsReadOnly();
    }

    /// <summary>
    /// Detects if content contains table structures (tab-separated or aligned columns).
    /// </summary>
    private static bool ContainsTables(string content)
    {
        var lines = content.Split('\n');
        var tableLineCount = 0;

        foreach (var line in lines)
        {
            if (TableRowPattern().IsMatch(line))
                tableLineCount++;
        }

        // Consider it a table if > 10% of lines match table patterns
        return lines.Length > 0 && (double)tableLineCount / lines.Length > 0.1;
    }

    /// <summary>
    /// Splits content into segments (paragraphs, table rows, subtitles).
    /// </summary>
    private static List<string> SplitIntoSegments(string content)
    {
        var segments = new List<string>();
        var lines = content.Split('\n');
        var currentSegment = new System.Text.StringBuilder();
        var isInTable = false;

        foreach (var line in lines)
        {
            var isTableRow = TableRowPattern().IsMatch(line);
            var isSubtitle = SubtitlePattern().IsMatch(line);
            var isEmpty = string.IsNullOrWhiteSpace(line);

            if (isEmpty)
            {
                if (currentSegment.Length > 0)
                {
                    segments.Add(currentSegment.ToString());
                    currentSegment.Clear();
                }
                isInTable = false;
                continue;
            }

            // Table row logic: group consecutive table rows together
            if (isTableRow)
            {
                if (!isInTable && currentSegment.Length > 0)
                {
                    segments.Add(currentSegment.ToString());
                    currentSegment.Clear();
                }
                isInTable = true;
                currentSegment.AppendLine(line);
            }
            else
            {
                if (isInTable && currentSegment.Length > 0)
                {
                    segments.Add(currentSegment.ToString());
                    currentSegment.Clear();
                }
                isInTable = false;
                currentSegment.AppendLine(line);
            }
        }

        if (currentSegment.Length > 0)
            segments.Add(currentSegment.ToString());

        return segments;
    }

    /// <summary>
    /// Attempts to extract a subtitle from a segment line.
    /// Returns null if no subtitle detected or if extracted text is too long.
    /// </summary>
    private static string? TryExtractSubtitle(string segment)
    {
        var firstLine = segment.Split('\n').FirstOrDefault()?.Trim();
        if (string.IsNullOrWhiteSpace(firstLine))
            return null;

        // Check for subtitle pattern
        if (SubtitlePattern().IsMatch(firstLine))
        {
            // Clean up common subtitle formats and limit length
            var cleaned = firstLine.TrimStart('[', '*', '-', '#')
                                   .TrimEnd(']', '*', '-', '#', ':')
                                   .Trim();

            // Only accept short subtitles (max 50 chars after cleaning)
            if (cleaned.Length > 0 && cleaned.Length <= 50)
                return cleaned;
        }

        return null;
    }

    /// <summary>
    /// Removes duplicate title prefix from chunk content.
    /// When a chunk starts with the same text as the section title, remove it.
    /// </summary>
    private static string RemoveDuplicateTitlePrefix(string content, string sectionTitle)
    {
        if (string.IsNullOrWhiteSpace(content) || string.IsNullOrWhiteSpace(sectionTitle))
            return content;

        var cleanTitle = sectionTitle.Trim();
        var lines = content.Split('\n', 2); // Only split first newline

        if (lines.Length < 2)
            return content;

        var firstLine = lines[0].Trim();
        var secondLine = lines[1].Trim();

        // Check if first line matches section title (with optional punctuation)
        if (string.Equals(firstLine, cleanTitle, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(firstLine, cleanTitle + ":", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(firstLine, cleanTitle + ".", StringComparison.OrdinalIgnoreCase))
        {
            // Check if second line is similar (avoid removing genuine content)
            if (secondLine.StartsWith(cleanTitle, StringComparison.OrdinalIgnoreCase) &&
                secondLine.Length < cleanTitle.Length + 5)
            {
                return content.Split('\n', 2)[1]; // Remove first line
            }
        }

        return content;
    }
}
