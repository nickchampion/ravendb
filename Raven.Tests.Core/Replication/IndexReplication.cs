﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Raven.Abstractions.Connection;
using Raven.Abstractions.Data;
using Raven.Abstractions.Indexing;
using Raven.Abstractions.Json;
using Raven.Abstractions.Replication;
using Raven.Client;
using Raven.Client.Connection;
using Raven.Client.Document;
using Raven.Client.Extensions;
using Raven.Client.Indexes;
using Raven.Json.Linq;
using Raven.Tests.Helpers;
using Xunit;

namespace Raven.Tests.Core.Replication
{
	public class IndexReplication : RavenTestBase
	{
		public class User
		{
			public string Id { get; set; }

			public string Name { get; set; }
		}

		public class UserIndex : AbstractIndexCreationTask<User>
		{
			public UserIndex()
			{
				Map = users => from user in users
					select new
					{
						user.Name
					};
			}
		}

		private static void SetupReplication(IDocumentStore source, string databaseName, params IDocumentStore[] destinations)
		{
			source
				.DatabaseCommands
				.ForDatabase(databaseName)
				.Put(
					Constants.RavenReplicationDestinations,
					null,
					RavenJObject.FromObject(new ReplicationDocument
					{
						Destinations = new List<ReplicationDestination>(destinations.Select(destination =>
							new ReplicationDestination
							{
								Database = databaseName,
								Url = destination.Url
							}))

					}),
					new RavenJObject());
		}

		private static void SetupReplication(IDocumentStore source, string databaseName, Func<IDocumentStore, bool> shouldSkipIndexReplication, params IDocumentStore[] destinations)
		{
			var replicationDocument = new ReplicationDocument
			{
				Destinations = new List<ReplicationDestination>(destinations.Select(destination =>
					new ReplicationDestination
					{
						Database = databaseName,
						Url = destination.Url,
						SkipIndexReplication = shouldSkipIndexReplication(destination)
					}))

			};

			using (var session = source.OpenSession(databaseName))
			{
				session.Store(replicationDocument,Constants.RavenReplicationDestinations);
				session.SaveChanges();
			}
		}

		[Fact]
		public void Should_skip_index_replication_if_flag_is_true()
		{
			var requestFactory = new HttpRavenRequestFactory();
			using (var sourceServer = GetNewServer(8077))
			using (var source = NewRemoteDocumentStore(ravenDbServer: sourceServer))
			using (var destinationServer1 = GetNewServer(8078))
			using (var destination1 = NewRemoteDocumentStore(ravenDbServer: destinationServer1))
			using (var destinationServer2 = GetNewServer())
			using (var destination2 = NewRemoteDocumentStore(ravenDbServer: destinationServer2))
			using (var destinationServer3 = GetNewServer(8081))
			using (var destination3 = NewRemoteDocumentStore(ravenDbServer: destinationServer3))
			{
				CreateDatabaseWithReplication(source, "testDB");
				CreateDatabaseWithReplication(destination1, "testDB");
				CreateDatabaseWithReplication(destination2, "testDB");
				CreateDatabaseWithReplication(destination3, "testDB");

				//turn-off automatic index replication - precaution
				source.Conventions.IndexAndTransformerReplicationMode = IndexAndTransformerReplicationMode.None;
				// ReSharper disable once AccessToDisposedClosure
				SetupReplication(source, "testDB", store => store == destination2, destination1, destination2, destination3);
			
				//make sure not to replicate the index automatically
				var userIndex = new UserIndex();
				source.DatabaseCommands.ForDatabase("testDB").PutIndex(userIndex.IndexName, userIndex.CreateIndexDefinition());

				var replicationRequestUrl = string.Format("{0}/databases/testDB/indexes/replicate/{1}", source.Url, userIndex.IndexName);
				var replicationRequest = requestFactory.Create(replicationRequestUrl, "POST", new RavenConnectionStringOptions
				{
					Url = source.Url
				});
				replicationRequest.ExecuteRequest();

				var indexStatsAfterReplication = destination1.DatabaseCommands.ForDatabase("testDB").GetStatistics().Indexes;
				Assert.True(indexStatsAfterReplication.Any(index => index.Name.Equals(userIndex.IndexName, StringComparison.InvariantCultureIgnoreCase)));

				//this one should not have replicated index -> because of SkipIndexReplication = true in ReplicationDocument of source
				indexStatsAfterReplication = destination2.DatabaseCommands.ForDatabase("testDB").GetStatistics().Indexes;
				Assert.False(indexStatsAfterReplication.Any(index => index.Name.Equals(userIndex.IndexName, StringComparison.InvariantCultureIgnoreCase)));

				indexStatsAfterReplication = destination3.DatabaseCommands.ForDatabase("testDB").GetStatistics().Indexes;
				Assert.True(indexStatsAfterReplication.Any(index => index.Name.Equals(userIndex.IndexName, StringComparison.InvariantCultureIgnoreCase)));

			}
		}

