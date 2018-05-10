namespace LibGit2Sharp.MongoDb.Test
{
    using Models;
    using MongoDB.Driver;

    public class MongoDbOdbBackendForTest : MongoDbOdbBackend
    {
        public MongoDbOdbBackendForTest(string databaseName, string collectionName, MongoDatabaseSettings databaseSettings = null, MongoCollectionSettings collectionSettings = null)
            : base(databaseName, collectionName, databaseSettings, collectionSettings)
        {
        }

        public MongoDbOdbBackendForTest(MongoClientSettings settings, string databaseName, string collectionName, MongoDatabaseSettings databaseSettings = null, MongoCollectionSettings collectionSettings = null)
            : base(settings, databaseName, collectionName, databaseSettings, collectionSettings)
        {
        }

        public IMongoCollection<MongoDbGitObject> Collection => MongoCollection;
    }
}
