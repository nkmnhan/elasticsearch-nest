using ElasticNEST.Models;
using Elasticsearch.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nest;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ElasticNEST.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class NESTController : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "freezing", "bracing", "chilly", "cool", "mild", "warm", "balmy", "hot", "sweltering", "scorching"
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
        public IActionResult Search([FromBody] Requests.SearchRequest request)
        {
            var query = BoostConstant(new MatchQuery
            {
                Field = "title",
                Query = request.SearchText
            }, 2);

            query |= BoostConstant(new MatchQuery
            {
                Field = "code",
                Query = request.SearchText
            }, 1);

            var searchDescriptor = new SearchDescriptor<MyIndexDocument>()
                .Index("my-index")
                .Query(x => query)
                .Explain();

            var json = _elasticClient.RequestResponseSerializer.SerializeToString(searchDescriptor);
            var result = _elasticClient.Search<MyIndexDocument>(searchDescriptor);
            return Ok(result.Documents);
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            bool exists = _elasticClient.Indices.Exists("my-index").Exists;
            if (!exists)
            {
                _elasticClient.Indices.Create("my-index", x => x.Map<MyIndexDocument>(m => m.AutoMap()));

                var items = new List<MyIndexDocument> {
                    new MyIndexDocument
                    {
                        Title = $"{Summaries[0]} {Summaries[1]} {Summaries[2]}",
                        Code = $"{Summaries[3]} {Summaries[4]} {Summaries[5]}",
                        Target = new List<string>
                        {
                            $"{Summaries[0]} {Summaries[1]}",
                            $"{Summaries[1]} {Summaries[2]}",
                        },
                    },
                    new MyIndexDocument
                    {
                        Title = $"{Summaries[0]} {Summaries[1]} {Summaries[4]}",
                        Code = $"{Summaries[0]} {Summaries[4]} {Summaries[2]}",
                        Target = new List<string>
                        {
                            $"{Summaries[0]} {Summaries[5]}",
                            $"{Summaries[3]} {Summaries[6]}",
                        },
                    },
                    new MyIndexDocument
                    {
                        Title = $"{Summaries[2]} {Summaries[1]} {Summaries[0]}",
                        Code = $"{Summaries[7]} {Summaries[8]} {Summaries[9]}",
                        Target = new List<string>
                        {
                            $"{Summaries[7]} {Summaries[1]}",
                            $"{Summaries[1]} {Summaries[9]}",
                        },
                    },
                    new MyIndexDocument
                    {
                        Title = $"{Summaries[0]} {Summaries[1]} {Summaries[2]} {Summaries[9]}",
                        Code = $"{Summaries[8]} {Summaries[9]} {Summaries[7]}",
                        Target = new List<string>
                        {
                            $"{Summaries[4]} {Summaries[6]}",
                            $"{Summaries[5]} {Summaries[7]}",
                        },
                    },
                    new MyIndexDocument
                    {
                        Title = $"{Summaries[0]} {Summaries[1]} {Summaries[2]}",
                        Code = $"{Summaries[0]} {Summaries[1]} {Summaries[2]}",
                        Target = new List<string>
                        {
                            $"{Summaries[0]} {Summaries[1]}",
                            $"{Summaries[1]} {Summaries[2]}",
                        },
                    },
                    new MyIndexDocument
                    {
                        Title = $"{Summaries[0]} {Summaries[1]} {Summaries[3]}",
                        Code = $"{Summaries[0]} {Summaries[2]} {Summaries[4]}",
                        Target = new List<string>
                        {
                            $"{Summaries[6]} {Summaries[8]}",
                            $"{Summaries[7]} {Summaries[9]}",
                        },
                    },
                };

                await _elasticClient.BulkAsync(b => b
                                         .Index("my-index")
                                         .IndexMany(items)
                                     );
            }

            var result = _elasticClient.Search<MyIndexDocument>(x => x.Index("my-index").Query(q => q.MatchAll()));
            return Ok(result.Documents);
        }

        public QueryContainer BoostConstant(QueryContainer query, int score)
        {
            return new ConstantScoreQuery
            {
                Boost = score,
                Filter = query
            };
        }
    }
}
