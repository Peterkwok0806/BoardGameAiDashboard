using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. 遊戲主表組態
            modelBuilder.Entity<Game>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Description).HasMaxLength(1000);
            });

            // 2. 規則切片表組態 (RAG 知識庫)
            modelBuilder.Entity<GameRuleChunk>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired();
                entity.Property(e => e.SectionTitle).HasMaxLength(200);
                entity.Property(e => e.QdrantPointId).IsRequired().HasMaxLength(50);

                // 設定一對多關聯
                entity.HasOne(d => d.Game)
                      .WithMany(p => p.RuleChunks)
                      .HasForeignKey(d => d.GameId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // 3. 角色表組態 (包含 JSON 動態擴充)
            modelBuilder.Entity<GameCharacter>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CodeName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);

                // 🌟 核心：將 Dictionary 轉換為 SQL Server 的 JSON 欄位
                entity.Property(e => e.CustomProperties)
                      .HasConversion(CreateJsonConverter())
                      .Metadata.SetValueComparer(CreateJsonComparer());

                entity.HasOne(d => d.Game)
                      .WithMany(p => p.Characters)
                      .HasForeignKey(d => d.GameId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // 4. 💡 裝備卡牌表組態 (包含 JSON 動態擴充)
            modelBuilder.Entity<GameCard>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.CodeName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);

                // 🌟 核心：將 Dictionary 轉換為 SQL Server 的 JSON 欄位
                entity.Property(e => e.CardProperties)
                      .HasConversion(CreateJsonConverter())
                      .Metadata.SetValueComparer(CreateJsonComparer());

                entity.HasOne(d => d.Game)
                      .WithMany(p => p.Cards)
                      .HasForeignKey(d => d.GameId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // 5. 💡 對戰歷史表組態 (包含 JSON 動態擴充 - ML.NET 預測用)
            modelBuilder.Entity<MatchHistory>(entity =>
            {
                entity.HasKey(e => e.Id);

                // 🌟 核心：將 Dictionary 轉換為 SQL Server 的 JSON 欄位
                entity.Property(e => e.GameFeatures)
                      .HasConversion(CreateJsonConverter())
                      .Metadata.SetValueComparer(CreateJsonComparer());

                entity.HasOne(d => d.Game)
                      .WithMany(p => p.MatchHistories)
                      .HasForeignKey(d => d.GameId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

        }

        #region JSON 轉換與比較輔助器 (Value Converter & Comparer)

        private static ValueConverter<Dictionary<string, string>, string> CreateJsonConverter()
        {
            return new ValueConverter<Dictionary<string, string>, string>(
                // 存入資料庫時：C# Dictionary 轉成 JSON 字串
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions)null!),
                // 從資料庫撈出時：JSON 字串 還原成 C# Dictionary
                v => JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions)null!)
                     ?? new Dictionary<string, string>()
            );
        }

        // 💡 負責讓 EF Core 追蹤物件內部的變化（防止更新失敗）
        private static ValueComparer<Dictionary<string, string>> CreateJsonComparer()
        {
            return new ValueComparer<Dictionary<string, string>>(
                // 比較兩個字典的 JSON 字串是否相同
                (c1, c2) => JsonSerializer.Serialize(c1, (JsonSerializerOptions)null!) == JsonSerializer.Serialize(c2, (JsonSerializerOptions)null!),
                // 產生雜湊碼
                c => c == null ? 0 : JsonSerializer.Serialize(c, (JsonSerializerOptions)null!).GetHashCode(),
                // 複製一份全新的字典供追蹤使用
                c => JsonSerializer.Deserialize<Dictionary<string, string>>(JsonSerializer.Serialize(c, (JsonSerializerOptions)null!), (JsonSerializerOptions)null!)!
            );
        }

        #endregion
    }
}
