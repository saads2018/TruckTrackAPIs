using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using System.Collections.Generic;
using TruckTrackAPIs.Models;

namespace TruckTrackAPIs.Data
{   
    public class ApplicationDbContext : IdentityDbContext<AdvancedUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
             : base(options)
        {
          
        }
        public DbSet<DeliveryDetails> deliveryDetails { get; set; }
        public DbSet<CustomersList> customersList { get; set; }
        public DbSet<DeliveriesList> deliveriesList { get; set; }
        public DbSet<RouteDeliveredDetails> routeDeliveredDetails { get; set; }

    }
}
