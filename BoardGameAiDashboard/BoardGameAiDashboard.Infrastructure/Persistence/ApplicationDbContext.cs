using System.Text.Json;
using BoardGameAiDashboard.Domain.Common;
using BoardGameAiDashboard.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BoardGameAiDashboard.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<Game> Games => Set<Game>();
        public DbSet<GameRuleChunk> GameRuleChunks => Set<GameRuleChunk>();
        public DbSet<GameCharacter> GameCharacters => Set<GameCharacter>();
        public DbSet<GameCard> GameCards => Set<GameCard>();
        public DbSet<MatchHistory> MatchHistories => Set<MatchHistory>();
        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. Game master table configuration
            modelBuilder.Entity<Game>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(1000);
                entity.HasQueryFilter(e => e.IsDeleted == false);
            });

            // 2. Rule chunk table configuration (RAG knowledge base)
            modelBuilder.Entity<GameRuleChunk>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.SectionTitle).HasMaxLength(200);
                entity.Property(e => e.QdrantPointId).IsRequired().HasMaxLength(50);

                entity.HasOne(d => d.Game)
                      .WithMany(p => p.RuleChunks)
                      .HasForeignKey(d => d.GameId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(e => e.IsDeleted == false);
            });

            // 3. Character table configuration (with JSON dynamic extension)
            modelBuilder.Entity<GameCharacter>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CodeName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);

                entity.Property(e => e.CustomProperties)
                      .HasConversion(CreateJsonConverter())
                      .Metadata.SetValueComparer(CreateJsonComparer());

                entity.HasOne(d => d.Game)
                      .WithMany(p => p.Characters)
                      .HasForeignKey(d => d.GameId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(e => e.IsDeleted == false);
            });

            // 4. Equipment card table configuration (with JSON dynamic extension)
            modelBuilder.Entity<GameCard>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CodeName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);

                entity.Property(e => e.CardProperties)
                      .HasConversion(CreateJsonConverter())
                      .Metadata.SetValueComparer(CreateJsonComparer());

                entity.HasOne(d => d.Game)
                      .WithMany(p => p.Cards)
                      .HasForeignKey(d => d.GameId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(e => e.IsDeleted == false);
            });

            // 5. Match history table configuration (with JSON dynamic extension for ML.NET prediction)
            modelBuilder.Entity<MatchHistory>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.GameFeatures)
                      .HasConversion(CreateJsonConverter())
                      .Metadata.SetValueComparer(CreateJsonComparer());

                entity.HasOne(d => d.Game)
                      .WithMany(p => p.MatchHistories)
                      .HasForeignKey(d => d.GameId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(e => e.IsDeleted == false);
            });

            // 6. User table configuration (JWT authentication)
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
                entity.Property(e => e.DisplayName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.PasswordHash).IsRequired().HasMaxLength(256);
                entity.HasIndex(e => e.Email).IsUnique();
                entity.HasQueryFilter(e => e.IsDeleted == false);
            });

            // 7. Chat message table configuration (RAG chat history)
            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.UserId).HasMaxLength(128);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.GameId);

                entity.Property(e => e.Sources)
                      .HasConversion(CreateJsonListConverter())
                      .Metadata.SetValueComparer(CreateJsonListComparer());

                entity.HasQueryFilter(e => e.IsDeleted == false);
            });

            // 8. Refresh token table configuration (JWT token rotation)
            modelBuilder.Entity<RefreshToken>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Token).IsRequired().HasMaxLength(512);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.Property(e => e.CreatedByIp).HasMaxLength(45);

                entity.HasOne(d => d.User)
                      .WithMany(p => p.RefreshTokens)
                      .HasForeignKey(d => d.UserId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasQueryFilter(e => e.IsDeleted == false);
            });
        }

        #region JSON Converter & Comparer Helpers (Value Converter & Comparer)

        private static ValueConverter<Dictionary<string, string>, string> CreateJsonConverter()
        {
            return new ValueConverter<Dictionary<string, string>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions)null!)
                     ?? new Dictionary<string, string>()
            );
        }

        private static ValueComparer<Dictionary<string, string>> CreateJsonComparer()
        {
            return new ValueComparer<Dictionary<string, string>>(
                (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions)null!) == JsonSerializer.Serialize(c2, (JsonSerializerOptions)null!),
                c => c == null ? 0 : JsonSerializer.Serialize(c, (JsonSerializerOptions)null!).GetHashCode(),
                c => JsonSerializer.Deserialize<Dictionary<string, string>>(JsonSerializer.Serialize(c, (JsonSerializerOptions)null!), (JsonSerializerOptions)null!)!
            );
        }

        private static ValueConverter<List<string>, string> CreateJsonListConverter()
        {
            return new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions)null!)
                     ?? new List<string>()
            );
        }

        private static ValueComparer<List<string>> CreateJsonListComparer()
        {
            return new ValueComparer<List<string>>(
                (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions)null!) == JsonSerializer.Serialize(c2, (JsonSerializerOptions)null!),
                c => c == null ? 0 : JsonSerializer.Serialize(c, (JsonSerializerOptions)null!).GetHashCode(),
                c => JsonSerializer.Deserialize<List<string>>(JsonSerializer.Serialize(c, (JsonSerializerOptions)null!), (JsonSerializerOptions)null!)!
            );
        }

        #endregion
    }
}
