namespace GoatStore.DataAccess.Entities
{
    public class Store
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty; 
        public string StorePasswordHash { get; set; } = string.Empty; 

        public List<User> Users { get; set; } = new();
        public List<Product> Products { get; set; } = new();
    }
}