using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;
using Microsoft.Graph;
using Microsoft.Identity.Web;
using TruckTrackAPIs.Models;
using Npgsql;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNet.Identity;
using System.Data;
using System.Web.Http.Dependencies;
using TruckTrackAPIs.Data;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR;
using TruckTrack;
using RouteAttribute = Microsoft.AspNetCore.Mvc.RouteAttribute;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using NpgsqlTypes;
using Microsoft.JSInterop;
using RestSharp;
using System.Net;

namespace TruckTrackAPIs.Controllers
{
    [ApiController]
    [Route("[controller]")]
    //[RequiredScope(RequiredScopesConfigurationKey = "AzureAd:Scopes")]
    public class MasterController : Controller
    {
        private readonly IDownstreamWebApi _downstreamWebApi;
        private readonly GraphServiceClient _graphServiceClient;
        private readonly Microsoft.AspNetCore.Identity.UserManager<AdvancedUser> _userManager;
        private readonly ILogger<MasterController> _logger;
        private readonly ApplicationDbContext _dbContext;
        public readonly IConfiguration _configuration;
        public string conString { get; set; }
        private IHubContext<DatabaseChangesHub> _hub;



        public MasterController(ApplicationDbContext dbContext, Microsoft.AspNetCore.Identity.UserManager<AdvancedUser> userManager, ILogger<MasterController> logger, GraphServiceClient graphServiceClient, IDownstreamWebApi downstreamWebApi, IConfiguration configuration, IHubContext<DatabaseChangesHub> hub)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _logger = logger;
            _graphServiceClient = graphServiceClient; ;
            _downstreamWebApi = downstreamWebApi; ;
            _configuration = configuration;
            conString = _configuration.GetConnectionString("DefaultConnection");
            _hub = hub;
        }


        [HttpPost("GetBusinessOcr")]
        public async Task<string> GetBusinessOcr(ImageInput imageInput)
        {
            string result = "No Route Found, Please Try Again!";
            var cust = await getInfo(imageInput.Base64Image);

            if (!String.IsNullOrWhiteSpace(cust.BusinessName) && !String.IsNullOrWhiteSpace(cust.Address1))
            {
                if (imageInput.id != -1)
                {
                    var delivery = _dbContext.deliveriesList.Where(x => x.DeliveryId == imageInput.id).FirstOrDefault();
                    if (delivery.DeliveryRoutes.Contains(cust.BusinessName + ":"))
                    {
                        result = "Route Already Added To The List!";
                    }
                    else
                    {
                        result = "Route Found!";
                    }
                }
                else
                {
                    result = "Route Found!";
                }

                if(result=="Route Found!")
                {
                    string[] invoices = imageInput.id == -1 ? null : _dbContext.deliveriesList.Where(x => x.DeliveryId == imageInput.id).FirstOrDefault().Invoices;
                    string clientId = "1a9fbb7c205d3bd";
                    byte[] image = Convert.FromBase64String(imageInput.Base64Image);

                    try
                    {
                        if (invoices == null)
                            Array.Resize<string>(ref invoices, 1);
                        else
                            Array.Resize<string>(ref invoices, invoices.Length + 1);

                        invoices[invoices.Length - 1] = await UploadImage(clientId, image);

                        if (imageInput.id == -1)
                        {
                            DeliveriesList delivery = new DeliveriesList();
                            delivery.DeliveryDriver = imageInput.driverName;
                            delivery.AtLocation = new string[2];
                            delivery.AtLocation[0] = "None";
                            delivery.AtLocation[1] = "false";
                            delivery.DeliveryRoutes = cust.BusinessName + ":";
                            delivery.RouteAddresses = cust.Address1 + ":";
                            delivery.DriverUserName = imageInput.driverEmail;
                            delivery.DeliveryStatus = "Start Journey";
                            delivery.Invoices = invoices;
                            _dbContext.deliveriesList.Add(delivery);
                        }
                        else
                        {
                            var delivery = _dbContext.deliveriesList.Where(x => x.DeliveryId == imageInput.id).FirstOrDefault();
                            _dbContext.deliveriesList.Where(x => x.DeliveryId == imageInput.id).FirstOrDefault().DeliveryRoutes = delivery.DeliveryRoutes + cust.BusinessName + ":";
                            _dbContext.deliveriesList.Where(x => x.DeliveryId == imageInput.id).FirstOrDefault().RouteAddresses = delivery.RouteAddresses + cust.Address1 + ":";
                            _dbContext.deliveriesList.Where(x => x.DeliveryId == imageInput.id).FirstOrDefault().Invoices = invoices;
                        }

                        _dbContext.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error uploading image: " + ex.Message);
                    }
                    await _hub.Clients.All.SendAsync("ReceiveMessage");
                }

            }

            return result;
        }


