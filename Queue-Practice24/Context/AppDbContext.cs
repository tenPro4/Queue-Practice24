using Microsoft.EntityFrameworkCore;
using Queue_Practice24.BackgroundServices;

namespace Queue_Practice24.Context
{
    public class AppDbContext : DbContext
    {
        public DbSet<VideoUpload> VideoUploads { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    }
}
