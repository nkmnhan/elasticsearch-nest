using ElasticNEST.Models;
using Elasticsearch.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ElasticNEST.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class NESTController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        private readonly ILogger<NESTController> _logger;
        private readonly ElasticClient _elasticClient;

        public NESTController(ILogger<NESTController> logger)
        {
            _logger = logger;
            var settings = new ConnectionSettings(new Uri("http://localhost:9200/"));
            _elasticClient = new ElasticClient(settings);
        }

        [HttpPost]
        public IActionResult Search()
        {
            var filter1 = new QueryStringQuery
            {
                Fields = "target",
                Query = "target.keyword: \"a0\"^50 OR target.keyword: \"a1\"^50"
            };

            var filter2 = new QueryStringQuery
            {
                Fields = "title",
                Query = "title: \"Title 0\"^6 OR \"Title 0\"^4"
            };

            var searchDescriptor = new SearchDescriptor<MyIndexDocument>().Index("my-index").Query(x => filter1 || filter2);

            var json = _elasticClient.RequestResponseSerializer.SerializeToString(searchDescriptor);
            var result = _elasticClient.Search<MyIndexDocument>(searchDescriptor);
            return Ok(result.Documents);
        }

        [HttpGet]
        public IActionResult Get()
        {
            bool exists = _elasticClient.Indices.Exists("my-index").Exists;
            if (!exists)
            {
                _elasticClient.Indices.Create("my-index", x => x.Map<MyIndexDocument>(m => m.AutoMap()));
                var bulkIndexer = new BulkDescriptor();

                for (int i = 0; i < 10; i++)
                {
                    bulkIndexer.Index<MyIndexDocument>(document => document
                        .Document(new MyIndexDocument
                        {
                            Title = $"Title {i}",
                            Target = new List<string> { $"a{i}", $"b{i}", $"c{i}" }
                        })
                        .Id(Guid.NewGuid().ToString())
                        .Index("my-index"));
                }

                _elasticClient.Bulk(bulkIndexer);
            }

            var result = _elasticClient.Search<MyIndexDocument>(x=>x.Index("my-index").Query(q=>q.MatchAll()));
            return Ok(result.Documents);
        }
    }
}
