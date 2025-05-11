using Microsoft.JSInterop;

namespace panel.Services 
{
    /// <summary>
    /// Cookie-stored admin details management class
    /// </summary>
    public class AdminDetails
    {
        private readonly HttpClient _http;
        private readonly IJSRuntime _jsRuntime;
        public string _name;
        public string _role;

        public AdminDetails(HttpClient http, IJSRuntime jsRuntime)
        {
            _http = http;
            _jsRuntime = jsRuntime;
        }

        public async Task GetDetails()
        {
            _name = await _jsRuntime.InvokeAsync<string>("adminDetails.getName");
            _role = await _jsRuntime.InvokeAsync<string>("adminDetails.getRole");
        }
        
        public async Task SetDetails(string Name, string Role) 
        {
            await _jsRuntime.InvokeAsync<string>("adminDetails.setName", Name);
            await _jsRuntime.InvokeAsync<string>("adminDetails.setRole", Role);
        }

        public async Task ForgetDetails()
        {
            await _jsRuntime.InvokeAsync<string>("adminDetails.removeName");
            await _jsRuntime.InvokeAsync<string>("adminDetails.removeRole");

            _name = null; _role = null;
        }
    }
}
