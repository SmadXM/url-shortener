namespace Domain.Entities
{
    public class User
    {
        public Guid GlobalId { get; set;  } = Guid.NewGuid();
        public required string Email { get; set; }
        public required string PasswordHash { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public List<ShortLink> ShortLinks { get; set; } = new List<ShortLink>();
    }
}
