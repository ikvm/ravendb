﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using FastTests;
using Raven.Client.Documents.Operations.Revisions;
using Raven.Client.Documents.Session;
using Raven.Client.Documents.Subscriptions;
using Raven.Tests.Core.Utils.Entities;
using Sparrow;
using Sparrow.Json;
using Xunit;

namespace SlowTests.Issues
{
    public class RavenDB_13762 : RavenTestBase
    {
        private readonly TimeSpan _reasonableWaitTime = Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(15);

        [Fact]
        public async Task SessionInSubscriptionsShouldNotTrackRevisions()
        {
            using (var store = GetDocumentStore())
            {
                var subscriptionId = await store.Subscriptions.CreateAsync<Revision<User>>();

                using (var context = JsonOperationContext.ShortTermSingleUse())
                {
                    var configuration = new RevisionsConfiguration
                    {
                        Default = new RevisionsCollectionConfiguration
                        {
                            Disabled = false,
                            MinimumRevisionsToKeep = 5,
                        },
                        Collections = new Dictionary<string, RevisionsCollectionConfiguration>
                        {
                            ["Users"] = new RevisionsCollectionConfiguration
                            {
                                Disabled = false
                            }
                        }
                    };

                    await Server
                        .ServerStore
                        .ModifyDatabaseRevisions(
                            context,
                            store.Database,
                            EntityToBlittable.ConvertCommandToBlittable(configuration, context));
                }

                for (int i = 0; i < 10; i++)
                {
                    for (var j = 0; j < 10; j++)
                    {
                        using (var session = store.OpenSession())
                        {
                            session.Store(new User
                            {
                                Name = $"users{i} ver {j}"
                            }, "users/" + i);

                            session.SaveChanges();
                        }
                    }
                }

                using (var sub = store.Subscriptions.GetSubscriptionWorker<Revision<User>>(new SubscriptionWorkerOptions(subscriptionId)
                {
                    TimeToWaitBeforeConnectionRetry = TimeSpan.FromSeconds(5)
                }))
                {
                    Exception exception = null;
                    var mre = new AsyncManualResetEvent();
                    GC.KeepAlive(sub.Run(x =>
                    {
                        try
                        {
                            using (var session = x.OpenSession())
                            {
                                x.Items[0].Result.Current.Name = "aaaa";

                                session.SaveChanges();
                            }
                        }
                        catch (Exception e)
                        {
                            exception = e;
                        }
                        finally
                        {
                            mre.Set();
                        }
                    }));

                    Assert.True(await mre.WaitAsync(_reasonableWaitTime));

                    if (exception != null)
                        throw exception;
                }
            }
        }
    }
}