		[Fact]
		public void ExecuteIndex_should_replicate_indexes_by_default()
		{
			using (var sourceServer = GetNewServer(8077))
			using (var source = NewRemoteDocumentStore(ravenDbServer: sourceServer))
			using (var destinationServer = GetNewServer(8078))
			using (var destination = NewRemoteDocumentStore(ravenDbServer: destinationServer))
			{
				CreateDatabaseWithReplication(source, "testDB");
				CreateDatabaseWithReplication(destination, "testDB");
				
				SetupReplication(source, "testDB", destination);

				var userIndex = new UserIndex();

				var indexStatsBeforeReplication = destination.DatabaseCommands.ForDatabase("testDB").GetStatistics().Indexes;
				Assert.False(indexStatsBeforeReplication.Any(index => index.Name.Equals(userIndex.IndexName, StringComparison.InvariantCultureIgnoreCase)));
				
				//this should fire http request to index replication endpoint -> so the index is replicated
				userIndex.Execute(source.DatabaseCommands.ForDatabase("testDB"),source.Conventions);
			
				var indexStatsAfterReplication = destination.DatabaseCommands.ForDatabase("testDB").GetStatistics().Indexes;
				Assert.True(indexStatsAfterReplication.Any(index => index.Name.Equals(userIndex.IndexName, StringComparison.InvariantCultureIgnoreCase)));
			}
		}

		[Fact]
		public void ExecuteIndex_should_not_replicate_indexes_if_convention_flag_is_not_set()
		{
			using (var sourceServer = GetNewServer(8077))
			using (var source = NewRemoteDocumentStore(ravenDbServer: sourceServer))
			using (var destinationServer = GetNewServer(8078))
			using (var destination = NewRemoteDocumentStore(ravenDbServer: destinationServer))
			{
				CreateDatabaseWithReplication(source, "testDB");
				CreateDatabaseWithReplication(destination, "testDB");

				SetupReplication(source, "testDB", destination);

				var userIndex = new UserIndex();

				var indexStatsBeforeReplication = destination.DatabaseCommands.ForDatabase("testDB").GetStatistics().Indexes;
				Assert.False(indexStatsBeforeReplication.Any(index => index.Name.Equals(userIndex.IndexName, StringComparison.InvariantCultureIgnoreCase)));

				source.Conventions.IndexAndTransformerReplicationMode = IndexAndTransformerReplicationMode.None;
				userIndex.Execute(source.DatabaseCommands.ForDatabase("testDB"), source.Conventions);

				//since IndexAndTransformerReplicationMode = IndexAndTransformerReplicationMode.None, creation
				//of index on source should not trigger replication to destination
				var indexStatsAfterReplication = destination.DatabaseCommands.ForDatabase("testDB").GetStatistics().Indexes;
				Assert.False(indexStatsAfterReplication.Any(index => index.Name.Equals(userIndex.IndexName, StringComparison.InvariantCultureIgnoreCase)));
			}
		}

		[Fact]
		public void CanReplicateIndex()
		{
			var requestFactory = new HttpRavenRequestFactory();
			using (var sourceServer = GetNewServer(8077))
			using (var source = NewRemoteDocumentStore(ravenDbServer: sourceServer))
			using (var destinationServer = GetNewServer(8078))
			using (var destination = NewRemoteDocumentStore(ravenDbServer: destinationServer))
			{
				CreateDatabaseWithReplication(source, "testDB");
				CreateDatabaseWithReplication(destination, "testDB");

				//turn-off automatic index replication - precaution
				source.Conventions.IndexAndTransformerReplicationMode = IndexAndTransformerReplicationMode.None;
				SetupReplication(source, "testDB", destination);

				//make sure not to replicate the index automatically
				var userIndex = new UserIndex();
				source.DatabaseCommands.ForDatabase("testDB").PutIndex(userIndex.IndexName, userIndex.CreateIndexDefinition());

				var indexStatsBeforeReplication = destination.DatabaseCommands.ForDatabase("testDB").GetStatistics().Indexes;
				Assert.False(indexStatsBeforeReplication.Any(index => index.Name.Equals(userIndex.IndexName, StringComparison.InvariantCultureIgnoreCase)));

				var replicationRequestUrl = string.Format("{0}/databases/testDB/indexes/replicate/{1}", source.Url, userIndex.IndexName);
				var replicationRequest = requestFactory.Create(replicationRequestUrl, "POST", new RavenConnectionStringOptions
				{
					Url = source.Url
				});
				replicationRequest.ExecuteRequest();

				var indexStatsAfterReplication = destination.DatabaseCommands.ForDatabase("testDB").GetStatistics().Indexes;				
				Assert.True(indexStatsAfterReplication.Any(index => index.Name.Equals(userIndex.IndexName, StringComparison.InvariantCultureIgnoreCase)));
			}
		}

		private static void CreateDatabaseWithReplication(DocumentStore store, string databaseName)
		{
			store.DatabaseCommands.GlobalAdmin.CreateDatabase(new DatabaseDocument
			{
				Id = databaseName,
				Settings =
				{
					{"Raven/DataDir", "~/Tenants/" + databaseName},
					{"Raven/ActiveBundles", "Replication"}
				}
			});
		}
	}
}