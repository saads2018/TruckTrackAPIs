namespace TruckTrackAPIs.Models
{
    public class Users
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Email { get; set; }
        public string PasswordHash { get; set; }
        public string? FullName { get; set; }
        public bool? Driver { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Picture { get; set; }

    }
}
