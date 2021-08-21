using ElasticNEST.Models;
using Elasticsearch.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nest;
using System;
using System.Collections.Generic;
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

        private static readonly string[] Code = new[]
        {
            "ctc-0001","ctz-0002","etz-0003","vta-0004","oar-0005","bbb-0006","xzc-0007"
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
            var sfConfig = new Dictionary<string, int>() {
                { "title", 10 } ,
                { "code", 20 },
                { "target", 2 }
            };
            var splitTexts = request.SearchText.Split(' ');

            var query = new QueryContainer();

            var titleField = Infer.Field<MyIndexDocument>(p => p.Title, 4);
            var bodyField = Infer.Field<MyIndexDocument>(p => p.Code, 10);
            var targetField = Infer.Field<MyIndexDocument>(p => p.Target, 2);

            query |= new MultiMatchQuery
            {
                Fields = titleField.And(bodyField).And(targetField),
                Query = request.SearchText
            };

            query |= new QueryStringQuery
            {
                Query = $"\"{request.SearchText}\"",
                Boost = 50
            };

            var functions = new List<IScoreFunction>();

            foreach (var item in sfConfig)
            {
                foreach (var text in splitTexts)
                {
                    functions.Add(new WeightFunction
                    {
                        Filter = new MatchQuery
                        {
                            Field = item.Key,
                            Query = text
                        },
                        Weight = item.Value
                    });
                }
            }

            var fsQuery = new FunctionScoreQuery()
            {
                Name = "named_query",
                Boost = 1.1,
                Query = query,
                BoostMode = FunctionBoostMode.Sum,
                ScoreMode = FunctionScoreMode.Sum,
                MinScore = 1.0,
                Functions = functions
            };

            var searchDescriptor = new SearchDescriptor<MyIndexDocument>()
                .Index("my-index")
                .Query(q => q.FunctionScore(fs => fsQuery))
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
                        Title = $"{Summaries[0]} {Summaries[0]} {Summaries[2]}",
                        Code = $"{Code[0]}",
                        Target = new List<string>
                        {
                            $"{Summaries[0]} {Summaries[1]}",
                            $"{Summaries[1]} {Summaries[2]}",
                        },
                    },
                    new MyIndexDocument
                    {
                        Title = $"{Summaries[0]} {Summaries[1]} {Summaries[4]}",
                        Code = $"{Code[1]}",
                        Target = new List<string>
                        {
                            $"{Summaries[0]} {Summaries[5]}",
                            $"{Summaries[3]} {Summaries[6]}",
                        },
                    },
                    new MyIndexDocument
                    {
                        Title = $"{Summaries[2]} {Summaries[1]} {Summaries[0]}",
                        Code = $"{Code[2]}",
                        Target = new List<string>
                        {
                            $"{Summaries[7]} {Summaries[1]}",
                            $"{Summaries[1]} {Summaries[9]}",
                        },
                    },
                    new MyIndexDocument
                    {
                        Title = $"{Summaries[9]} {Summaries[1]} {Summaries[2]} {Summaries[9]}",
                        Code = $"{Code[3]}",
                        Target = new List<string>
                        {
                            $"{Summaries[4]} {Summaries[6]}",
                            $"{Summaries[5]} {Summaries[7]}",
                        },
                    },
                    new MyIndexDocument
                    {
                        Title = $"{Summaries[7]} {Summaries[1]} {Summaries[2]}",
                        Code = $"{Code[4]}",
                        Target = new List<string>
                        {
                            $"{Summaries[0]} {Summaries[1]}",
                            $"{Summaries[1]} {Summaries[2]}",
                        },
                    },
                    new MyIndexDocument
                    {
                        Title = $"{Summaries[4]} {Summaries[1]} {Summaries[3]}",
                        Code = $"{Code[5]}",
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
