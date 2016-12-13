using OrientDB.Core;

namespace OrientDB.Serializers.RecordCSVSerializer.Models
{
    internal class OLinkCollection
    {
        internal int PageSize { get; set; }
        internal ORID Root { get; set; }
        internal int KeySize { get; set; }
    }
}