        public static async Task<string> UploadImage(string clientId, byte[] image)
        {
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Client-ID", clientId);

                using (var form = new MultipartFormDataContent())
                {
                    form.Add(new ByteArrayContent(image), "image", "image.jpg");

                    using (var response = await httpClient.PostAsync("https://api.imgur.com/3/image", form))
                    {
                        if (response.IsSuccessStatusCode)
                        {
                            string responseData = await response.Content.ReadAsStringAsync();
                            dynamic data = JsonConvert.DeserializeObject(responseData);
                            string imageUrl = data.data.link;
                            return imageUrl;
                        }
                        else
                        {
                            throw new Exception("Failed to upload image: " + response.StatusCode);
                        }
                    }
                }
            }
        }

        [HttpGet("GetUsers")]
        public async Task<IEnumerable<AdvancedUser>> GetUsers()
        {
            var userData = _dbContext.Users.ToList();
            return userData;
        }

        [HttpGet("GetDeliveries")]
        public List<DeliveriesList> GetDeliveriesList()
        {
            var deliveryData = _dbContext.deliveriesList.ToList();
            return deliveryData;
        }

        [HttpGet("GetDeliveredDetails")]
        public List<RouteDeliveredDetails> GetDeliveredDetails()
        {
            var deliveryData = _dbContext.routeDeliveredDetails.ToList();
            return deliveryData;
        }

        [HttpGet("GetExactDeliveredDetails")]
        public RouteDeliveredDetails GetExactDeliveredDetails(int deliveryID, int customerID)
        {
            try
            {
                var data = _dbContext.routeDeliveredDetails.ToList().Where(x => x.DeliveryID == deliveryID && x.CustomerID == customerID).FirstOrDefault();
                return data;
            }
            catch
            {
                return null;
            }
        }

        [HttpGet("GetCustomers")]
        public IEnumerable<CustomersList> GetCustomersList()
        {
            var customerData = _dbContext.customersList.ToList();
            return customerData;
        }

        [HttpGet("GetDeliveryDetails")]
        public DeliveryDetails GetDeliveryDetails(int deliveryID)
        {
            try
            {
                var data = _dbContext.deliveryDetails.ToList().Where(x => x.DeliveryID == deliveryID).FirstOrDefault();
                return data;
            }
            catch
            {
                return null;
            }
        }

        [HttpGet("CheckIfUserIsValid")]
        public async Task<bool> GetValidateUser(string Email, string Password)
        {
            var user = await _userManager.FindByEmailAsync(Email);
            if (user == null)
            {
                // User does not exist
                return false;
            }

            // Verify the provided password against the stored hash
            var passwordValid = await _userManager.CheckPasswordAsync(user, Password);

            return passwordValid;
        }


        [HttpPost("createDelivery")]
        public async void createDelivery(DeliveriesList delivery)
        {
            try
            {
                _dbContext.deliveriesList.Add(delivery);
                _dbContext.SaveChanges();
                await _hub.Clients.All.SendAsync("ReceiveMessage");
                await _hub.Clients.All.SendAsync("UpdateApps", delivery.DeliveryId);
            }
            catch
            {

            }
        }

        [HttpPost("deleteDelivery")]
        public async void deleteDelivery(int id)
        {
            try
            {
                var toRemove = _dbContext.routeDeliveredDetails.ToList().Where(x => x.DeliveryID == id).ToList();
                if (toRemove.Count > 0)
                {
                    foreach (var remove in toRemove)
                        _dbContext.routeDeliveredDetails.Remove(remove);

                    _dbContext.SaveChanges();
                }
                if (_dbContext.deliveryDetails.ToList().Where(x => x.DeliveryID == id).FirstOrDefault() != null)
                {
                    _dbContext.deliveryDetails.Remove(_dbContext.deliveryDetails.ToList().Where(x => x.DeliveryID == id).FirstOrDefault());
                    _dbContext.SaveChanges();
                }

                _dbContext.deliveriesList.Remove(_dbContext.deliveriesList.ToList().Where(x => x.DeliveryId == id).FirstOrDefault());
                _dbContext.SaveChanges();

                await _hub.Clients.All.SendAsync("ReceiveMessage");
                await _hub.Clients.All.SendAsync("UpdateApps", id);
            }
            catch
            {

            }
        }

        [HttpPost("updateDelivery")]
        public async void updateDelivery(DeliveriesList delivery)
        {
            try
            {
                _dbContext.deliveriesList.Where(x => x.DeliveryId == delivery.DeliveryId).FirstOrDefault().DeliveryDriver = delivery.DeliveryDriver;
                _dbContext.deliveriesList.Where(x => x.DeliveryId == delivery.DeliveryId).FirstOrDefault().DeliveryTimes = delivery.DeliveryTimes;
                _dbContext.deliveriesList.Where(x => x.DeliveryId == delivery.DeliveryId).FirstOrDefault().DeliveryStatus = delivery.DeliveryStatus;
                _dbContext.deliveriesList.Where(x => x.DeliveryId == delivery.DeliveryId).FirstOrDefault().Coordinates = delivery.Coordinates;
                _dbContext.deliveriesList.Where(x => x.DeliveryId == delivery.DeliveryId).FirstOrDefault().AtLocation = delivery.AtLocation;
                _dbContext.deliveriesList.Where(x => x.DeliveryId == delivery.DeliveryId).FirstOrDefault().CoordinateSpeeds = delivery.CoordinateSpeeds;
                _dbContext.deliveriesList.Where(x => x.DeliveryId == delivery.DeliveryId).FirstOrDefault().CoordinateTimes = delivery.CoordinateTimes;
                _dbContext.deliveriesList.Where(x => x.DeliveryId == delivery.DeliveryId).FirstOrDefault().DriverUserName = delivery.DriverUserName;
                _dbContext.deliveriesList.Where(x => x.DeliveryId == delivery.DeliveryId).FirstOrDefault().RouteAddresses = delivery.RouteAddresses;
                _dbContext.deliveriesList.Where(x => x.DeliveryId == delivery.DeliveryId).FirstOrDefault().Invoices = delivery.Invoices;
                _dbContext.deliveriesList.Where(x => x.DeliveryId == delivery.DeliveryId).FirstOrDefault().DeliveryRoutes = delivery.DeliveryRoutes;

                _dbContext.SaveChanges();
                await _hub.Clients.All.SendAsync("ReceiveMessage");
                await _hub.Clients.All.SendAsync("UpdateApps", delivery.DeliveryId);
            }
            catch
            {

            }
        }

        [HttpPost("updateDeliveryStatus")]
        public async void updateDeliveryStatus(int id, string status)
        {
            try
            {
                _dbContext.deliveriesList.Where(x => x.DeliveryId == id).FirstOrDefault().DeliveryStatus = status;
                _dbContext.SaveChanges();

                if(status=="Ended")
                    await _hub.Clients.All.SendAsync("ReceiveMessage");
            }
            catch
            {

            }
        }

        [HttpPost("updateInitialDeliveryDetails")]
        public async void updateInitialDeliveryDetails(int deliveryID, string truckNumber, int mileage, string fuelTank)
        {
            try
            {
                DeliveryDetails deliveryDetails = new DeliveryDetails();

                if (_dbContext.deliveryDetails.ToList().Count == 0)
                    deliveryDetails.ID = 1;
                else
                    deliveryDetails.ID = _dbContext.deliveryDetails.ToList().MaxBy(x => x.ID).ID + 1;

                deliveryDetails.DeliveryID = deliveryID;
                deliveryDetails.TruckNumber = truckNumber;
                deliveryDetails.StartingMileage = mileage;
                deliveryDetails.FuelTank_Starting = fuelTank;

                _dbContext.deliveryDetails.Add(deliveryDetails);
                _dbContext.SaveChanges();
            }
            catch
            {

            }
        }

        [HttpPost("updateDeliveredDetails")]
        public async void updateDeliveredDetails(int deliveryID, int customerID, string invoice, string returnedItems, string amount)
        {
            try
            {

                _dbContext.routeDeliveredDetails.Where(x => x.DeliveryID == deliveryID && x.CustomerID == customerID).FirstOrDefault().DeliveryID = deliveryID;
                _dbContext.routeDeliveredDetails.Where(x => x.DeliveryID == deliveryID && x.CustomerID == customerID).FirstOrDefault().CustomerID = customerID;
                _dbContext.routeDeliveredDetails.Where(x => x.DeliveryID == deliveryID && x.CustomerID == customerID).FirstOrDefault().InvoiceNo = invoice;
                _dbContext.routeDeliveredDetails.Where(x => x.DeliveryID == deliveryID && x.CustomerID == customerID).FirstOrDefault().AmountReceived = amount;
                _dbContext.routeDeliveredDetails.Where(x => x.DeliveryID == deliveryID && x.CustomerID == customerID).FirstOrDefault().ReturnedItems = returnedItems;

                _dbContext.SaveChanges();
            }
            catch
            {

            }
        }

        [HttpPost("addDeliveredDetails")]
        public async void addDeliveredDetails(int deliveryID, int customerID, string invoice, string returnedItems, string amount)
        {
            try
            {
                RouteDeliveredDetails deliveryDetails = new RouteDeliveredDetails();

                if (_dbContext.routeDeliveredDetails.ToList().Count == 0)
                    deliveryDetails.ID = 1;
                else
                    deliveryDetails.ID = _dbContext.routeDeliveredDetails.ToList().MaxBy(x => x.ID).ID + 1;

                deliveryDetails.DeliveryID = deliveryID;
                deliveryDetails.CustomerID = customerID;
                deliveryDetails.InvoiceNo = invoice;
                deliveryDetails.AmountReceived = amount;
                deliveryDetails.ReturnedItems = returnedItems;

                _dbContext.routeDeliveredDetails.Add(deliveryDetails);
                _dbContext.SaveChanges();
            }
            catch
            {

            }
        }



        [HttpPost("updateFinalDeliveryDetails")]
        public async void updateFinalDeliveryDetails(int deliveryID, int mileage, string fuelTank)
        {
            try
            {
                _dbContext.deliveryDetails.Where(x => x.DeliveryID == deliveryID).FirstOrDefault().EndingMileage = mileage;
                _dbContext.deliveryDetails.Where(x => x.DeliveryID == deliveryID).FirstOrDefault().FuelTank_Ending = fuelTank;

                _dbContext.SaveChanges();
            }
            catch
            {

            }
        }


        [HttpPost("PostDeliveryTime")]
        public async void PostDeliveryTime(string UserName, string time)
        {
            try
            {
                var delivery = _dbContext.deliveriesList.ToList().Where(x => x.DriverUserName == UserName && x.DeliveryStatus == "Started").FirstOrDefault();
                var routes = delivery.DeliveryRoutes.Substring(0, delivery.DeliveryRoutes.LastIndexOf(":")).Split(":");

                var count = 0;
                var deliveries = delivery.DeliveryTimes;

                if (deliveries == null)
                    deliveries = "";

                foreach (var route in routes)
                {
                    if (deliveries.Contains(route + ":"))
                    {
                        count++;
                        deliveries = deliveries.Substring(deliveries.IndexOf(":") + 7);
                    }
                    else
                    {
                        if (count == routes.Length - 1)
                            delivery.DeliveryStatus = "End Journey";
                        delivery.DeliveryTimes += route + ":" + time + ":";
                        string[] notATLocation = { "None", "false" };
                        delivery.AtLocation = notATLocation;
                        _dbContext.deliveriesList.ToList()[_dbContext.deliveriesList.ToList().IndexOf(delivery)] = delivery;
                        _dbContext.SaveChanges();
                        break;
                    }
                }

                try
                {
                    await _hub.Clients.All.SendAsync("ReceiveMessage");
                }
                catch
                {
                }
            }
            catch (Exception ex)
            {

            }
        }


        [HttpPost("AtLocation")]
        public async Task<bool> AtLocation(int id, string customer, string address)
        {
            bool successful = false;

            try
            {
                _dbContext.deliveriesList.Where(x => x.DeliveryId == id).FirstOrDefault().AtLocation[0] = customer + ":" + address;
                _dbContext.deliveriesList.Where(x => x.DeliveryId == id).FirstOrDefault().AtLocation[1] = "true";
                _dbContext.SaveChanges();
                successful = true;
            }
            catch
            {

            }

            return successful;
        }

        [HttpPost("SaveCoordinates")]
        public async Task<bool> SaveCoordinates(int id, double latitude, double longitude, string time, string speed)
        {
            bool successful = false;

            try
            {
                NpgsqlPoint[] coordinates = _dbContext.deliveriesList.Where(x => x.DeliveryId == id).FirstOrDefault().Coordinates;
                string[] times = _dbContext.deliveriesList.Where(x => x.DeliveryId == id).FirstOrDefault().CoordinateTimes;
                string[] speeds = _dbContext.deliveriesList.Where(x => x.DeliveryId == id).FirstOrDefault().CoordinateSpeeds;

                if (coordinates == null)
                    Array.Resize<NpgsqlPoint>(ref coordinates, 1);
                else
                    Array.Resize<NpgsqlPoint>(ref coordinates, coordinates.Length + 1);

                if (times == null)
                    Array.Resize<string>(ref times, 1);
                else
                    Array.Resize<string>(ref times, times.Length + 1);

                if (speeds == null)
                    Array.Resize<string>(ref speeds, 1);
                else
                    Array.Resize<string>(ref speeds, speeds.Length + 1);

                coordinates[coordinates.Length - 1] = new NpgsqlPoint(latitude, longitude);
                times[times.Length - 1] = time;
                speeds[speeds.Length - 1] = speed;

                _dbContext.deliveriesList.Where(x => x.DeliveryId == id).FirstOrDefault().Coordinates = coordinates;
                _dbContext.deliveriesList.Where(x => x.DeliveryId == id).FirstOrDefault().CoordinateTimes = times;
                _dbContext.deliveriesList.Where(x => x.DeliveryId == id).FirstOrDefault().CoordinateSpeeds = speeds;
                _dbContext.SaveChanges();
                successful = true;
            }
            catch
            {

            }

            return successful;
        }



        private async Task<CustomersList> getInfo(string base64Image)
        {
            string directory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) + "\\TruckTrack";
            string filePath = directory + "\\truckTrackImg.jpeg";

            var options = new RestClientOptions("https://xm.tryier.com:6001")
            {
                MaxTimeout = -1,
            };
            var client = new RestClient(options);
            var request = new RestRequest("/Ocr", Method.Post);
            request.AddHeader("Authorization", "Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJuYW1lIjoiYXBpdXNlciIsImp0aSI6IjI4ZGZhN2QyLTc1NDctNGMxYS05NTVkLTY2NWQzZmU3NWJiZiIsImh0dHA6Ly9zY2hlbWFzLm1pY3Jvc29mdC5jb20vd3MvMjAwOC8wNi9pZGVudGl0eS9jbGFpbXMvcm9sZSI6IkFwcGxpY2F0aW9uIiwiZXhwIjoxNzE3MzY2MTY3LCJpc3MiOiJodHRwczovL3htLnRyeWllci5jb206NjAwMSJ9.yhLWch3TicNWTUDBcg6IVVEuK1WdtRrSTOvLIj1HO1A");
            request.AlwaysMultipartFormData = true;
            request.AddFile("file", Convert.FromBase64String(base64Image), "file_OCR.png");
            RestResponse response = await client.ExecuteAsync(request);
            var result = (response.Content);
            List<string> words = JsonConvert.DeserializeObject<List<string>?>(response.Content);
            string list = String.Join(" ", words);
            CustomersList cust = new CustomersList();

            foreach (var customer in _dbContext.customersList.ToList())
            {
                if (!String.IsNullOrWhiteSpace(customer.BusinessName) && !String.IsNullOrWhiteSpace(customer.Address1))
                {
                    bool match = true;
                    List<string> nameList = customer.BusinessName.Split(" ").ToList();
                    List<string> addressList = customer.Address1.Split(" ").ToList();
                    foreach (var n in nameList)
                    {
                        if (!list.Contains(n.Trim()))
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        foreach (var a in addressList)
                        {
                            if (!list.Contains(a.Trim()))
                            {
                                match = false;
                                break;
                            }
                        }
                    }

                    if (match)
                    {
                        cust = customer;
                        break;
                    }
                }
            }

            return cust;
        }


        /*[HttpPost("GetBusinessOcr")]
        public async Task<CustomersList> GetBusinessOcr(IFormFile file)
        {
            byte[] data = null;
            using(var memory = new MemoryStream())
            {
                await file.CopyToAsync(memory);
                data = memory.ToArray();
            }
            return await getInfo(Convert.ToBase64String(data));
        }*/
    }
}




