using FastTests;
using Raven.NewClient.Client.Data;
using Xunit;

namespace SlowTests.MailingList
{
    public class WildCardQuery : RavenNewTestBase
    {
        [Fact]
        public void CanQuery()
        {
            using (var store = GetDocumentStore())
            {
                using (var commands = store.Commands())
                {
                    commands.Query("dynamic", new IndexQuery(store.Conventions)
                    {
                        Query = "PortalId:0 AND Query:(*) QueryBoosted:(*)"
                    });
                }
            }
        }
    }
}
