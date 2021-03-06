// -----------------------------------------------------------------------
//  <copyright file="RavenDB_1828.cs" company="Hibernating Rhinos LTD">
//      Copyright (c) Hibernating Rhinos LTD. All rights reserved.
//  </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;

using Raven.Client.Indexes;
using Raven.Tests.Common;

using Xunit;

namespace Raven.Tests.Issues
{
    public class RavenDB_1828 : RavenTest
    {
        public class Product
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public string CategoryId { get; set; }
        }

        public class Category
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        public class ProductWithTransformerParameter : AbstractTransformerCreationTask<Product>
        {
            public class Result
            {
                public string ProductId { get; set; }
                public string ProductName { get; set; }
                public string Input { get; set; }
            }
            public ProductWithTransformerParameter()
            {
                TransformResults = docs => from product in docs
                                           select new
                                           {
                                               ProductId = product.Id,
                                               ProductName = product.Name,
                                               Input = Parameter("input")
                                           };
            }
        }

        public class ProductWithTransformerParameterAndInclude : AbstractTransformerCreationTask<Product>
        {
            public class Result
            {
                public string ProductId { get; set; }
                public string ProductName { get; set; }
                public string CategoryId { get; set; }
            }
            public ProductWithTransformerParameterAndInclude()
            {
                TransformResults = docs => from product in docs
                                           let _ = Include(product.CategoryId)
                                           select new
                                           {
                                               ProductId = product.Id,
                                               ProductName = product.Name,
                                               product.CategoryId,
                                           };
            }
        }

        [Fact]
        public void CanUseResultsTransformerByName()
        {
            using (var store = NewDocumentStore())
            {
                new ProductWithTransformerParameter().Execute(store);
                using (var session = store.OpenSession())
                {
                    session.Store(new Product() { Name = "Irrelevant" });
                    session.SaveChanges();
                }

                var t = new ProductWithTransformerParameter();
                Console.WriteLine(t.TransformerName);
                using (var session = store.OpenSession())
                {
                    var result = session.Query<Product>()
                                .Customize(x => x.WaitForNonStaleResults())
                                .TransformWith<ProductWithTransformerParameter.Result>("ProductWithTransformerParameter")
                                .AddTransformerParameter("input", "Foo")
                                .Single();

                    Assert.Equal("Foo", result.Input);

                }
            }
        } 
    }
}
