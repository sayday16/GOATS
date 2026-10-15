using System;

namespace GoatStore.DataAccess.Entities
{
    public enum UserRole
    {
        Admin,
        Seller
    }

    public class User
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        public Guid StoreId { get; set; }
        public Store? Store { get; set; }
    }
}