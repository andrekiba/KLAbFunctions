using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace KLabFunctions.Models
{
    public class Session
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("n");
        public DateTime CreatedTime { get; set; } = DateTime.UtcNow;
        public string Description { get; set; }
        public bool Accepted { get; set; }
        public bool Rejected { get; set; }
    }

    public class CreateSession
    {
        public string Description { get; set; }
    }

    public class UpdateSession
    {
        public string Description { get; set; }
        public bool Accepted { get; set; }
        public bool Rejected { get; set; }
    }

    public class SessionEntity : TableEntity
    {
        public DateTime CreatedTime { get; set; }
        public string Description { get; set; }
        public bool Accepted { get; set; }
        public bool Rejected { get; set; }
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
                Description = session.Description,
                Accepted = session.Accepted,
                Rejected = session.Rejected
            };
        }

        public static Session ToSession(this SessionEntity se)
        {
            return new Session
            {
                Id = se.RowKey,
                CreatedTime = se.CreatedTime,
                Description = se.Description,
                Accepted = se.Accepted,
                Rejected = se.Rejected
            };
        }

    }
}
