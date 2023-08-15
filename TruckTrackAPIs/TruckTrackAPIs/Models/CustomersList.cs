using System.ComponentModel.DataAnnotations;

namespace TruckTrackAPIs.Models
{
    public class CustomersList
    {
        [Key]
        public int CustId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? BusinessName { get; set; }
        public string? Address1 { get; set; }
        public string? Address2 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? Route { get; set; }
        public int? Stop { get; set; }
    }
}
