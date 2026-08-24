using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SocialApp.Models.Entities;

namespace SocialApp.Data;

/// <summary>
/// The EF Core database context.
/// Inheriting IdentityDbContext&lt;ApplicationUser&gt; brings in all the
/// AspNetUsers / AspNetRoles / AspNetUserClaims ... tables automatically.
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Post> Posts => Set<Post>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Like> Likes => Set<Like>();
    public DbSet<Follow> Follows => Set<Follow>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Always call base first so Identity can configure its own tables.
        base.OnModelCreating(builder);

        // ---------------- Post ----------------
        builder.Entity<Post>(entity =>
        {
            entity.HasOne(p => p.User)
                  .WithMany(u => u.Posts)
                  .HasForeignKey(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            // The feed always sorts by newest first, so index CreatedAt descending.
            entity.HasIndex(p => p.CreatedAt).IsDescending();
        });

        // ---------------- Comment ----------------
        builder.Entity<Comment>(entity =>
        {
            entity.HasOne(c => c.Post)
                  .WithMany(p => p.Comments)
                  .HasForeignKey(c => c.PostId)
                  .OnDelete(DeleteBehavior.Cascade);

            // Restrict (not Cascade) to avoid multiple cascade paths,
            // which SQL Server does not allow.
            entity.HasOne(c => c.User)
                  .WithMany(u => u.Comments)
                  .HasForeignKey(c => c.UserId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ---------------- Like ----------------
        builder.Entity<Like>(entity =>
        {
            entity.HasOne(l => l.Post)
                  .WithMany(p => p.Likes)
                  .HasForeignKey(l => l.PostId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(l => l.User)
                  .WithMany(u => u.Likes)
                  .HasForeignKey(l => l.UserId)
                  .OnDelete(DeleteBehavior.Restrict);

            // One like per user per post.
            entity.HasIndex(l => new { l.PostId, l.UserId }).IsUnique();
        });

        // ---------------- Follow ----------------
        builder.Entity<Follow>(entity =>
        {
            entity.HasOne(f => f.Follower)
                  .WithMany(u => u.Following)
                  .HasForeignKey(f => f.FollowerId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(f => f.Followee)
                  .WithMany(u => u.Followers)
                  .HasForeignKey(f => f.FolloweeId)
                  .OnDelete(DeleteBehavior.Restrict);

            // Cannot follow the same person twice.
            entity.HasIndex(f => new { f.FollowerId, f.FolloweeId }).IsUnique();
        });
    }
}
