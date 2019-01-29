using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace KLabFunctions.Models
{
    public class Session
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("n");
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        public string Description { get; set; }
        public bool IsAccepted { get; set; }
    }

    public class CreateSession
    {
        public string Description { get; set; }
    }

    public class UpdateSession
    {
        public string Description { get; set; }
        public bool IsAccepted { get; set; }
    }

    public class SessionEntity : TableEntity
    {
        public DateTime CreatedTime { get; set; }
        public string Description { get; set; }
        public bool IsAccepted { get; set; }
    }

    public static class Mappings
    {
        public static SessionEntity ToSessionEntity(this Session session)
        {
            return new SessionEntity
            {
                PartitionKey = "SESSION",
                RowKey = session.Id,
                CreatedTime = session.CreatedTime,
                IsAccepted = session.IsAccepted,
                Description = session.Description
            };
        }

        public static Session ToSession(this SessionEntity se)
        {
            return new Session
            {
                Id = se.RowKey,
                CreatedTime = se.CreatedTime,
                IsAccepted = se.IsAccepted,
                Description = se.Description
            };
        }

    }
}
