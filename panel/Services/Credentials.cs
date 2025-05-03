using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace panel.Services 
{
    /// <summary>
    /// Cookie-stored login credentials handler ckass.
    /// </summary>
    public class Credentials
    {
        private readonly HttpClient _http;
        private readonly IJSRuntime _jsRuntime;
        public string _token;
        public string _instance;

        public Credentials(HttpClient http, IJSRuntime jsRuntime)
        {
            _http = http;
            _jsRuntime = jsRuntime;
        }

        public async Task GetCredentials()
        {
            _token = await _jsRuntime.InvokeAsync<string>("userCredentials.getToken");
            _instance = await _jsRuntime.InvokeAsync<string>("userCredentials.getInstance");
        }
        
        public async Task SetCredentials(string token, string instance) 
        {
            await _jsRuntime.InvokeAsync<string>("userCredentials.setToken", token);
            await _jsRuntime.InvokeAsync<string>("userCredentials.setInstance", instance);
        }

        public async Task ForgetCredentials()
        {
            await _jsRuntime.InvokeAsync<string>("userCredentials.removeToken");
            await _jsRuntime.InvokeAsync<string>("userCredentials.removeInstance");

            _token = null; _instance = null;
        }
    }
}
