using System;
using System.Collections.Generic;

namespace ElasticNEST.Models
{
    public class MyIndexDocument
    {
        public Guid Id { get; set; } =Guid.NewGuid();

        public string Title { get; set; }

        public string Code { get; set; }
     
        public List<string> Target { get; set; }
    }
}
