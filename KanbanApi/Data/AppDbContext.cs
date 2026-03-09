using KanbanApi.Models;
using Microsoft.EntityFrameworkCore;

namespace KanbanApi.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardMember> BoardMembers => Set<BoardMember>();
    public DbSet<Column> Columns => Set<Column>();
    public DbSet<Card> Cards => Set<Card>();
    public DbSet<CardStateHistory> CardStateHistories => Set<CardStateHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BoardMember>()
            .HasKey(bm => new { bm.BoardId, bm.UserId });

        modelBuilder.Entity<BoardMember>()
            .HasOne(bm => bm.Board)
            .WithMany(b => b.Members)
            .HasForeignKey(bm => bm.BoardId);

        modelBuilder.Entity<BoardMember>()
            .HasOne(bm => bm.User)
            .WithMany(u => u.BoardMemberships)
            .HasForeignKey(bm => bm.UserId);

        modelBuilder.Entity<Board>()
            .HasOne(b => b.Owner)
            .WithMany()
            .HasForeignKey(b => b.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username)
            .IsUnique();
    }
}
