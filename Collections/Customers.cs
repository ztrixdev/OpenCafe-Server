using MongoDB.Bson;
using MongoDB.Driver;
using OpenCafe.Server.DBmgmt;
using OpenCafe.Server.Helpers;
using OpenCafe.Server.Logging;
using BCrypt.Net;

namespace OpenCafe.Server.Collections;

public class Customer
(
    long internalID,
    string username,
    string email,
    bool isEmailVerified,
    string password,
    long[]? hearts,
    long[]? reviews,
    long? card
)
{
    public ObjectId Id { get; set; }
    public long InternalID { get; set; } = internalID;
    public string Username { get; set; } = username;
    public string Email { get; set; } = email;
    public bool IsEmailVerified { get; set; } = isEmailVerified;
    public string Password { get; set; } = password;
    public long[]? Hearts { get; set; } = hearts;
    public long[]? Reviews { get; set; } = reviews;
    public long? Card { get; set; } = card;
}

public class Customers
{
    /// <summary>
    /// Self explanatory - a user registering request body.
    /// </summary>
    /// <param name="Username">Desired username</param>
    /// <param name="Email">Email to use for registration</param>
    /// <param name="Password">Password to lock the account up with xD</param>
    public record RegisterRequest(string Username, string Email, string Password);

    /// <summary>
    /// A request body used universally whenever you just need the email and the password for the account.
    /// </summary>
    /// <param name="Email"></param>
    /// <param name="Password"></param>
    public record EmailPasswordRequest(string Email, string Password);

    // Might use it later.
    readonly Logger logger = new Logger();

    /// <summary>
    /// A function that searches for a customer object in the database by their internal ID or email.
    /// </summary>
    /// <param name="Parameter">
    /// Contains two values: a parameter as a key and it's value as the value. 
    /// Only allowed parameters are "iid" and "email"
    /// Example: new KeyValuePair<string, string>("email", "persone@beispeil.at")
    /// </param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - null if no customer with the provided parameter was found in the database
    /// - an object of the Customer class if the search completed successfully
    /// </returns>
    /// <exception cref="ArgumentException">If the Parameter's values are invalid/empty/not matching the "iid or email" template</exception>
    /// <exception cref="FormatException">If the IID provided cannot be converted to a number</exception>
    public static async Task<Customer> GetCustomerBy(KeyValuePair<string, string> Parameter, Database database)
    {
        var customerCollection = database._database.GetCollection<Customer>("customers");

        if (string.IsNullOrWhiteSpace(Parameter.Key) || string.IsNullOrWhiteSpace(Parameter.Value))
            throw new ArgumentException("Cannot use null or empty data as search parameters.");

        if (!Parameter.Key.Equals("iid", StringComparison.CurrentCultureIgnoreCase) && !Parameter.Key.Equals("email", StringComparison.CurrentCultureIgnoreCase))
            throw new ArgumentException("Cannot find customers using data other than InternalID or email.");

        Customer? customer;
        switch (Parameter.Key.ToLower())
        {
            case "iid":
                long iid = 0;
                var iidParse = Int64.TryParse(Parameter.Value, out iid);
                if (!iidParse)
                    throw new FormatException("The provided IID isn't a valid number");

                customer = await customerCollection.Find((Customer customer) => customer.InternalID == iid).FirstOrDefaultAsync();
                break;
            case "email":
                var encryptedEmail = await CryptoHelper.EncryptAsync(Parameter.Value, database.collectionEncryption["customers"]["key"], database.collectionEncryption["customers"]["iv"]);
                customer = await customerCollection.Find((Customer customer) => customer.Email == encryptedEmail).FirstOrDefaultAsync(); ;
                break;
            default:
                return null;
        }

        return customer;
    }

    /// <summary>
    /// Register function for customers. Validates all the data and rregisters a new customer account inside of the database.
    /// </summary>
    /// <param name="request">Refer to RegisterRequest info</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - Bad Request if one or more of the request fields aren't provided or if the password is shorter than 8 characters
    /// - Conflict if a customer with the provided email already exists in the database
    /// - OK with the newmade customer object
    /// </returns>
    public static async Task<IResult> Register(RegisterRequest request, Database database)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest("One or more of the request fields is not provided.");

        if (request.Password.Length < 8)
            return Results.BadRequest("The provided password doesn't match the length requirement.");

        if (await GetCustomerBy(new KeyValuePair<string, string>("email", request.Email), database) != null)
            return Results.Conflict("A customer with the provided email already exists in the database.");

        var key = database.collectionEncryption["customers"]["key"];
        var iv = database.collectionEncryption["customers"]["iv"];

        var encryptedEmail = await CryptoHelper.EncryptAsync(request.Email, key, iv);
        var encryptedPassword = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var random = new Random();
        long newIID = random.NextInt64();
        // If the IID is taken, generates a new one untill it's free.
        while (await GetCustomerBy(new KeyValuePair<string, string>("iid", $"{newIID}"), database) != null)
        {
            newIID = random.NextInt64();
            continue;
        }

        var customer = new Customer(internalID: newIID, username: request.Username, email: encryptedEmail, isEmailVerified: false, password: encryptedPassword, null, null, null);
        var customerCollection = database._database.GetCollection<Customer>("customers");
        await customerCollection.InsertOneAsync(customer);

        return Results.Ok(await GetCustomerBy(new KeyValuePair<string, string>("iid", $"{newIID}"), database));
    }

    /// <summary>
    /// Login function for customers. Check if the provided email and password, if encrypted, match with any present in the database.
    /// </summary>
    /// <param name="request">Refer to EmailPasswordRequest info</param>
    /// <param name="database">Initialized Database object</param>
    /// <returns>
    /// - Bad Request if one or more of the request fields aren't provided 
    /// - Not Found of no users with the provided email were found
    /// - Unauthorized if the provided password is incorrect 
    /// - OK with the customer object
    /// </returns>
    public static async Task<IResult> Login(EmailPasswordRequest request, Database database)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest("One or more of the request fields is not provided.");

        var customer = await GetCustomerBy(new KeyValuePair<string, string>("email", request.Email), database);
        if (customer == null)
            return Results.NotFound("Cannot find a user with this email in the databse.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, customer.Password))
                return Results.Unauthorized();

        return Results.Ok(customer);
    }
}

