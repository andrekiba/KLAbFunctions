using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace KLabFunctions.Models
{
    public class Show : TableEntity
    {
        public string Title { get; set; }
        public string Rating { get; set; }
        public string RatingLevel { get; set; }
        public string RatingDescription { get; set; }
        public string ReleaseYear { get; set; }
    }
}
