using System.Linq;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.OData;
using Microsoft.Azure.Mobile.Server;
using ChobiIotMAService.DataObjects;
using ChobiIotMAService.Models;

namespace ChobiIotMAService.Controllers
{
    public class DeviceEntryController : TableController<DeviceEntry>
    {
        protected override void Initialize(HttpControllerContext controllerContext)
        {
            base.Initialize(controllerContext);
            ChobiIotMAContext context = new ChobiIotMAContext();
            DomainManager = new EntityDomainManager<DeviceEntry>(context, Request);
        }

        // GET tables/DeviceEntry
        public IQueryable<DeviceEntry> GetAllDeviceEntry()
        {
            return Query(); 
        }

        // GET tables/DeviceEntry/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public SingleResult<DeviceEntry> GetDeviceEntry(string id)
        {
            return Lookup(id);
        }

        // PATCH tables/DeviceEntry/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public Task<DeviceEntry> PatchDeviceEntry(string id, Delta<DeviceEntry> patch)
        {
             return UpdateAsync(id, patch);
        }

        // POST tables/DeviceEntry
        public async Task<IHttpActionResult> PostDeviceEntry(DeviceEntry item)
        {
            DeviceEntry current = await InsertAsync(item);
            return CreatedAtRoute("Tables", new { id = current.Id }, current);
        }

        // DELETE tables/DeviceEntry/48D68C86-6EA6-4C25-AA33-223FC9A27959
        public Task DeleteDeviceEntry(string id)
        {
             return DeleteAsync(id);
        }
    }
}
