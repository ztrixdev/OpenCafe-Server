
using System.Net;
using System.Text.RegularExpressions;
using panel.Services;

namespace panel.Helpers 
{
    public class Auth 
    {
        /// <summary>
        /// Attempts to login using the passed credentials. 
        /// Sends a GET request to oc/api/admin/login with a token and processes the response.
        /// </summary>
        /// <param name="creds">Cookie-stored login credentials</param>
        /// <exception cref="ArgumentException">The provided instance URL is invalid.</exception>
        /// <exception cref="WebException">The provided instance URL doesn't belong to an OpenCafe instance.</exception> 
        /// <returns>true if the login succeeded and false if not.</returns>
        public async Task<bool> Login(Credentials creds)
        {
            var client = new HttpClient();

            if (creds._instance == null || creds._token == null)
                throw new ArgumentException("Can't login with null credentials.");

            Uri uri; bool createUri;
            createUri = Uri.TryCreate(creds._instance, UriKind.Absolute, out uri);
            if (!createUri)
                throw new ArgumentException("The provided instance URL is invalid.");
        
            var OCCheck = await client.GetAsync(uri);
            if (await OCCheck.Content.ReadAsStringAsync() != "Wilkommen auf OpenCafe!")
                throw new WebException("The provided instance URL doesn't belong to an OpenCafe instance.");

            var loginReq = await client.GetAsync($"{uri}api/admin/login?token={creds._token}");
            if (loginReq.IsSuccessStatusCode) return true;

            return false;    
        }     
    }
}
