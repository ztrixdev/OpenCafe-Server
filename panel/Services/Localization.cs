using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;

namespace panel.Services 
{
    public class Localization
    {
        private readonly HttpClient _http;
        private readonly IJSRuntime _jsRuntime;
        private Dictionary<string, string>? _translations;
        public bool IsLoaded { get; private set; } = false;
        public string CurrentCulture { get; private set; } = "en";

        public Localization(HttpClient http, IJSRuntime jsRuntime)
        {
            _http = http;
            _jsRuntime = jsRuntime;
        }

        public async Task InitializeAsync()
        {
            var culture = await _jsRuntime.InvokeAsync<string>("blazorCulture.get") ?? "en";
            await LoadTranslationsAsync(culture);
        }

        public async Task LoadTranslationsAsync(string culture)
        {
            try
            {
                var json = await _http.GetStringAsync($"Strings/{culture}.json");
                _translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                CurrentCulture = culture;
                IsLoaded = true;
                await _jsRuntime.InvokeVoidAsync("blazorCulture.set", culture);
            }
            catch
            {
                Console.WriteLine("Error loading translations.");
            }
        }

        public string Translate(string key)
        {
            if (_translations?.TryGetValue(key, out var value) ?? false)
            {
                return value;
            }
            return key;
        }
    }
}
