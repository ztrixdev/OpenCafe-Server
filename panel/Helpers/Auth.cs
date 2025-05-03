
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

            var urlRegex = new Regex(
            @"^(https?|ftps?):\/\/(?:[a-zA-Z0-9]" +
                    @"(?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}" +
                    @"(?::(?:0|[1-9]\d{0,3}|[1-5]\d{4}|6[0-4]\d{3}" +
                    @"|65[0-4]\d{2}|655[0-2]\d|6553[0-5]))?" +
                    @"(?:\/(?:[-a-zA-Z0-9@%_\+.~#?&=]+\/?)*)?$",
            RegexOptions.IgnoreCase);
            urlRegex.Matches(creds._instance);

            if (!urlRegex.IsMatch(creds._instance))
                throw new ArgumentException("The provided instance URL is invalid.");
        
            var OCCheck = await client.GetAsync(creds._instance);
            if (await OCCheck.Content.ReadAsStringAsync() != "Wilkommen auf OpenCafe!")
                throw new WebException("The provided instance URL doesn't belong to an OpenCafe instance.");

            var loginReq = await client.GetAsync($"{creds._instance}/api/admin/login?token={creds._token}");
            if (loginReq.IsSuccessStatusCode) return true;

            return false;

            // TODO: Diese Funktion debuggen.
        }     
    }
}
