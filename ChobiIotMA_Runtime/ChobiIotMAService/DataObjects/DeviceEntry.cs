using Microsoft.Azure.Mobile.Server;

namespace ChobiIotMAService.DataObjects
{
    public class DeviceEntry : EntityData
    {
        public string DeviceId { get; set; }
        public bool ServiceAvailable { get; set; }
        public string IoTHubEndpoint { get; set; }
        public string DeviceKey { get; set; }
    }
}