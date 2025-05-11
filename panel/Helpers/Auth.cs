
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using System.Text.RegularExpressions;
using panel.Services;

namespace panel.Helpers 
{
    class Id 
    {
        long timestamp {get; set;} 
        string creationTime {get; set;}
    }

    class Admin 
    {
        Id _id {get; set;} 
        public string name {get; set;}
        public string role {get; set;}
        string token {get; set;}
    }

    public class Auth 
    {
        /// <summary>
        /// Attempts to login using the passed credentials. 
        /// Sends a GET request to oc/api/admin/login with a token and processes the response.
        /// </summary>
        /// <param name="credentials">Cookie-stored login credentials</param>
        /// <exception cref="ArgumentException">The provided instance URL is invalid.</exception>
        /// <exception cref="WebException">The provided instance URL doesn't belong to an OpenCafe instance.</exception> 
        /// <returns>a KVP containing login status in a bool and a KVP with admin's name being the key and their role being the value.</returns>
        public async Task<KeyValuePair<bool, KeyValuePair<string, string>>> Login(Credentials credentials)
        {
            var client = new HttpClient();

            if (credentials._instance == null || credentials._token == null)
                throw new ArgumentException("Can't login with null credentials.");

            Uri uri; bool createUri;
            createUri = Uri.TryCreate(credentials._instance, UriKind.Absolute, out uri);
            if (createUri == false)
                throw new ArgumentException("The provided instance URL is invalid.");
        
            var OCCheck = await client.GetAsync(uri);
            if (await OCCheck.Content.ReadAsStringAsync() != "Wilkommen auf OpenCafe!")
                throw new WebException("The provided instance URL doesn't belong to an OpenCafe instance.");

            var loginReq = await client.GetAsync($"{uri}api/admin/login?token={credentials._token}");
            var response = await loginReq.Content.ReadAsStringAsync();
            var deserializedResponse = JsonSerializer.Deserialize<Admin>(response);
            if (deserializedResponse == null) {
                throw new UnauthorizedAccessException($"Deserialization failed for response: {response}");
            }

            if (loginReq.IsSuccessStatusCode) 
                return new KeyValuePair<bool, KeyValuePair<string, string>>
                    (true, new KeyValuePair<string, string>(deserializedResponse.name, deserializedResponse.role));

            return new KeyValuePair<bool, KeyValuePair<string, string>>
                (false, new KeyValuePair<string, string>(null, null));
        }     
    }
}
