﻿using System;
using Microsoft.WindowsAzure.Storage.Table;

namespace KLabFunctions.Models
{
    public class NetflixShow : TableEntity
    {
        public string Title { get; set; }
        public string Rating { get; set; }
        public string RatingLevel { get; set; }
        public string RatingDescription { get; set; }
        public DateTime ReleaseYear { get; set; }
    }
}
