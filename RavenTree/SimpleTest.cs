﻿using System.Linq;
using Raven.Client.Embedded;
using Raven.Client.Indexes;
using Xunit;

namespace RavenTree
{
    public class SimpleTest
    {
        [Fact]
        public void Can_create_tree_with_children_count()
        {
            using (var store = new EmbeddableDocumentStore
                                           {
                                               RunInMemory = true
                                           }.Initialize())
            {
                using (var session = store.OpenSession())
                {
                    session.Store(new Category { Id = "categories/1", ParentId = null });
                    session.Store(new Category { Id = "categories/2", ParentId = null });

                    session.Store(new Category { Id = "categories/11", ParentId = "categories/1" });
                    session.Store(new Category { Id = "categories/12", ParentId = "categories/1" });
                    session.Store(new Category { Id = "categories/13", ParentId = "categories/1" });

                    session.Store(new Category { Id = "categories/21", ParentId = "categories/2" });
                    session.Store(new Category { Id = "categories/22", ParentId = "categories/2" });

                    session.SaveChanges();
                }

                new CategoriesWithChildrenCount().Execute(store);

                using (var session = store.OpenSession())
                {
                    var results = session.Query<CategoriesWithChildrenCount.ReduceResult, CategoriesWithChildrenCount>()
                        .Customize(x => x.WaitForNonStaleResultsAsOfLastWrite())
                        .ToList();

                    Assert.NotEmpty(results);
                    Assert.Equal(3, results.First(x => x.Id == "categories/1").Count);
                    Assert.Equal(2, results.First(x => x.Id == "categories/2").Count);
                }
            }
        }

        public class Category
        {
            public string Id { get; set; }
            public string ParentId { get; set; }
        }

        public class CategoriesWithChildrenCount : AbstractIndexCreationTask<Category, CategoriesWithChildrenCount.ReduceResult>
        {
            public class ReduceResult
            {
                public string Id { get; set; }
                public int Count { get; set; }
                public string ParentId { get; set; }
            }

            public CategoriesWithChildrenCount()
            {
                Map = categories => from category in categories
                                    let items = new[]
				                    {
				                    	new {Id = (string) category.Id, Count = (int) 0, ParentId = (string) category.ParentId},
				                    	// explicitly casting is important here!
				                    	new {Id = (string) category.ParentId, Count = (int) 1, ParentId = (string) null}
				                    }
                                    from item in items
                                    select new
                                    {
                                        Id = item.Id,
                                        // don't follow resharper-suggestion to remove redundant names!
                                        Count = item.Count,
                                        ParentId = item.ParentId
                                    };

                Reduce = results => from result in results
                                    group result by result.Id into g
                                    let itemWithParent = g.FirstOrDefault(x => x.ParentId != null)
                                    select new
                                    {
                                        Id = g.Key,
                                        Count = g.Sum(x => x.Count),
                                        ParentId = (itemWithParent == null) ? (string)null : itemWithParent.ParentId
                                    };
            }
        }
    }
}