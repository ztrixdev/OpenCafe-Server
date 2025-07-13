using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OpenCafe.Server.Helpers;

public static class BsonOperations
{
    public static readonly string Set = "$set";
    public static readonly string Push = "$push";
    public static readonly string Pull = "$pull";
    public static readonly string NotEqual = "$ne";
}
